using Ditado.Aplicacao.DTOs.Categorias;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Infra.Data;
using Ditado.Testes.Infra;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ditado.Testes.Services;

public class CategoriaServiceTests : IDisposable
{
	private readonly DitadoDbContext _context;
	private readonly CategoriaService _service;

	public CategoriaServiceTests()
	{
		var options = new DbContextOptionsBuilder<DitadoDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		_context = new DitadoDbContext(options);
		_service = new CategoriaService(_context);
	}

	public void Dispose()
	{
		_context.Database.EnsureDeleted();
		_context.Dispose();
	}

	[Fact]
	public async Task CriarCategoria_DeveRetornarCategoriaComId()
	{
		// Arrange
		var request = new CriarCategoriaRequest { Nome = "Ortografia" };

		// Act
		var resultado = await _service.CriarCategoriaAsync(request);

		// Assert
		Assert.NotNull(resultado);
		Assert.True(resultado.Id > 0);
		Assert.Equal("Ortografia", resultado.Nome);
		Assert.Equal(0, resultado.TotalDitados);
	}

	[Fact]
	public async Task CriarCategoria_ComNomeVazio_DeveLancarExcecao()
	{
		// Arrange
		var request = new CriarCategoriaRequest { Nome = "" };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.CriarCategoriaAsync(request)
		);

		Assert.Equal("Nome da categoria é obrigatório.", exception.Message);
	}

	[Fact]
	public async Task CriarCategoria_ComNomeDuplicado_DeveLancarExcecao()
	{
		// Arrange
		_context.Categorias.Add(new Categoria { Nome = "Ortografia" });
		await _context.SaveChangesAsync();

		var request = new CriarCategoriaRequest { Nome = "Ortografia" };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.CriarCategoriaAsync(request)
		);

		Assert.Equal("Já existe uma categoria com este nome.", exception.Message);
	}

	[Fact]
	public async Task CriarCategoria_ComNomeDuplicadoCaseInsensitive_DeveLancarExcecao()
	{
		// Arrange
		_context.Categorias.Add(new Categoria { Nome = "Ortografia" });
		await _context.SaveChangesAsync();

		var request = new CriarCategoriaRequest { Nome = "ORTOGRAFIA" };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.CriarCategoriaAsync(request)
		);

		Assert.Equal("Já existe uma categoria com este nome.", exception.Message);
	}

	[Fact]
	public async Task ListarCategorias_DeveRetornarListaOrdenada()
	{
		// Arrange
		_context.Categorias.AddRange(
			new Categoria { Nome = "Pontuação" },
			new Categoria { Nome = "Acentuação" },
			new Categoria { Nome = "Ortografia" }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _service.ListarCategoriasAsync();

		// Assert
		Assert.Equal(3, resultado.Count);
		Assert.Equal("Acentuação", resultado[0].Nome); // Ordem alfabética
		Assert.Equal("Ortografia", resultado[1].Nome);
		Assert.Equal("Pontuação", resultado[2].Nome);
	}

	[Fact]
	public async Task ObterPorId_CategoriaExistente_DeveRetornarCategoria()
	{
		// Arrange
		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _service.ObterPorIdAsync(categoria.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal("Ortografia", resultado.Nome);
	}

	[Fact]
	public async Task ObterPorId_CategoriaInexistente_DeveRetornarNull()
	{
		// Act
		var resultado = await _service.ObterPorIdAsync(999);

		// Assert
		Assert.Null(resultado);
	}

	[Fact]
	public async Task AtualizarCategoria_DeveAlterarNome()
	{
		// Arrange
		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		var request = new AtualizarCategoriaRequest { Nome = "Ortografia Avançada" };

		// Act
		var resultado = await _service.AtualizarCategoriaAsync(categoria.Id, request);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal("Ortografia Avançada", resultado.Nome);
	}

	[Fact]
	public async Task AtualizarCategoria_ComNomeDuplicado_DeveLancarExcecao()
	{
		// Arrange
		_context.Categorias.AddRange(
			new Categoria { Nome = "Ortografia" },
			new Categoria { Nome = "Pontuação" }
		);
		await _context.SaveChangesAsync();

		var categoriaParaAtualizar = await _context.Categorias.FirstAsync(c => c.Nome == "Pontuação");
		var request = new AtualizarCategoriaRequest { Nome = "Ortografia" };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.AtualizarCategoriaAsync(categoriaParaAtualizar.Id, request)
		);

		Assert.Equal("Já existe outra categoria com este nome.", exception.Message);
	}

	[Fact]
	public async Task DeletarCategoria_DeveRemoverCategoria()
	{
		// Arrange
		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _service.DeletarCategoriaAsync(categoria.Id);

		// Assert
		Assert.True(resultado);
		Assert.Empty(await _context.Categorias.ToListAsync());
	}

	[Fact]
	public async Task DeletarCategoria_ComDitadosAssociados_DeveRemoverAssociacoes()
	{
		// Arrange
		var categoria = new Categoria { Nome = "Ortografia" };
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Teste",
			AudioLeitura = new byte[] { 1, 2, 3 }
		};
		_context.Categorias.Add(categoria);
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		_context.DitadoCategorias.Add(new DitadoCategoria
		{
			DitadoId = ditado.Id,
			CategoriaId = categoria.Id
		});
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _service.DeletarCategoriaAsync(categoria.Id);

		// Assert
		Assert.True(resultado);
		Assert.Empty(await _context.Categorias.ToListAsync());
		Assert.Empty(await _context.DitadoCategorias.ToListAsync());
		Assert.NotEmpty(await _context.Ditados.ToListAsync()); // Ditado ainda existe
	}

	[Fact]
	public async Task ValidarCategorias_ComCategoriasValidas_DeveRetornarListaDeIds()
	{
		// Arrange
		_context.Categorias.AddRange(
			new Categoria { Nome = "Categoria 1" },
			new Categoria { Nome = "Categoria 2" }
		);
		await _context.SaveChangesAsync();

		var ids = await _context.Categorias.Select(c => c.Id).ToListAsync();

		// Act
		var resultado = await _service.ValidarCategoriasAsync(ids);

		// Assert
		Assert.Equal(2, resultado.Count);
	}

	[Fact]
	public async Task ValidarCategorias_ComCategoriaInexistente_DeveLancarExcecao()
	{
		// Arrange
		_context.Categorias.Add(new Categoria { Nome = "Categoria 1" });
		await _context.SaveChangesAsync();

		var ids = new List<int> { 1, 999 };

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _service.ValidarCategoriasAsync(ids)
		);

		Assert.Contains("Categorias não encontradas: 999", exception.Message);
	}

	[Fact]
	public async Task ValidarCategorias_ComListaVazia_DeveRetornarListaVazia()
	{
		// Act
		var resultado = await _service.ValidarCategoriasAsync(new List<int>());

		// Assert
		Assert.Empty(resultado);
	}
}