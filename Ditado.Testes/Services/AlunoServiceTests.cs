using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ditado.Testes.Services;

public class AlunoServiceTests : IDisposable
{
	private readonly DitadoDbContext _context;
	private readonly AlunoService _alunoService;
	private readonly PasswordHasher _passwordHasher;

	public AlunoServiceTests()
	{
		var options = new DbContextOptionsBuilder<DitadoDbContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options;

		_context = new DitadoDbContext(options);
		_passwordHasher = new PasswordHasher();
		_alunoService = new AlunoService(_context);
	}

	public void Dispose()
	{
		_context.Database.EnsureDeleted();
		_context.Dispose();
	}

	private async Task<Usuario> CriarUsuarioAsync(TipoUsuario tipo, string nome = "Usuário Teste")
	{
		var usuario = new Usuario
		{
			Nome = nome,
			Login = $"{tipo.ToString().ToLower()}{Guid.NewGuid()}@teste.com",
			SenhaHash = _passwordHasher.Hash("senha123"),
			Tipo = tipo,
			Ativo = true
		};
		_context.Usuarios.Add(usuario);
		await _context.SaveChangesAsync();
		return usuario;
	}

	private async Task<Turma> CriarTurmaAsync(int professorId, string nome = "5º Ano A")
	{
		var turma = new Turma
		{
			Nome = nome,
			Serie = 5,
			AnoLetivo = 2024,
			ProfessorResponsavelId = professorId,
			Ativo = true
		};
		_context.Turmas.Add(turma);
		await _context.SaveChangesAsync();
		return turma;
	}

	private async Task AdicionarAlunoNaTurmaAsync(int alunoId, int turmaId)
	{
		var aluno = await _context.Usuarios.FindAsync(alunoId);
		var turma = await _context.Turmas.Include(t => t.Alunos).FirstAsync(t => t.Id == turmaId);
		turma.Alunos.Add(aluno!);
		await _context.SaveChangesAsync();
	}

