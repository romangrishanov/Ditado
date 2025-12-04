using Ditado.Aplicacao.DTOs.Turmas;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ditado.Testes.Services;

public class TurmaServiceTests : IDisposable
{
	private readonly DitadoDbContext _context;
	private readonly TurmaService _turmaService;
	private readonly PasswordHasher _passwordHasher;

	public TurmaServiceTests()
	{
		var options = new DbContextOptionsBuilder<DitadoDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		_context = new DitadoDbContext(options);
		_passwordHasher = new PasswordHasher();
		_turmaService = new TurmaService(_context);
	}

	public void Dispose()
	{
		_context.Database.EnsureDeleted();
		_context.Dispose();
	}

	private async Task<Usuario> CriarProfessorAsync()
	{
		var professor = new Usuario
		{
			Nome = "Professor Teste",
			Login = $"prof{Guid.NewGuid()}@teste.com",
			SenhaHash = _passwordHasher.Hash("senha123"),
			Tipo = TipoUsuario.Professor,
			Ativo = true
		};
		_context.Usuarios.Add(professor);
		await _context.SaveChangesAsync();
		return professor;
	}

	private async Task<Turma> CriarTurmaAsync(int professorId)
	{
		var turma = new Turma
		{
			Nome = "5º Ano A",
			Serie = 5,
			AnoLetivo = 2024,
			ProfessorResponsavelId = professorId,
			Ativo = true
		};
		_context.Turmas.Add(turma);
		await _context.SaveChangesAsync();
		return turma;
	}

	private async Task<Dominio.Entidades.Ditado> CriarDitadoAsync(int autorId)
	{
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = "Ditado Teste",
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = autorId,
			Ativo = true
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();
		return ditado;
	}

	[Fact]
	public async Task AtribuirDitado_ComDadosValidos_DeveAtribuirComSucesso()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma = await CriarTurmaAsync(professor.Id);
		var ditado = await CriarDitadoAsync(professor.Id);

		var request = new AtribuirDitadoRequest
		{
			DitadoId = ditado.Id,
			DataLimite = DateTime.UtcNow.AddDays(7)
		};

		// Act
		var resultado = await _turmaService.AtribuirDitadoAsync(turma.Id, request);

