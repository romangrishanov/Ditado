using Ditado.Aplicacao.DTOs;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ditado.Testes.Services;

public class DitadoServiceTests : IDisposable
{
	private readonly DitadoDbContext _context;
	private readonly DitadoService _ditadoService;
	private readonly CategoriaService _categoriaService;

	public DitadoServiceTests()
	{
		var options = new DbContextOptionsBuilder<DitadoDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		_context = new DitadoDbContext(options);
		_categoriaService = new CategoriaService(_context);
		_ditadoService = new DitadoService(_context, _categoriaService);
	}

	public void Dispose()
	{
		_context.Database.EnsureDeleted();
		_context.Dispose();
	}

	private string GerarAudioBase64Valido()
	{
		var audioBytes = new byte[] { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70 };
		return $"data:audio/mpeg;base64,{Convert.ToBase64String(audioBytes)}";
	}

	private async Task<Usuario> CriarUsuarioTesteAsync(TipoUsuario tipo = TipoUsuario.Professor)
	{
		var usuario = new Usuario
		{
			Nome = "Professor Teste",
			Login = $"professor{Guid.NewGuid()}@teste.com",
			SenhaHash = "hash",
			Tipo = tipo,
			Ativo = true
		};
		_context.Usuarios.Add(usuario);
		await _context.SaveChangesAsync();
		return usuario;
	}

	[Fact]
	public async Task CriarDitado_SemCategorias_DeveCriarComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var request = new CriarDitadoRequest
		{
			Titulo = "Ditado Teste",
			Descricao = "Descrição teste",
			TextoComMarcacoes = "O [gato] é bonito.",
			AudioBase64 = GerarAudioBase64Valido(),
			CategoriaIds = new List<int>()
		};

		// Act
		var resultado = await _ditadoService.CriarDitadoAsync(request, professor.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.True(resultado.Id > 0);
		Assert.Equal("Ditado Teste", resultado.Titulo);
		Assert.Equal(professor.Id, resultado.AutorId);
		Assert.Equal("Professor Teste", resultado.AutorNome);
		Assert.Empty(resultado.Categorias);
	}

	[Fact]
	public async Task CriarDitado_ComUmaCategoria_DeveCriarComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		var request = new CriarDitadoRequest
		{
			Titulo = "Ditado Teste",
			TextoComMarcacoes = "O [gato] é bonito.",
			AudioBase64 = GerarAudioBase64Valido(),
			CategoriaIds = new List<int> { categoria.Id }
		};

		// Act
		var resultado = await _ditadoService.CriarDitadoAsync(request, professor.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Single(resultado.Categorias);
		Assert.Equal("Ortografia", resultado.Categorias[0].Nome);
		Assert.Equal(professor.Id, resultado.AutorId);
	}

	[Fact]
	public async Task CriarDitado_ComMultiplasCategorias_DeveCriarComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var cat1 = new Categoria { Nome = "Ortografia" };
		var cat2 = new Categoria { Nome = "Pontuação" };
		var cat3 = new Categoria { Nome = "Acentuação" };
		_context.Categorias.AddRange(cat1, cat2, cat3);
		await _context.SaveChangesAsync();

		var request = new CriarDitadoRequest
		{
			Titulo = "Ditado Completo",
			TextoComMarcacoes = "O [gato] é bonito.",
			AudioBase64 = GerarAudioBase64Valido(),
			CategoriaIds = new List<int> { cat1.Id, cat2.Id, cat3.Id }
		};

		// Act
		var resultado = await _ditadoService.CriarDitadoAsync(request, professor.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal(3, resultado.Categorias.Count);
		Assert.Contains(resultado.Categorias, c => c.Nome == "Ortografia");
		Assert.Contains(resultado.Categorias, c => c.Nome == "Pontuação");
		Assert.Contains(resultado.Categorias, c => c.Nome == "Acentuação");
	}

	[Fact]
	public async Task CriarDitado_ComCategoriaDuplicada_DeveIgnorarDuplicatas()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		var request = new CriarDitadoRequest
		{
			Titulo = "Ditado Teste",
			TextoComMarcacoes = "O [gato] é bonito.",
			AudioBase64 = GerarAudioBase64Valido(),
			CategoriaIds = new List<int> { categoria.Id, categoria.Id, categoria.Id }
		};

		// Act
		var resultado = await _ditadoService.CriarDitadoAsync(request, professor.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Single(resultado.Categorias);
	}

