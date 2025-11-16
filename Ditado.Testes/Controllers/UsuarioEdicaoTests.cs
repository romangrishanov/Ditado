using System.Net;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Enums;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class UsuarioEdicaoTests : TesteIntegracaoBase
{
	private string? _tokenAdmin;
	private string? _tokenProfessor;
	private int _professorId;
	private string? _tokenAluno;
	private int _alunoId;
	private bool _professorCriado;
	private bool _alunoCriado;

	public UsuarioEdicaoTests(CustomWebApplicationFactory factory) : base(factory)
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

		_professorId = await SolicitarEAprovarUsuarioSeNaoExiste("professor.teste@teste.com", "Professor Teste", TipoUsuario.Professor);

		RemoverTokenAutorizacao();
		var login = new LoginRequest { Login = "professor.teste@teste.com", Senha = "senha123" };
		var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", login);
		_tokenProfessor = loginResponse!.Token;
		_professorCriado = true;

		return (_tokenProfessor, _professorId);
	}

	private async Task<(string token, int id)> CriarELogarAlunoAsync()
	{
		if (_alunoCriado && _tokenAluno != null)
			return (_tokenAluno, _alunoId);

		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		_alunoId = await SolicitarEAprovarUsuarioSeNaoExiste("aluno.teste@teste.com", "Aluno Teste", TipoUsuario.Aluno);

		RemoverTokenAutorizacao();
		var login = new LoginRequest { Login = "aluno.teste@teste.com", Senha = "senha123" };
		var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", login);
		_tokenAluno = loginResponse!.Token;
		_alunoCriado = true;

		return (_tokenAluno, _alunoId);
	}

	#region Testes de Edição Própria

	[Fact]
	public async Task EdicaoPropria_AlterarNome_DevePermitir()
	{
		// Arrange
		var (token, id) = await CriarELogarAlunoAsync();
		AdicionarTokenAutorizacao(token);

		var request = new AtualizarUsuarioRequest
		{
			Nome = "Novo Nome Aluno"
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{id}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Nome.Should().Be("Novo Nome Aluno");
	}

	[Fact]
	public async Task EdicaoPropria_AlterarMatricula_DevePermitir()
	{
		// Arrange
		var (token, id) = await CriarELogarAlunoAsync();
		AdicionarTokenAutorizacao(token);

		var request = new AtualizarUsuarioRequest
		{
			Matricula = "MAT2024"
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{id}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Matricula.Should().Be("MAT2024");
	}

	[Fact]
	public async Task EdicaoPropria_AlterarSenha_DevePermitir()
	{
		// Arrange
		var emailUnico = $"aluno.senha.{Guid.NewGuid().ToString("N")[..8]}@teste.com";

		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario(emailUnico, "Aluno Senha", TipoUsuario.Aluno);

		RemoverTokenAutorizacao();
		var login = new LoginRequest { Login = emailUnico, Senha = "senha123" };
		var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", login);
		AdicionarTokenAutorizacao(loginResponse!.Token);

		var request = new AtualizarUsuarioRequest
		{
			SenhaAtual = "senha123",
			SenhaNova = "novaSenha456"
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// Tentar logar com nova senha
		RemoverTokenAutorizacao();
		var loginNovo = new LoginRequest { Login = emailUnico, Senha = "novaSenha456" };
		var loginNovoResponse = await PostAsync<LoginResponse>("/api/usuarios/login", loginNovo);
		loginNovoResponse.Should().NotBeNull();
		loginNovoResponse!.Token.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task EdicaoPropria_TentarAlterarProprioTipo_DeveNegar()
	{
		// Arrange
		var (token, id) = await CriarELogarAlunoAsync();
		AdicionarTokenAutorizacao(token);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Administrador
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{id}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("não pode alterar seu próprio tipo");
	}

	[Fact]
	public async Task EdicaoPropria_TentarAlterarProprioAtivo_DeveNegar()
	{
		// Arrange
		var (token, id) = await CriarELogarAlunoAsync();
		AdicionarTokenAutorizacao(token);

		var request = new AtualizarUsuarioRequest
		{
			Ativo = false
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{id}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("não pode alterar seu próprio status ativo");
	}

	#endregion

	#region Testes de Alteração de Ativo

	[Fact]
	public async Task AlterarAtivo_AlunoTentaDesativarOutroAluno_DeveNegar()
	{
		// Arrange
		var (token, _) = await CriarELogarAlunoAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var outroAlunoId = await SolicitarEAprovarUsuario("outro.aluno@teste.com", "Outro Aluno", TipoUsuario.Aluno);

		AdicionarTokenAutorizacao(token);

		var request = new AtualizarUsuarioRequest
		{
			Ativo = false
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{outroAlunoId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("Alunos não podem alterar status");
	}

	[Fact]
	public async Task AlterarAtivo_ProfessorDesativaAluno_DevePermitir()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.desativar@teste.com", "Aluno Para Desativar", TipoUsuario.Aluno);

		AdicionarTokenAutorizacao(tokenProf);

		var request = new AtualizarUsuarioRequest
		{
			Ativo = false
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Ativo.Should().BeFalse();
	}

	[Fact]
	public async Task AlterarAtivo_ProfessorTentaDesativarOutroProfessor_DeveNegar()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var outroProfId = await SolicitarEAprovarUsuario("outro.prof@teste.com", "Outro Professor", TipoUsuario.Professor);

		AdicionarTokenAutorizacao(tokenProf);

		var request = new AtualizarUsuarioRequest
		{
			Ativo = false
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{outroProfId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("Professores só podem alterar status de Alunos");
	}

	[Fact]
	public async Task AlterarAtivo_AdminDesativaQualquerUsuario_DevePermitir()
	{
		// Arrange
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var professorId = await SolicitarEAprovarUsuario("prof.desativar@teste.com", "Professor Desativar", TipoUsuario.Professor);

		var request = new AtualizarUsuarioRequest
		{
			Ativo = false
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{professorId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Ativo.Should().BeFalse();
	}

	#endregion

	#region Testes de Alteração de Tipo - Aumento

	[Fact]
	public async Task AlterarTipo_ProfessorPromoveAlunoParaProfessor_DevePermitir()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.promover@teste.com", "Aluno Promover", TipoUsuario.Aluno);

		AdicionarTokenAutorizacao(tokenProf);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Professor
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Tipo.Should().Be("Professor");
	}

	[Fact]
	public async Task AlterarTipo_ProfessorTentaPromoverAlunoParaAdmin_DeveNegar()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.admin@teste.com", "Aluno Admin", TipoUsuario.Aluno);

		AdicionarTokenAutorizacao(tokenProf);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Administrador
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("não podem promover usuários a Administrador");
	}

	[Fact]
	public async Task AlterarTipo_AdminPromoveAlunoParaAdmin_DevePermitir()
	{
		// Arrange
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.virar.admin@teste.com", "Aluno Virar Admin", TipoUsuario.Aluno);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Administrador
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Tipo.Should().Be("Administrador");
	}

	[Fact]
	public async Task AlterarTipo_ProfessorPromoveAcessoSolicitadoParaAluno_DevePermitir()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();

		// Criar usuário com AcessoSolicitado
		RemoverTokenAutorizacao();
		var solicitacao = new CriarUsuarioRequest
		{
			Nome = "Solicitante Promover",
			Login = "solicitante.promover@teste.com",
			Senha = "senha123"
		};
		await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);

		AdicionarTokenAutorizacao(tokenProf);
		var pendentes = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
		var solicitanteId = pendentes!.First(u => u.Login == "solicitante.promover@teste.com").Id;

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Aluno,
			Ativo = true
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{solicitanteId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Tipo.Should().Be("Aluno");
	}

	#endregion

	#region Testes de Alteração de Tipo - Redução

	[Fact]
	public async Task AlterarTipo_ProfessorTentaRebaixarProfessorParaAluno_DeveNegar()
	{
		// Arrange
		var (tokenProf, _) = await CriarELogarProfessorAsync();
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var outroProfId = await SolicitarEAprovarUsuario("prof.rebaixar@teste.com", "Prof Rebaixar", TipoUsuario.Professor);

		AdicionarTokenAutorizacao(tokenProf);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Aluno
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{outroProfId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("Apenas administradores podem reduzir tipo");
	}

	[Fact]
	public async Task AlterarTipo_AdminRebaixaProfessorParaAluno_DevePermitir()
	{
		// Arrange
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var professorId = await SolicitarEAprovarUsuario("prof.virar.aluno@teste.com", "Prof Virar Aluno", TipoUsuario.Professor);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.Aluno
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{professorId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Tipo.Should().Be("Aluno");
	}

	[Fact]
	public async Task AlterarTipo_AdminTentaRebaixarParaAcessoSolicitado_DeveNegar()
	{
		// Arrange
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.rebaixar.solicitado@teste.com", "Aluno Rebaixar", TipoUsuario.Aluno);

		var request = new AtualizarUsuarioRequest
		{
			Tipo = TipoUsuario.AcessoSolicitado
		};

		// Act
		var response = await PostAsyncRaw($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var conteudo = await response.Content.ReadAsStringAsync();
		conteudo.Should().Contain("Não é possível alterar usuário para tipo 'AcessoSolicitado'");
	}

	#endregion

	#region Testes Combinados

	[Fact]
	public async Task AlterarUsuario_AdminAlteraNomeTipoEAtivo_DevePermitirTudo()
	{
		// Arrange
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);
		var alunoId = await SolicitarEAprovarUsuario("aluno.completo@teste.com", "Aluno Completo", TipoUsuario.Aluno);

		var request = new AtualizarUsuarioRequest
		{
			Nome = "Nome Alterado Admin",
			Tipo = TipoUsuario.Professor,
			Ativo = false
		};

		// Act
		var response = await PostAsync<UsuarioResponse>($"/api/usuarios/{alunoId}", request, "PUT");

		// Assert
		response.Should().NotBeNull();
		response!.Nome.Should().Be("Nome Alterado Admin");
		response.Tipo.Should().Be("Professor");
		response.Ativo.Should().BeFalse();
	}

	#endregion

	// Métodos auxiliares
	private async Task<int> SolicitarEAprovarUsuarioSeNaoExiste(string email, string nome, TipoUsuario tipo)
	{
		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		// Verifica se já existe nas solicitações pendentes
		var pendentes = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
		var usuarioPendente = pendentes!.FirstOrDefault(u => u.Login == email);

		if (usuarioPendente != null)
		{
			// Aprova se estiver pendente
			await PostAsyncRaw($"/api/usuarios/{usuarioPendente.Id}/aprovar-acesso", new AprovarAcessoRequest { NovoTipo = tipo });
			return usuarioPendente.Id;
		}

		// Verifica se já existe aprovado (tenta fazer login)
		try
		{
			RemoverTokenAutorizacao();
			var loginTest = new LoginRequest { Login = email, Senha = "senha123" };
			var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", loginTest);

			if (loginResponse != null)
			{
				// Usuário já existe e está aprovado - busca o ID
				AdicionarTokenAutorizacao(tokenAdmin);
				var todosUsuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
				var usuarioExistente = todosUsuarios!.First(u => u.Login == email);
				return usuarioExistente.Id;
			}
		}
		catch
		{
			// Usuário não existe ou está inativo - segue para criar
		}

		// Se não existe, cria novo
		RemoverTokenAutorizacao();
		var solicitacao = new CriarUsuarioRequest
		{
			Nome = nome,
			Login = email,
			Senha = "senha123"
		};

		await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);

		AdicionarTokenAutorizacao(tokenAdmin);
		var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
		var usuario = usuarios!.First(u => u.Login == email);

		await PostAsyncRaw($"/api/usuarios/{usuario.Id}/aprovar-acesso", new AprovarAcessoRequest { NovoTipo = tipo });

		return usuario.Id;
	}

	private async Task<int> SolicitarEAprovarUsuario(string email, string nome, TipoUsuario tipo)
	{
		var tokenAtual = Client.DefaultRequestHeaders.Authorization?.Parameter;
		RemoverTokenAutorizacao();

		var solicitacao = new CriarUsuarioRequest
		{
			Nome = nome,
			Login = email,
			Senha = "senha123"
		};

		await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);

		var tokenAdmin = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(tokenAdmin);

		var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
		var usuario = usuarios!.FirstOrDefault(u => u.Login == email);

		if (usuario == null)
			throw new InvalidOperationException($"Usuário {email} não encontrado nas solicitações pendentes");

		await PostAsyncRaw($"/api/usuarios/{usuario.Id}/aprovar-acesso", new AprovarAcessoRequest { NovoTipo = tipo });

		if (!string.IsNullOrEmpty(tokenAtual))
			AdicionarTokenAutorizacao(tokenAtual);

		return usuario.Id;
	}

	private async Task<T?> PostAsync<T>(string url, object body, string method)
	{
		var request = new HttpRequestMessage(new HttpMethod(method), url)
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

	private async Task<HttpResponseMessage> PostAsyncRaw(string url, object body, string method)
	{
		var request = new HttpRequestMessage(new HttpMethod(method), url)
		{
			Content = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(body),
				System.Text.Encoding.UTF8,
				"application/json")
		};

		return await Client.SendAsync(request);
	}
}