		// Assert
		Assert.True(resultado);
		var atribuicao = await _context.TurmaDitados
			.FirstOrDefaultAsync(td => td.TurmaId == turma.Id && td.DitadoId == ditado.Id);
		Assert.NotNull(atribuicao);
		Assert.Equal(request.DataLimite.Date, atribuicao.DataLimite.Date);
		Assert.True(atribuicao.DataAtribuicao <= DateTime.UtcNow);
	}

	[Fact]
	public async Task AtribuirDitado_TurmaInexistente_DeveRetornarFalse()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var ditado = await CriarDitadoAsync(professor.Id);

		var request = new AtribuirDitadoRequest
		{
			DitadoId = ditado.Id,
			DataLimite = DateTime.UtcNow.AddDays(7)
		};

		// Act
		var resultado = await _turmaService.AtribuirDitadoAsync(999, request);

		// Assert
		Assert.False(resultado);
	}

	[Fact]
	public async Task AtribuirDitado_DitadoInexistente_DeveLancarExcecao()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma = await CriarTurmaAsync(professor.Id);

		var request = new AtribuirDitadoRequest
		{
			DitadoId = 999,
			DataLimite = DateTime.UtcNow.AddDays(7)
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _turmaService.AtribuirDitadoAsync(turma.Id, request)
		);

		Assert.Equal("Ditado não encontrado.", exception.Message);
	}

	[Fact]
	public async Task AtribuirDitado_DitadoJaAtribuido_DeveLancarExcecao()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma = await CriarTurmaAsync(professor.Id);
		var ditado = await CriarDitadoAsync(professor.Id);

		var atribuicaoExistente = new TurmaDitado
		{
			TurmaId = turma.Id,
			DitadoId = ditado.Id,
			DataAtribuicao = DateTime.UtcNow,
			DataLimite = DateTime.UtcNow.AddDays(5)
		};
		_context.TurmaDitados.Add(atribuicaoExistente);
		await _context.SaveChangesAsync();

		var request = new AtribuirDitadoRequest
		{
			DitadoId = ditado.Id,
			DataLimite = DateTime.UtcNow.AddDays(7)
		};

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _turmaService.AtribuirDitadoAsync(turma.Id, request)
		);

		Assert.Equal("Este ditado já está atribuído a esta turma.", exception.Message);
	}

	[Fact]
	public async Task AtualizarAtribuicao_ComDadosValidos_DeveAtualizarDataLimite()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma = await CriarTurmaAsync(professor.Id);
		var ditado = await CriarDitadoAsync(professor.Id);

		var atribuicao = new TurmaDitado
		{
			TurmaId = turma.Id,
			DitadoId = ditado.Id,
			DataAtribuicao = DateTime.UtcNow,
			DataLimite = DateTime.UtcNow.AddDays(5)
		};
		_context.TurmaDitados.Add(atribuicao);
		await _context.SaveChangesAsync();

		var novaDataLimite = DateTime.UtcNow.AddDays(10);
		var request = new AtualizarAtribuicaoRequest
		{
			DataLimite = novaDataLimite
		};

		// Act
		var resultado = await _turmaService.AtualizarAtribuicaoAsync(turma.Id, ditado.Id, request);

		// Assert
		Assert.True(resultado);
		var atribuicaoAtualizada = await _context.TurmaDitados
			.FirstAsync(td => td.TurmaId == turma.Id && td.DitadoId == ditado.Id);
		Assert.Equal(novaDataLimite.Date, atribuicaoAtualizada.DataLimite.Date);
	}

	[Fact]
	public async Task AtualizarAtribuicao_AtribuicaoInexistente_DeveRetornarFalse()
	{
		// Arrange
		var request = new AtualizarAtribuicaoRequest
		{
			DataLimite = DateTime.UtcNow.AddDays(10)
		};

		// Act
		var resultado = await _turmaService.AtualizarAtribuicaoAsync(999, 999, request);

		// Assert
		Assert.False(resultado);
	}

	[Fact]
	public async Task RemoverAtribuicao_AtribuicaoExistente_DeveRemoverComSucesso()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma = await CriarTurmaAsync(professor.Id);
		var ditado = await CriarDitadoAsync(professor.Id);

		var atribuicao = new TurmaDitado
		{
			TurmaId = turma.Id,
			DitadoId = ditado.Id,
			DataAtribuicao = DateTime.UtcNow,
			DataLimite = DateTime.UtcNow.AddDays(5)
		};
		_context.TurmaDitados.Add(atribuicao);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _turmaService.RemoverAtribuicaoAsync(turma.Id, ditado.Id);

		// Assert
		Assert.True(resultado);
		var atribuicaoRemovida = await _context.TurmaDitados
			.FirstOrDefaultAsync(td => td.TurmaId == turma.Id && td.DitadoId == ditado.Id);
		Assert.Null(atribuicaoRemovida);
	}

	[Fact]
	public async Task RemoverAtribuicao_AtribuicaoInexistente_DeveRetornarFalse()
	{
		// Act
		var resultado = await _turmaService.RemoverAtribuicaoAsync(999, 999);

		// Assert
		Assert.False(resultado);
	}

	[Fact]
	public async Task AtribuirDitado_MesmoDitadoParaDiferentesTurmas_DevePermitir()
	{
		// Arrange
		var professor = await CriarProfessorAsync();
		var turma1 = await CriarTurmaAsync(professor.Id);
		var turma2 = new Turma
		{
			Nome = "5º Ano B",
			Serie = 5,
			AnoLetivo = 2024,
			ProfessorResponsavelId = professor.Id,
			Ativo = true
		};
		_context.Turmas.Add(turma2);
		await _context.SaveChangesAsync();

		var ditado = await CriarDitadoAsync(professor.Id);

		var request1 = new AtribuirDitadoRequest
		{
			DitadoId = ditado.Id,
			DataLimite = DateTime.UtcNow.AddDays(7)
		};

		var request2 = new AtribuirDitadoRequest
		{
			DitadoId = ditado.Id,
			DataLimite = DateTime.UtcNow.AddDays(10)
		};

		// Act
		var resultado1 = await _turmaService.AtribuirDitadoAsync(turma1.Id, request1);
		var resultado2 = await _turmaService.AtribuirDitadoAsync(turma2.Id, request2);

		// Assert
		Assert.True(resultado1);
		Assert.True(resultado2);
		var atribuicoes = await _context.TurmaDitados
			.Where(td => td.DitadoId == ditado.Id)
			.ToListAsync();
		Assert.Equal(2, atribuicoes.Count);
	}
}