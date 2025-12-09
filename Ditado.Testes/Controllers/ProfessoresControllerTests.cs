using System.Net;
using Ditado.Aplicacao.DTOs.Professores;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Enums;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class ProfessoresControllerTests : TesteIntegracaoBase
{
    private string? _tokenProfessor;
    private int _professorId;

    public ProfessoresControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<(string token, int id)> ObterTokenProfessorAsync()
    {
        if (_tokenProfessor != null)
            return (_tokenProfessor, _professorId);

        var tokenAdmin = await ObterTokenAdminAsync();
        AdicionarTokenAutorizacao(tokenAdmin);

        // Tentar criar o professor
        var request = new CriarUsuarioRequest
        {
            Nome = "Professor Testes",
            Login = "professor.testes@teste.com",
            Senha = "senha123",
            Tipo = TipoUsuario.Professor
        };

        var usuarioCriado = await PostAsync<UsuarioResponse>("/api/usuarios", request);

        // Se falhou ao criar (já existe), buscar na lista
        if (usuarioCriado == null)
        {
            var usuarios = await GetAsync<List<UsuarioResponse>>("/api/usuarios");
            usuarioCriado = usuarios?.FirstOrDefault(u => u.Login == "professor.testes@teste.com");

            if (usuarioCriado == null)
                throw new InvalidOperationException("Não foi possível criar ou encontrar o professor de testes.");
        }

        _professorId = usuarioCriado.Id;

        // Fazer login com o professor
        RemoverTokenAutorizacao();
        var login = new LoginRequest { Login = "professor.testes@teste.com", Senha = "senha123" };
        var loginResponse = await PostAsync<LoginResponse>("/api/usuarios/login", login);

        if (loginResponse?.Token == null)
            throw new InvalidOperationException("Não foi possível fazer login com o professor de testes.");

        _tokenProfessor = loginResponse.Token;

        return (_tokenProfessor, _professorId);
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_SemAutenticacao_DeveRetornarUnauthorized()
    {
        // Arrange
        RemoverTokenAutorizacao();

        // Act
        var response = await GetAsyncRaw("/api/professores/meus-ditados-atribuidos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_ProfessorSemAtribuicoes_DeveRetornarListaVazia()
    {
        // Arrange
        var (token, _) = await ObterTokenProfessorAsync();
        AdicionarTokenAutorizacao(token);

        // Act
        var response = await GetAsync<List<DitadoAtribuidoResumoDto>>("/api/professores/meus-ditados-atribuidos");

        // Assert
        response.Should().NotBeNull();
        response.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_SemAutenticacao_DeveRetornarUnauthorized()
    {
        // Arrange
        RemoverTokenAutorizacao();

        // Act
        var response = await GetAsyncRaw("/api/professores/turmas/1/ditados/1/resultados");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_AtribuicaoInexistente_DeveRetornarNotFound()
    {
        // Arrange
        var (token, _) = await ObterTokenProfessorAsync();
        AdicionarTokenAutorizacao(token);

        // Act
        var response = await GetAsyncRaw("/api/professores/turmas/9999/ditados/9999/resultados");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}