	private async Task<Dominio.Entidades.Ditado> CriarDitadoAsync(int autorId, string titulo = "Ditado Teste")
	{
		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = titulo,
			AudioLeitura = new byte[] { 1, 2, 3 },
			AutorId = autorId,
			Ativo = true
		};
		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();
		return ditado;
	}

	private async Task AtribuirDitadoAsync(int turmaId, int ditadoId, DateTime dataLimite)
	{
		var turmaDitado = new TurmaDitado
		{
			TurmaId = turmaId,
			DitadoId = ditadoId,
			DataAtribuicao = DateTime.UtcNow,
			DataLimite = dataLimite
		};
		_context.TurmaDitados.Add(turmaDitado);
		await _context.SaveChangesAsync();
	}

	[Fact]
	public async Task ListarMeusDitados_AlunoSemTurmas_DeveRetornarListaVazia()
	{
		// Arrange
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Empty(resultado);
	}

	[Fact]
	public async Task ListarMeusDitados_TurmaSemDitados_DeveRetornarListaVazia()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Empty(resultado);
	}

	[Fact]
	public async Task ListarMeusDitados_ComDitadoAtribuido_DeveRetornarDitado()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno, "João Aluno");
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id, "Ortografia Básica");
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(ditado.Id, resultado[0].DitadoId);
		Assert.Equal("Ortografia Básica", resultado[0].Titulo);
		Assert.Equal(dataLimite.Date, resultado[0].DataLimite.Date);
		Assert.False(resultado[0].Atrasado);
		Assert.False(resultado[0].Status.JaTentou);
		Assert.Equal(0, resultado[0].Status.Tentativas);
	}

	[Fact]
	public async Task ListarMeusDitados_ComDitadoAtrasado_DevemarcarComoAtrasado()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(-3); // 3 dias atrás
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.True(resultado[0].Atrasado);
	}

	[Fact]
	public async Task ListarMeusDitados_ComTentativaRealizada_DeveIncluirStatus()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		// Criar tentativa
		var resposta = new RespostaDitado
		{
			DitadoId = ditado.Id,
			AlunoId = aluno.Id,
			DataRealizacao = DateTime.UtcNow.AddDays(-1),
			Nota = 85.5m
		};
		_context.RespostaDitados.Add(resposta);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.True(resultado[0].Status.JaTentou);
		Assert.Equal(1, resultado[0].Status.Tentativas);
		Assert.NotNull(resultado[0].Status.UltimaTentativaEm);
		Assert.Equal(85.5m, resultado[0].Status.MelhorNota);
	}

	[Fact]
	public async Task ListarMeusDitados_ComMultiplasTentativas_DeveRetornarMelhorNota()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		// Criar múltiplas tentativas
		_context.RespostaDitados.AddRange(
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-3), Nota = 60.0m },
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-2), Nota = 75.0m },
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-1), Nota = 90.5m }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(3, resultado[0].Status.Tentativas);
		Assert.Equal(90.5m, resultado[0].Status.MelhorNota);
	}

	[Fact]
	public async Task ListarMeusDitados_DitadoEmMultiplasTurmas_DeveListarTodasTurmas()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma1 = await CriarTurmaAsync(professor.Id, "5º Ano A");
		var turma2 = await CriarTurmaAsync(professor.Id, "5º Ano B");
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma1.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma2.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		await AtribuirDitadoAsync(turma1.Id, ditado.Id, DateTime.UtcNow.AddDays(5));
		await AtribuirDitadoAsync(turma2.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(2, resultado[0].Turmas.Count);
		Assert.Contains(resultado[0].Turmas, t => t.TurmaNome == "5º Ano A");
		Assert.Contains(resultado[0].Turmas, t => t.TurmaNome == "5º Ano B");
	}

	[Fact]
	public async Task ListarMeusDitados_DitadoEmMultiplasTurmasComDatasLimiteDiferentes_DeveUsarDataMaisProxima()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma1 = await CriarTurmaAsync(professor.Id, "5º Ano A");
		var turma2 = await CriarTurmaAsync(professor.Id, "5º Ano B");
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma1.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma2.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimiteMaisProxima = DateTime.UtcNow.AddDays(3);
		var dataLimiteMaisDistante = DateTime.UtcNow.AddDays(10);
		await AtribuirDitadoAsync(turma1.Id, ditado.Id, dataLimiteMaisDistante);
		await AtribuirDitadoAsync(turma2.Id, ditado.Id, dataLimiteMaisProxima);

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(dataLimiteMaisProxima.Date, resultado[0].DataLimite.Date);
	}

	[Fact]
	public async Task ListarMeusDitados_MultiplosDitados_DeveOrdenarPorDataLimite()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado1 = await CriarDitadoAsync(professor.Id, "Ditado 1");
		var ditado2 = await CriarDitadoAsync(professor.Id, "Ditado 2");
		var ditado3 = await CriarDitadoAsync(professor.Id, "Ditado 3");

		await AtribuirDitadoAsync(turma.Id, ditado1.Id, DateTime.UtcNow.AddDays(10));
		await AtribuirDitadoAsync(turma.Id, ditado2.Id, DateTime.UtcNow.AddDays(3));
		await AtribuirDitadoAsync(turma.Id, ditado3.Id, DateTime.UtcNow.AddDays(7));

		// Act
		var resultado = await _alunoService.ListarMeusDitadosAsync(aluno.Id);

		// Assert
		Assert.Equal(3, resultado.Count);
		Assert.Equal("Ditado 2", resultado[0].Titulo); // Mais próximo (3 dias)
		Assert.Equal("Ditado 3", resultado[1].Titulo); // Intermediário (7 dias)
		Assert.Equal("Ditado 1", resultado[2].Titulo); // Mais distante (10 dias)
	}

	[Fact]
	public async Task ListarMinhasTentativas_SemTentativas_DeveRetornarListaVazia()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var ditado = await CriarDitadoAsync(professor.Id);

		// Act
		var resultado = await _alunoService.ListarMinhasTentativasAsync(aluno.Id, ditado.Id);

		// Assert
		Assert.Empty(resultado);
	}

	[Fact]
	public async Task ListarMinhasTentativas_ComUmaTentativa_DeveRetornarDetalhes()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		var segmento1 = new DitadoSegmento { DitadoId = ditado.Id, Ordem = 1, Tipo = TipoSegmento.Lacuna, Conteudo = "gato" };
		var segmento2 = new DitadoSegmento { DitadoId = ditado.Id, Ordem = 2, Tipo = TipoSegmento.Lacuna, Conteudo = "cachorro" };
		_context.DitadoSegmentos.AddRange(segmento1, segmento2);
		await _context.SaveChangesAsync();

		var resposta = new RespostaDitado
		{
			DitadoId = ditado.Id,
			AlunoId = aluno.Id,
			DataRealizacao = DateTime.UtcNow.AddDays(-2),
			Nota = 50.0m
		};
		_context.RespostaDitados.Add(resposta);
		await _context.SaveChangesAsync();

		_context.RespostaSegmentos.AddRange(
			new RespostaSegmento { RespostaDitadoId = resposta.Id, SegmentoId = segmento1.Id, RespostaFornecida = "gato", Correto = true },
			new RespostaSegmento { RespostaDitadoId = resposta.Id, SegmentoId = segmento2.Id, RespostaFornecida = "cachoro", Correto = false, TipoErro = TipoErro.Ortografico }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMinhasTentativasAsync(aluno.Id, ditado.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(resposta.Id, resultado[0].TentativaId);
		Assert.Equal(50.0m, resultado[0].Nota);
		Assert.Equal(2, resultado[0].TotalLacunas);
		Assert.Equal(1, resultado[0].Acertos);
		Assert.Equal(1, resultado[0].Erros);
		Assert.False(resultado[0].Atrasado);
	}

	[Fact]
	public async Task ListarMinhasTentativas_TentativaAposDataLimite_DevemarcarComoAtrasado()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(-5);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		var resposta = new RespostaDitado
		{
			DitadoId = ditado.Id,
			AlunoId = aluno.Id,
			DataRealizacao = DateTime.UtcNow.AddDays(-2), // Após o limite
			Nota = 100.0m
		};
		_context.RespostaDitados.Add(resposta);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMinhasTentativasAsync(aluno.Id, ditado.Id);

		// Assert
		Assert.Single(resultado);
		Assert.True(resultado[0].Atrasado);
	}

	[Fact]
	public async Task ListarMinhasTentativas_MultiplasTentativas_DeveOrdenarPorDataRealizacao()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		_context.RespostaDitados.AddRange(
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-3), Nota = 60.0m },
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-1), Nota = 90.0m },
			new RespostaDitado { DitadoId = ditado.Id, AlunoId = aluno.Id, DataRealizacao = DateTime.UtcNow.AddDays(-2), Nota = 75.0m }
		);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMinhasTentativasAsync(aluno.Id, ditado.Id);

		// Assert
		Assert.Equal(3, resultado.Count);
		Assert.Equal(60.0m, resultado[0].Nota); // Primeira tentativa (-3 dias)
		Assert.Equal(75.0m, resultado[1].Nota); // Segunda tentativa (-2 dias)
		Assert.Equal(90.0m, resultado[2].Nota); // Última tentativa (-1 dia)
	}

	[Fact]
	public async Task ListarMinhasTentativas_AposRemoverAtribuicao_DeveAindaListarTentativas()
	{
		// Arrange
		var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
		var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
		var turma = await CriarTurmaAsync(professor.Id);
		await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

		var ditado = await CriarDitadoAsync(professor.Id);
		var dataLimite = DateTime.UtcNow.AddDays(7);
		await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

		var resposta = new RespostaDitado
		{
			DitadoId = ditado.Id,
			AlunoId = aluno.Id,
			DataRealizacao = DateTime.UtcNow.AddDays(-1),
			Nota = 85.0m
		};
		_context.RespostaDitados.Add(resposta);
		await _context.SaveChangesAsync();

		// Remover atribuição
		var atribuicao = await _context.TurmaDitados.FirstAsync(td => td.TurmaId == turma.Id && td.DitadoId == ditado.Id);
		_context.TurmaDitados.Remove(atribuicao);
		await _context.SaveChangesAsync();

		// Act
		var resultado = await _alunoService.ListarMinhasTentativasAsync(aluno.Id, ditado.Id);

		// Assert
		Assert.Single(resultado);
		Assert.Equal(85.0m, resultado[0].Nota);
	}
}