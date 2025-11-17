using System.Net;
using Ditado.Aplicacao.DTOs.Turmas;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Enums;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class TurmasControllerTests : TesteIntegracaoBase
{
    private string? _tokenAdmin;
    private string? _tokenProfessor;
    private int _professorId;
    private bool _professorCriado;

    public TurmasControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<string> ObterTokenAdminAsync()
    {
        if (_tokenAdmin != null)
            return _tokenAdmin;

        var loginRequest = new LoginRequest
        {
            Login = "admin@admin.com",
            Senha = "admin"
        };

        var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", loginRequest);
        _tokenAdmin = loginResponse!.Token;
        return _tokenAdmin;
    }

	private async Task<(string token, int id)> CriarELogarProfessorAsync()
	{
		if (_professorCriado && _tokenProfessor != null)
			return (_tokenProfessor, _professorId);

		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		// Buscar se já existe
		var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
		var professorExistente = usuarios!.FirstOrDefault(u => u.Login == "professor.turmas@teste.com");

		if (professorExistente != null)
		{
			_professorId = professorExistente.Id;
		}
		else
		{
			// Criar novo
			var request = new CriarUsuarioRequest
			{
				Nome = "Professor Turmas",
				Login = "professor.turmas@teste.com",
				Senha = "senha123",
				Tipo = TipoUsuario.Professor
			};

			var usuarioCriado = await PostAsync<UsuarioResponse>("/api/usuarios", request);
			_professorId = usuarioCriado!.Id;
		}

		// Fazer login
		RemoverTokenAutorizacao();
		var login = new LoginRequest { Login = "professor.turmas@teste.com", Senha = "senha123" };
		var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", login);
		_tokenProfessor = loginResponse!.Token;
		_professorCriado = true;

		return (_tokenProfessor, _professorId);
	}

    #region Criação de Turmas

    [Fact]
    public async Task CriarTurma_ComoProfessor_SemAlunos_DevePermitir()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        var request = new CriarTurmaRequest
        {
            Nome = "5º Ano A",
            Serie = 5,
            AnoLetivo = 2024,
            Semestre = "1º Semestre",
            Descricao = "Turma da manhã",
            ProfessorResponsavelId = profId,
            AlunosIds = new List<int>()
        };

        // Act
        var response = await PostAsync<TurmaResponse>("/api/turmas", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.Nome.Should().Be("5º Ano A");
        response.Serie.Should().Be(5);
        response.AnoLetivo.Should().Be(2024);
        response.Semestre.Should().Be("1º Semestre");
        response.ProfessorResponsavelId.Should().Be(profId);
        response.TotalAlunos.Should().Be(0);
        response.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task CriarTurma_ComoAdmin_DevePermitir()
    {
        // Arrange
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        // Admin pode ser professor responsável
        var adminUser = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
        var admin = adminUser!.First(u => u.Login == "admin@admin.com");

        var request = new CriarTurmaRequest
        {
            Nome = "6º Ano B",
            Serie = 6,
            AnoLetivo = 2024,
            ProfessorResponsavelId = admin.Id,
            AlunosIds = new List<int>()
        };

        // Act
        var response = await PostAsync<TurmaResponse>("/api/turmas", request);

        // Assert
        response.Should().NotBeNull();
        response!.ProfessorResponsavelId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task CriarTurma_ComAlunos_DeveAssociarCorretamente()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        var aluno1Id = await CriarAlunoAsync("aluno.turma1@teste.com", "Aluno Turma 1");
        var aluno2Id = await CriarAlunoAsync("aluno.turma2@teste.com", "Aluno Turma 2");
        var aluno3Id = await CriarAlunoAsync("aluno.turma3@teste.com", "Aluno Turma 3");

        AdicionarTokenAutorizacao(tokenProf);

        var request = new CriarTurmaRequest
        {
            Nome = "7º Ano C",
            Serie = 7,
            AnoLetivo = 2024,
            ProfessorResponsavelId = profId,
            AlunosIds = new List<int> { aluno1Id, aluno2Id, aluno3Id }
        };

        // Act
        var response = await PostAsync<TurmaResponse>("/api/turmas", request);

        // Assert
        response.Should().NotBeNull();
        response!.TotalAlunos.Should().Be(3);
        response.Alunos.Should().HaveCount(3);
        response.Alunos.Select(a => a.Id).Should().Contain(new[] { aluno1Id, aluno2Id, aluno3Id });
    }

    [Fact]
    public async Task CriarTurma_ComProfessorInvalido_DeveRetornarErro()
    {
        // Arrange
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        var request = new CriarTurmaRequest
        {
            Nome = "8º Ano D",
            Serie = 8,
            AnoLetivo = 2024,
            ProfessorResponsavelId = 99999, // ID inválido
            AlunosIds = new List<int>()
        };

        // Act
        var response = await PostAsyncRaw("/api/turmas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var conteudo = await response.Content.ReadAsStringAsync();
        conteudo.Should().Contain("Professor responsável inválido");
    }

    [Fact]
    public async Task CriarTurma_ComAlunoInvalido_DeveRetornarErro()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        var request = new CriarTurmaRequest
        {
            Nome = "9º Ano E",
            Serie = 9,
            AnoLetivo = 2024,
            ProfessorResponsavelId = profId,
            AlunosIds = new List<int> { 99999 } // Aluno inválido
        };

        // Act
        var response = await PostAsyncRaw("/api/turmas", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var conteudo = await response.Content.ReadAsStringAsync();
        conteudo.Should().Contain("alunos são inválidos");
    }

    #endregion

    #region Obter e Listar Turmas

    [Fact]
    public async Task ObterTurma_ComIdValido_DeveRetornarTurmaCompleta()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        var turmaId = await CriarTurmaTeste(profId);

        // Act
        var response = await GetAsync<TurmaResponse>($"/api/turmas/{turmaId}");

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().Be(turmaId);
        response.ProfessorResponsavelNome.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ObterTurma_ComIdInexistente_DeveRetornarNotFound()
    {
        // Arrange
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        // Act
        var response = await GetAsyncRaw("/api/turmas/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListarTurmas_DeveRetornarTodasAtivas()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        await CriarTurmaTeste(profId);
        await CriarTurmaTeste(profId);

        // Act
        var response = await GetAsync<List<TurmaResponse>>("/api/turmas");

        // Assert
        response.Should().NotBeNull();
        response!.Should().HaveCountGreaterOrEqualTo(2);
        response.All(t => t.Ativo).Should().BeTrue();
    }

    [Fact]
    public async Task ListarTurmasPorProfessor_DeveRetornarApenasSuasTurmas()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        await CriarTurmaTeste(profId);
        await CriarTurmaTeste(profId);

        // Act
        var response = await GetAsync<List<TurmaResponse>>($"/api/turmas/professor/{profId}");

        // Assert
        response.Should().NotBeNull();
        response!.Should().HaveCountGreaterOrEqualTo(2);
        response.All(t => t.ProfessorResponsavelId == profId).Should().BeTrue();
    }

    #endregion

    #region Atualizar Turmas

    [Fact]
    public async Task AtualizarTurma_AdicionarAlunos_DeveAtualizarCorretamente()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        var turmaId = await CriarTurmaTeste(profId);
        var aluno1Id = await CriarAlunoAsync("aluno.add1@teste.com", "Aluno Add 1");
        var aluno2Id = await CriarAlunoAsync("aluno.add2@teste.com", "Aluno Add 2");

        AdicionarTokenAutorizacao(tokenProf);

        var request = new AtualizarTurmaRequest
        {
            AlunosIds = new List<int> { aluno1Id, aluno2Id }
        };

        // Act
        var response = await PutAsync<TurmaResponse>($"/api/turmas/{turmaId}", request);

        // Assert
        response.Should().NotBeNull();
        response!.TotalAlunos.Should().Be(2);
        response.Alunos.Select(a => a.Id).Should().Contain(new[] { aluno1Id, aluno2Id });
    }

    [Fact]
    public async Task AtualizarTurma_RemoverAlunos_DevePermitir()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        var aluno1Id = await CriarAlunoAsync("aluno.rem1@teste.com", "Aluno Rem 1");
        var aluno2Id = await CriarAlunoAsync("aluno.rem2@teste.com", "Aluno Rem 2");

        AdicionarTokenAutorizacao(tokenProf);

        var turmaId = await CriarTurmaComAlunosAsync(profId, new List<int> { aluno1Id, aluno2Id });

        var request = new AtualizarTurmaRequest
        {
            AlunosIds = new List<int> { aluno1Id } // Remove aluno2
        };

        // Act
        var response = await PutAsync<TurmaResponse>($"/api/turmas/{turmaId}", request);

        // Assert
        response.Should().NotBeNull();
        response!.TotalAlunos.Should().Be(1);
        response.Alunos.Select(a => a.Id).Should().Contain(aluno1Id);
        response.Alunos.Select(a => a.Id).Should().NotContain(aluno2Id);
    }

    [Fact]
    public async Task AtualizarTurma_RemoverTodosAlunos_DevePermitir()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        var aluno1Id = await CriarAlunoAsync("aluno.limpar1@teste.com", "Aluno Limpar 1");

        AdicionarTokenAutorizacao(tokenProf);

        var turmaId = await CriarTurmaComAlunosAsync(profId, new List<int> { aluno1Id });

        var request = new AtualizarTurmaRequest
        {
            AlunosIds = new List<int>() // Lista vazia = remove todos
        };

        // Act
        var response = await PutAsync<TurmaResponse>($"/api/turmas/{turmaId}", request);

        // Assert
        response.Should().NotBeNull();
        response!.TotalAlunos.Should().Be(0);
    }

    [Fact]
    public async Task AtualizarTurma_TrocarProfessor_DevePermitir()
    {
        // Arrange
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        var (_, prof1Id) = await CriarELogarProfessorAsync();
        var turmaId = await CriarTurmaTeste(prof1Id);

        var prof2Id = await CriarProfessorAsync("professor2@teste.com", "Professor 2");

        var request = new AtualizarTurmaRequest
        {
            ProfessorResponsavelId = prof2Id
        };

        // Act
        var response = await PutAsync<TurmaResponse>($"/api/turmas/{turmaId}", request);

        // Assert
        response.Should().NotBeNull();
        response!.ProfessorResponsavelId.Should().Be(prof2Id);
    }

    [Fact]
    public async Task AtualizarTurma_DesativarTurma_DevePermitir()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        var turmaId = await CriarTurmaTeste(profId);

        var request = new AtualizarTurmaRequest
        {
            Ativo = false
        };

        // Act
        var response = await PutAsync<TurmaResponse>($"/api/turmas/{turmaId}", request);

        // Assert
        response.Should().NotBeNull();
        response!.Ativo.Should().BeFalse();
    }

    #endregion

    #region Excluir Turmas

    [Fact]
    public async Task ExcluirTurma_ComoAdmin_DevePermitir()
    {
        // Arrange
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        var (_, profId) = await CriarELogarProfessorAsync();
        var turmaId = await CriarTurmaTeste(profId);

        // Act
        var response = await ExcluirAsyncRaw($"/api/turmas/{turmaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getTurma = await GetAsyncRaw($"/api/turmas/{turmaId}");
        getTurma.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExcluirTurma_ProfessorExcluiSuaPropriaTurma_DevePermitir()
    {
        // Arrange
        var (tokenProf, profId) = await CriarELogarProfessorAsync();
        AdicionarTokenAutorizacao(tokenProf);

        var turmaId = await CriarTurmaTeste(profId);

        // Act
        var response = await ExcluirAsyncRaw($"/api/turmas/{turmaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ExcluirTurma_ProfessorTentaExcluirTurmaDeOutro_DeveNegar()
    {
        // Arrange
        var (tokenProf1, profId1) = await CriarELogarProfessorAsync();
        
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);
        
        var prof2Id = await CriarProfessorAsync("professor.outro@teste.com", "Outro Professor");
        var turmaId = await CriarTurmaTeste(prof2Id);

        AdicionarTokenAutorizacao(tokenProf1);

        // Act
        var response = await ExcluirAsyncRaw($"/api/turmas/{turmaId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

	#endregion

	#region Gerenciar Alunos

	[Fact]
	public async Task AdicionarAluno_ComoProfessor_DevePermitir()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var turmaId = await CriarTurmaTeste(profId);
		var alunoId = await CriarAlunoAsync("aluno.adicionar@teste.com", "Aluno Adicionar");

		// Act
		var response = await PostAsync<TurmaResponse>($"/api/turmas/{turmaId}/alunos/{alunoId}", new { });

		// Assert
		response.Should().NotBeNull();
		response!.TotalAlunos.Should().Be(1);
		response.Alunos.Should().Contain(a => a.Id == alunoId);
	}

	[Fact]
	public async Task AdicionarAluno_AlunoJaMatriculado_DeveRetornarErro()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var alunoId = await CriarAlunoAsync("aluno.duplicado@teste.com", "Aluno Duplicado");
		var turmaId = await CriarTurmaComAlunosAsync(profId, new List<int> { alunoId });

		// Act
		var response = await PostAsyncRaw($"/api/turmas/{turmaId}/alunos/{alunoId}", new { });

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("já está matriculado");
	}

	[Fact]
	public async Task AdicionarAluno_IdInvalido_DeveRetornarErro()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var turmaId = await CriarTurmaTeste(profId);

		// Act
		var response = await PostAsyncRaw($"/api/turmas/{turmaId}/alunos/99999", new { });

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("Aluno não encontrado");
	}

	[Fact]
	public async Task RemoverAluno_ComoProfessor_DevePermitir()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var aluno1Id = await CriarAlunoAsync("aluno.remover1@teste.com", "Aluno Remover 1");
		var aluno2Id = await CriarAlunoAsync("aluno.remover2@teste.com", "Aluno Remover 2");
		var turmaId = await CriarTurmaComAlunosAsync(profId, new List<int> { aluno1Id, aluno2Id });

		// Act
		var response = await DeleteAsync($"/api/turmas/{turmaId}/alunos/{aluno1Id}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var turmaAtualizada = await GetAsync<TurmaResponse>($"/api/turmas/{turmaId}");
		turmaAtualizada!.TotalAlunos.Should().Be(1);
		turmaAtualizada.Alunos.Should().NotContain(a => a.Id == aluno1Id);
		turmaAtualizada.Alunos.Should().Contain(a => a.Id == aluno2Id);
	}

	[Fact]
	public async Task RemoverAluno_AlunoNaoEstaNaTurma_DeveRetornarErro()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var turmaId = await CriarTurmaTeste(profId);
		var alunoId = await CriarAlunoAsync("aluno.naopertence@teste.com", "Aluno Não Pertence");

		// Act
		var response = await DeleteAsync($"/api/turmas/{turmaId}/alunos/{alunoId}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("não está matriculado");
	}

	[Fact]
	public async Task RemoverAluno_RemoverUltimoAluno_DevePermitir()
	{
		// Arrange
		var (tokenProf, profId) = await CriarELogarProfessorAsync();
		AdicionarTokenAutorizacao(tokenProf);

		var alunoId = await CriarAlunoAsync("aluno.ultimo@teste.com", "Aluno Último");
		var turmaId = await CriarTurmaComAlunosAsync(profId, new List<int> { alunoId });

		// Act
		var response = await DeleteAsync($"/api/turmas/{turmaId}/alunos/{alunoId}");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var turmaAtualizada = await GetAsync<TurmaResponse>($"/api/turmas/{turmaId}");
		turmaAtualizada!.TotalAlunos.Should().Be(0);
	}

	#endregion

	// Métodos auxiliares
	private async Task<int> CriarTurmaTeste(int professorId)
	{
		// Garantir que há um token ativo
		var tokenAtual = Client.DefaultRequestHeaders.Authorization?.Parameter;
		if (string.IsNullOrEmpty(tokenAtual))
		{
			var tokenAdmin = await ObterTokenAdminAsync();
			AdicionarTokenAutorizacao(tokenAdmin);
		}

		var request = new CriarTurmaRequest
		{
			Nome = $"Turma Teste {Guid.NewGuid().ToString("N")[..8]}",
			Serie = 5,
			AnoLetivo = 2024,
			Semestre = "1º Semestre",
			ProfessorResponsavelId = professorId,
			AlunosIds = new List<int>()
		};

		var response = await PostAsync<TurmaResponse>("/api/turmas", request);
		return response!.Id;
	}

	private async Task<int> CriarTurmaComAlunosAsync(int professorId, List<int> alunosIds)
	{
		// Garantir que há um token ativo
		var tokenAtual = Client.DefaultRequestHeaders.Authorization?.Parameter;
		if (string.IsNullOrEmpty(tokenAtual))
		{
			var tokenAdmin = await ObterTokenAdminAsync();
			AdicionarTokenAutorizacao(tokenAdmin);
		}

		var request = new CriarTurmaRequest
		{
			Nome = $"Turma Com Alunos {Guid.NewGuid().ToString("N")[..8]}",
			Serie = 5,
			AnoLetivo = 2024,
			ProfessorResponsavelId = professorId,
			AlunosIds = alunosIds
		};

		var response = await PostAsync<TurmaResponse>("/api/turmas", request);
		return response!.Id;
	}

	private async Task<int> CriarProfessorAsync(string email, string nome)
	{
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		// Simplificar: usar POST /api/usuarios direto
		var request = new CriarUsuarioRequest
		{
			Nome = nome,
			Login = email,
			Senha = "senha123",
			Tipo = TipoUsuario.Professor
		};

		try
		{
			var usuarioCriado = await PostAsync<UsuarioResponse>("/api/usuarios", request);
			return usuarioCriado!.Id;
		}
		catch
		{
			// Já existe - buscar ID
			var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
			var professorExistente = usuarios!.First(u => u.Login == email);
			return professorExistente.Id;
		}
	}

	private async Task<int> CriarAlunoAsync(string email, string nome)
	{
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		// Simplificar: usar POST /api/usuarios direto
		var request = new CriarUsuarioRequest
		{
			Nome = nome,
			Login = email,
			Senha = "senha123",
			Tipo = TipoUsuario.Aluno
		};

		try
		{
			var usuarioCriado = await PostAsync<UsuarioResponse>("/api/usuarios", request);
			return usuarioCriado!.Id;
		}
		catch
		{
			// Já existe - buscar ID
			var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
			var alunoExistente = usuarios!.First(u => u.Login == email);
			return alunoExistente.Id;
		}
	}

	private async Task<T?> PutAsync<T>(string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json")
        };

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    private async Task<HttpResponseMessage> ExcluirAsyncRaw(string url)
    {
        return await Client.DeleteAsync(url);
    }
}