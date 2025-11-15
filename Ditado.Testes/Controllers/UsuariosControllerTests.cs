using System.Net;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Enums;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class UsuariosControllerTests : TesteIntegracaoBase
{
    private string? _tokenAdmin;

    public UsuariosControllerTests(CustomWebApplicationFactory factory) : base(factory)
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
        
        if (loginResponse?.Token != null)
        {
            _tokenAdmin = loginResponse.Token;
            return _tokenAdmin;
        }

        throw new InvalidOperationException("Não foi possível obter token de admin.");
    }

    [Fact]
    public async Task SolicitarAcesso_SemAutenticacao_DeveCriarUsuarioComAcessoPendente()
    {
        // Arrange
        var request = new CriarUsuarioRequest
        {
            Nome = "João Silva",
            Login = "joao.silva@teste.com",
            Senha = "senha123",
            Matricula = "2024001"
        };

        // Act
        var response = await PostAsyncRaw("/api/usuarios/solicitar-acesso", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SolicitarAcesso_ComEmailDuplicado_DeveRetornarBadRequest()
    {
        // Arrange - Criar primeiro usuário
        var request1 = new CriarUsuarioRequest
        {
            Nome = "Usuário 1",
            Login = "duplicado@teste.com",
            Senha = "senha123"
        };
        await PostAsyncRaw("/api/usuarios/solicitar-acesso", request1);

        // Tentar criar com mesmo email
        var request2 = new CriarUsuarioRequest
        {
            Nome = "Usuário 2",
            Login = "duplicado@teste.com",
            Senha = "senha456"
        };

        // Act
        var response = await PostAsyncRaw("/api/usuarios/solicitar-acesso", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_UsuarioComAcessoPendente_DeveRetornarUnauthorizedComMensagem()
    {
        // Arrange - Solicitar acesso
        var solicitacao = new CriarUsuarioRequest
        {
            Nome = "Maria Santos",
            Login = "maria.santos@teste.com",
            Senha = "senha123"
        };
        await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);

        // Act - Tentar fazer login
        var loginRequest = new LoginRequest
        {
            Login = "maria.santos@teste.com",
            Senha = "senha123"
        };
        var response = await PostAsyncRaw("/api/usuarios/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var conteudo = await response.Content.ReadAsStringAsync();
        conteudo.Should().Contain("não foi aprovado");
    }

    [Fact]
    public async Task ListarSolicitacoesPendentes_ComoAdmin_DeveRetornarLista()
    {
        // Arrange
        var token = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(token);

        // Criar algumas solicitações
        await SolicitarAcessoTeste("pendente1@teste.com", "Pendente 1");
        await SolicitarAcessoTeste("pendente2@teste.com", "Pendente 2");

        // Act
        var response = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");

        // Assert
        response.Should().NotBeNull();
        response!.Should().HaveCountGreaterOrEqualTo(2);
        response.All(u => u.Tipo == "AcessoSolicitado").Should().BeTrue();
        response.All(u => !u.Ativo).Should().BeTrue();
    }

    [Fact]
    public async Task AprovarAcesso_ComoAdmin_DevePermitirQualquerTipo()
    {
        // Arrange
        var token = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(token);

        // Solicitar acesso
        var solicitacaoId = await SolicitarAcessoTeste("novo.admin@teste.com", "Novo Admin");

        // Act - Admin aprovando como Administrador
        var aprovacao = new AprovarAcessoRequest
        {
            NovoTipo = TipoUsuario.Administrador
        };
        var responseAprovacao = await PostAsyncRaw($"/api/usuarios/{solicitacaoId}/aprovar-acesso", aprovacao);

        // Assert
        responseAprovacao.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verificar se usuário está ativo e com tipo correto
        var usuario = await GetAsync<UsuarioResponse>($"/api/usuarios/{solicitacaoId}");
        usuario.Should().NotBeNull();
        usuario!.Tipo.Should().Be("Administrador");
        usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task AprovarAcesso_ComoProfessor_DevePermitirApenasProfessorEAluno()
    {
        // Arrange - Criar e aprovar um professor
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);
        
        var professorId = await SolicitarAcessoTeste("professor@teste.com", "Professor Teste");
        await PostAsyncRaw($"/api/usuarios/{professorId}/aprovar-acesso", new AprovarAcessoRequest { NovoTipo = TipoUsuario.Professor });

        // Login como professor
        RemoverTokenAutorizacao();
        var loginProfessor = new LoginRequest { Login = "professor@teste.com", Senha = "senha123" };
        var professorLogin = await PostAsync<LoginResponse>("/api/usuarios/login", loginProfessor);
        AdicionarTokenAutorizacao(professorLogin!.Token);

        // Solicitar novo acesso
        var alunoId = await SolicitarAcessoTeste("aluno@teste.com", "Aluno Teste");

        // Act - Professor aprovando como Aluno (deve funcionar)
        var aprovacaoAluno = new AprovarAcessoRequest { NovoTipo = TipoUsuario.Aluno };
        var responseAluno = await PostAsyncRaw($"/api/usuarios/{alunoId}/aprovar-acesso", aprovacaoAluno);

        // Assert
        responseAluno.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Tentar aprovar como Administrador (deve falhar)
        var adminId = await SolicitarAcessoTeste("tentativa.admin@teste.com", "Tentativa Admin");
        var aprovacaoAdmin = new AprovarAcessoRequest { NovoTipo = TipoUsuario.Administrador };
        var responseAdmin = await PostAsyncRaw($"/api/usuarios/{adminId}/aprovar-acesso", aprovacaoAdmin);

        // Assert
        responseAdmin.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FluxoCompleto_SolicitarAprovarELogar_DevePermitirAcesso()
    {
        // Arrange
        var emailNovo = "fluxo.completo@teste.com";
        var senhaNova = "senhaSegura123";

        // 1. Solicitar acesso (sem autenticação)
        var solicitacao = new CriarUsuarioRequest
        {
            Nome = "Usuário Fluxo Completo",
            Login = emailNovo,
            Senha = senhaNova,
            Matricula = "2024999"
        };
        var solicitacaoResponse = await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);
        solicitacaoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Admin lista solicitações pendentes
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);
        
        var pendentes = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
        var usuarioPendente = pendentes!.FirstOrDefault(u => u.Login == emailNovo);
        usuarioPendente.Should().NotBeNull();
        usuarioPendente!.Tipo.Should().Be("AcessoSolicitado");
        usuarioPendente.Ativo.Should().BeFalse();

        // 3. Tentar logar ANTES da aprovação (deve falhar)
        RemoverTokenAutorizacao();
        var loginAntes = new LoginRequest { Login = emailNovo, Senha = senhaNova };
        var responseLoginAntes = await PostAsyncRaw("/api/usuarios/login", loginAntes);
        responseLoginAntes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Admin aprova o acesso como Aluno
        AdicionarTokenAutorizacao(tokenAdmin);
        var aprovacao = new AprovarAcessoRequest { NovoTipo = TipoUsuario.Aluno };
        var aprovacaoResponse = await PostAsyncRaw($"/api/usuarios/{usuarioPendente.Id}/aprovar-acesso", aprovacao);
        aprovacaoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Agora consegue logar
        RemoverTokenAutorizacao();
        var loginDepois = new LoginRequest { Login = emailNovo, Senha = senhaNova };
        var responseLoginDepois = await PostAsync<LoginResponse>("/api/usuarios/login", loginDepois);
        
        responseLoginDepois.Should().NotBeNull();
        responseLoginDepois!.Token.Should().NotBeNullOrEmpty();
        responseLoginDepois.Usuario.Should().NotBeNull();
        responseLoginDepois.Usuario.Tipo.Should().Be("Aluno");
        responseLoginDepois.Usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public async Task AprovarAcesso_UsuarioQueNaoEstaPendente_DeveRetornarErro()
    {
        // Arrange - Criar usuário já aprovado via Admin
        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        var usuarioNormal = new CriarUsuarioRequest
        {
            Nome = "Usuário Normal",
            Login = "normal@teste.com",
            Senha = "senha123",
            Tipo = TipoUsuario.Aluno
        };
        var usuarioCriado = await PostAsync<UsuarioResponse>("/api/usuarios", usuarioNormal);

        // Act - Tentar aprovar um usuário que não está pendente
        var aprovacao = new AprovarAcessoRequest { NovoTipo = TipoUsuario.Professor };
        var response = await PostAsyncRaw($"/api/usuarios/{usuarioCriado!.Id}/aprovar-acesso", aprovacao);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var conteudo = await response.Content.ReadAsStringAsync();
        conteudo.Should().Contain("não está pendente de aprovação");
    }

    [Fact]
    public async Task ListarSolicitacoesPendentes_SemAutenticacao_DeveRetornarUnauthorized()
    {
        // Act
        var response = await GetAsyncRaw("/api/usuarios/solicitacoes-pendentes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Métodos auxiliares
    private async Task<int> SolicitarAcessoTeste(string email, string nome)
    {
        // Remove token temporariamente para solicitar acesso sem auth
        var tokenAtual = Client.DefaultRequestHeaders.Authorization?.Parameter;
        RemoverTokenAutorizacao();

        var solicitacao = new CriarUsuarioRequest
        {
            Nome = nome,
            Login = email,
            Senha = "senha123"
        };

        await PostAsyncRaw("/api/usuarios/solicitar-acesso", solicitacao);

        // Restaura token se havia
        if (!string.IsNullOrEmpty(tokenAtual))
            AdicionarTokenAutorizacao(tokenAtual);

        // Busca o usuário criado para retornar o ID
        var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios/solicitacoes-pendentes");
        var usuario = usuarios!.First(u => u.Login == email);
        return usuario.Id;
    }
}