	[Fact]
	public async Task CriarDitado_ComCategoriaInexistente_DeveLancarExcecao()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var request = new CriarDitadoRequest
		{
			Titulo = "Ditado Teste",
			TextoComMarcacoes = "O [gato] é bonito.",
			AudioBase64 = GerarAudioBase64Valido(),
			CategoriaIds = new List<int> { 999 }
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _ditadoService.CriarDitadoAsync(request, professor.Id)
		);

		Assert.Contains("Categorias não encontradas", exception.Message);
	}

	[Fact]
	public async Task AtualizarDitado_AdicionarCategorias_DeveAtualizarComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Original",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		var categoria = new Categoria { Nome = "Ortografia" };
		_context.Categorias.Add(categoria);
		await _context.SaveChangesAsync();

		var request = new AtualizarDitadoRequest
		{
			CategoriaIds = new List<int> { categoria.Id }
		};

		// Act
		var resultado = await _ditadoService.AtualizarDitadoAsync(ditado.Id, request);

		// Assert
		Assert.NotNull(resultado);
		Assert.Single(resultado.Categorias);
		Assert.Equal("Ortografia", resultado.Categorias[0].Nome);
	}

	[Fact]
	public async Task AtualizarDitado_RemoverTodasCategorias_DeveRemoverComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var categoria = new Categoria { Nome = "Ortografia" };
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Original",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
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

		var request = new AtualizarDitadoRequest
		{
			CategoriaIds = new List<int>()
		};

		// Act
		var resultado = await _ditadoService.AtualizarDitadoAsync(ditado.Id, request);

		// Assert
		Assert.NotNull(resultado);
		Assert.Empty(resultado.Categorias);
	}

	[Fact]
	public async Task AtualizarDitado_SubstituirCategorias_DeveSubstituirComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var cat1 = new Categoria { Nome = "Ortografia" };
		var cat2 = new Categoria { Nome = "Pontuação" };
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Original",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
		};
		_context.Categorias.AddRange(cat1, cat2);
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		_context.DitadoCategorias.Add(new DitadoCategoria
		{
			DitadoId = ditado.Id,
			CategoriaId = cat1.Id
		});
		await _context.SaveChangesAsync();

		var request = new AtualizarDitadoRequest
		{
			CategoriaIds = new List<int> { cat2.Id }
		};

		// Act
		var resultado = await _ditadoService.AtualizarDitadoAsync(ditado.Id, request);

		// Assert
		Assert.NotNull(resultado);
		Assert.Single(resultado.Categorias);
		Assert.Equal("Pontuação", resultado.Categorias[0].Nome);
		Assert.DoesNotContain(resultado.Categorias, c => c.Nome == "Ortografia");
	}

	[Fact]
	public async Task AtualizarDitado_SemAlterarCategorias_NaoDeveModificarCategorias()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var categoria = new Categoria { Nome = "Ortografia" };
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Original",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
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

		var request = new AtualizarDitadoRequest
		{
			Titulo = "Título Atualizado"
		};

		// Act
		var resultado = await _ditadoService.AtualizarDitadoAsync(ditado.Id, request);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal("Título Atualizado", resultado.Titulo);
		Assert.Single(resultado.Categorias);
		Assert.Equal("Ortografia", resultado.Categorias[0].Nome);
	}

	[Fact]
	public async Task ListarDitados_DeveIncluirCategorias()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var cat1 = new Categoria { Nome = "Ortografia" };
		var cat2 = new Categoria { Nome = "Pontuação" };
		var ditado1 = new Dominio.Entidades.Ditado 
		{ 
			Titulo = "Ditado 1", 
			AudioLeitura = new byte[] { 1 },
			AutorId = professor.Id
		};
		var ditado2 = new Dominio.Entidades.Ditado 
		{ 
			Titulo = "Ditado 2", 
			AudioLeitura = new byte[] { 2 },
			AutorId = professor.Id
		};

		_context.Categorias.AddRange(cat1, cat2);
		_context.Ditados.AddRange(ditado1, ditado2);
		await _context.SaveChangesAsync();

		_context.DitadoCategorias.AddRange(
			new DitadoCategoria { DitadoId = ditado1.Id, CategoriaId = cat1.Id },
			new DitadoCategoria { DitadoId = ditado1.Id, CategoriaId = cat2.Id },
			new DitadoCategoria { DitadoId = ditado2.Id, CategoriaId = cat1.Id }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ListarDitadosAsync();

		// Assert
		Assert.Equal(2, resultado.Count);
		
		var ditado1Result = resultado.First(d => d.Titulo == "Ditado 1");
		Assert.Equal(2, ditado1Result.Categorias.Count);
		Assert.Equal(professor.Id, ditado1Result.AutorId);
		Assert.Equal("Professor Teste", ditado1Result.AutorNome);
		
		var ditado2Result = resultado.First(d => d.Titulo == "Ditado 2");
		Assert.Single(ditado2Result.Categorias);
	}

	[Fact]
	public async Task DeletarDitado_PorAutor_DeveExcluirComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Teste",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.DeletarDitadoAsync(ditado.Id, professor.Id, TipoUsuario.Professor);

		// Assert
		Assert.True(resultado);
		Assert.Empty(await _context.Ditados.ToListAsync());
	}

	[Fact]
	public async Task DeletarDitado_PorAdmin_DeveExcluirComSucesso()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var admin = await CriarUsuarioTesteAsync(TipoUsuario.Administrador);
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Teste",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.DeletarDitadoAsync(ditado.Id, admin.Id, TipoUsuario.Administrador);

		// Assert
		Assert.True(resultado);
		Assert.Empty(await _context.Ditados.ToListAsync());
	}

	[Fact]
	public async Task DeletarDitado_PorProfessorNaoAutor_DeveLancarExcecao()
	{
		// Arrange
		var autor = await CriarUsuarioTesteAsync();
		var outroProfessor = await CriarUsuarioTesteAsync();
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Teste",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = autor.Id
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _ditadoService.DeletarDitadoAsync(ditado.Id, outroProfessor.Id, TipoUsuario.Professor)
		);

		Assert.Equal("Apenas o autor do ditado ou um administrador podem excluí-lo.", exception.Message);
		Assert.NotEmpty(await _context.Ditados.ToListAsync()); // Ditado não foi excluído
	}

	[Fact]
	public async Task DeletarDitado_SemAutor_AdminPodeExcluir()
	{
		// Arrange
		var admin = await CriarUsuarioTesteAsync(TipoUsuario.Administrador);
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Sem Autor",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = null // Sem autor (professor foi excluído)
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.DeletarDitadoAsync(ditado.Id, admin.Id, TipoUsuario.Administrador);

		// Assert
		Assert.True(resultado);
		Assert.Empty(await _context.Ditados.ToListAsync());
	}

	[Fact]
	public async Task DeletarDitado_SemAutor_ProfessorNaoPodeExcluir()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Sem Autor",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = null
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _ditadoService.DeletarDitadoAsync(ditado.Id, professor.Id, TipoUsuario.Professor)
		);

		Assert.Equal("Apenas o autor do ditado ou um administrador podem excluí-lo.", exception.Message);
	}

	[Fact]
	public async Task DeletarDitado_Inexistente_DeveRetornarFalse()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();

		// Act
		var resultado = await _ditadoService.DeletarDitadoAsync(999, professor.Id, TipoUsuario.Professor);

		// Assert
		Assert.False(resultado);
	}

	// Atualizar o teste de submissão de resposta:

	[Fact]
	public async Task SubmeterResposta_ComRespostasValidas_DeveCalcularNotaCorretamente()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var aluno = await CriarUsuarioTesteAsync(TipoUsuario.Aluno);
		
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Teste",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id,
			Ativo = true
		};
		
		var segmento1 = new DitadoSegmento { Ordem = 1, Tipo = TipoSegmento.Lacuna, Conteudo = "gato" };
		var segmento2 = new DitadoSegmento { Ordem = 2, Tipo = TipoSegmento.Lacuna, Conteudo = "cachorro" };
		ditado.Segmentos.Add(segmento1);
		ditado.Segmentos.Add(segmento2);
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		var request = new SubmeterRespostaRequest
		{
			Respostas = new List<RespostaSegmentoDto>
			{
				new() { SegmentoId = segmento1.Id, Resposta = "gato" },
				new() { SegmentoId = segmento2.Id, Resposta = "cachorro" }
			}
		};

		// Act
		var resultado = await _ditadoService.SubmeterRespostaAsync(ditado.Id, request, aluno.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal(100.0m, resultado.Nota);
		Assert.Equal(2, resultado.TotalLacunas);
		Assert.Equal(2, resultado.Acertos);
		Assert.Equal(0, resultado.Erros);
		
		// Verificar que foi salvo com AlunoId
		var respostaSalva = await _context.RespostaDitados.FirstAsync();
		Assert.Equal(aluno.Id, respostaSalva.AlunoId);
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_ComIdValido_DeveRetornarDitadoCompleto()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Completo",
			Descricao = "Teste de visualização",
			AudioLeitura = Convert.FromBase64String("SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk="),
			AutorId = professor.Id,
			Ativo = true
		};
		
		// Adicionar segmentos
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 1, 
			Tipo = TipoSegmento.Texto, 
			Conteudo = "O " 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 2, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "cachorro" 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 3, 
			Tipo = TipoSegmento.Texto, 
			Conteudo = " late e o " 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 4, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "gato" 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 5, 
			Tipo = TipoSegmento.Texto, 
			Conteudo = " mia." 
		});
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(ditado.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal(ditado.Id, resultado.Id);
		Assert.Equal("Ditado Completo", resultado.Titulo);
		Assert.Equal("Teste de visualização", resultado.Descricao);
		Assert.StartsWith("data:audio/mpeg;base64,", resultado.AudioBase64);
		Assert.Equal(5, resultado.Segmentos.Count); // "O ", [cachorro], " late e o ", [gato], " mia."
		Assert.Equal(professor.Id, resultado.AutorId);
		Assert.Equal("Professor Teste", resultado.AutorNome);

		// Verificar que as lacunas têm o conteúdo visível
		var lacunas = resultado.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();
		Assert.Equal(2, lacunas.Count);
		Assert.Equal("cachorro", lacunas[0].Conteudo);
		Assert.Equal("gato", lacunas[1].Conteudo);
		
		// Verificar ordem dos segmentos
		Assert.Equal("O ", resultado.Segmentos[0].Conteudo);
		Assert.Equal("cachorro", resultado.Segmentos[1].Conteudo);
		Assert.Equal(" late e o ", resultado.Segmentos[2].Conteudo);
		Assert.Equal("gato", resultado.Segmentos[3].Conteudo);
		Assert.Equal(" mia.", resultado.Segmentos[4].Conteudo);
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_ComIdInexistente_DeveRetornarNull()
	{
		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(99999);

		// Assert
		Assert.Null(resultado);
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_DitadoInativo_DeveRetornarNull()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Inativo",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id,
			Ativo = false // Inativo
		};
		
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 1, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "teste" 
		});
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(ditado.Id);

		// Assert
		Assert.Null(resultado); // Ditados inativos não devem ser retornados
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_ComCategorias_DeveIncluirCategorias()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		var cat1 = new Categoria { Nome = "Ortografia" };
		var cat2 = new Categoria { Nome = "5º Ano" };
		_context.Categorias.AddRange(cat1, cat2);
		await _context.SaveChangesAsync();
		
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado com Categorias",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id,
			Ativo = true
		};
		
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 1, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "casa" 
		});
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();
		
		_context.DitadoCategorias.AddRange(
			new DitadoCategoria { DitadoId = ditado.Id, CategoriaId = cat1.Id },
			new DitadoCategoria { DitadoId = ditado.Id, CategoriaId = cat2.Id }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(ditado.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal(2, resultado.Categorias.Count);
		Assert.Contains(resultado.Categorias, c => c.Nome == "Ortografia");
		Assert.Contains(resultado.Categorias, c => c.Nome == "5º Ano");
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_SemAutor_DeveRetornarAutorNomeNull()
	{
		// Arrange
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Sem Autor",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = null, // Sem autor
			Ativo = true
		};
		
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 1, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "teste" 
		});
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(ditado.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Null(resultado.AutorId);
		Assert.Null(resultado.AutorNome);
	}

	[Fact]
	public async Task ObterDitadoCompletoPorId_ApenaLacunas_DeveRetornarTodasVisiveis()
	{
		// Arrange
		var professor = await CriarUsuarioTesteAsync();
		
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Só Lacunas",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = professor.Id,
			Ativo = true
		};
		
		// Apenas lacunas, sem texto
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 1, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "primeira" 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 2, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "segunda" 
		});
		ditado.Segmentos.Add(new DitadoSegmento 
		{ 
			Ordem = 3, 
			Tipo = TipoSegmento.Lacuna, 
			Conteudo = "terceira" 
		});
		
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _ditadoService.ObterDitadoCompletoPorIdAsync(ditado.Id);

		// Assert
		Assert.NotNull(resultado);
		Assert.Equal(3, resultado.Segmentos.Count);
		Assert.All(resultado.Segmentos, s => Assert.Equal("Lacuna", s.Tipo));
		Assert.Equal("primeira", resultado.Segmentos[0].Conteudo);
		Assert.Equal("segunda", resultado.Segmentos[1].Conteudo);
		Assert.Equal("terceira", resultado.Segmentos[2].Conteudo);
	}
}