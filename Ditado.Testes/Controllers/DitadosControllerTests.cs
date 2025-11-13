using System.Net;
using Ditado.Aplicacao.DTOs;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class DitadosControllerTests : TesteIntegracaoBase
{
    public DitadosControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CriarDitado_ComDadosValidos_DeveRetornarSucesso()
    {
        // Arrange
        var audioBase64 = "data:audio/mpeg;base64,SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk="; // Audio mock
        var request = new CriarDitadoRequest
        {
            Titulo = "Animais Domésticos",
            Descricao = "Teste sobre animais",
            TextoComMarcacoes = "O [cachorro] late muito quando vê o [gato].",
            AudioBase64 = audioBase64
        };

        // Act
        var response = await PostAsync<DitadoResponse>("/api/ditados", request);

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().BeGreaterThan(0);
        response.Titulo.Should().Be("Animais Domésticos");
        response.Descricao.Should().Be("Teste sobre animais");
        response.DataCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ObterDitadoParaRealizar_ComIdValido_DeveRetornarDitadoSemRespostas()
    {
        // Arrange - Criar ditado primeiro
        var ditadoId = await CriarDitadoTeste();

        // Act
        var response = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");

        // Assert
        response.Should().NotBeNull();
        response!.Id.Should().Be(ditadoId);
        response.Titulo.Should().NotBeNullOrEmpty();
        response.AudioBase64.Should().StartWith("data:audio/mpeg;base64,");
        response.Segmentos.Should().NotBeEmpty();
        
        // Valida que segmentos de lacuna não contêm a resposta
        var lacunas = response.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();
        lacunas.Should().NotBeEmpty();
        lacunas.All(l => l.Conteudo == null).Should().BeTrue();
        lacunas.All(l => l.SegmentoId.HasValue).Should().BeTrue();
        
        // Valida que segmentos de texto contêm o conteúdo
        var textos = response.Segmentos.Where(s => s.Tipo == "Texto").ToList();
        textos.Should().NotBeEmpty();
        textos.All(t => !string.IsNullOrEmpty(t.Conteudo)).Should().BeTrue();
    }

    [Fact]
    public async Task ObterDitadoParaRealizar_ComIdInexistente_DeveRetornarNotFound()
    {
        // Act
        var response = await GetAsyncRaw("/api/ditados/99999/realizar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmeterResposta_ComRespostasCorretas_DeveRetornarPontuacao100()
    {
        // Arrange - Criar ditado
        var ditadoId = await CriarDitadoTeste();
        
        // Obter segmentos para pegar os IDs das lacunas
        var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
        var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

        var request = new SubmeterRespostaRequest
        {
            Respostas = new List<RespostaSegmentoDto>
            {
                new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "cachorro" },
                new() { SegmentoId = lacunas[1].SegmentoId!.Value, Resposta = "gato" }
            }
        };

        // Act
        var response = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{ditadoId}/submeter", request);

        // Assert
        response.Should().NotBeNull();
        response!.Pontuacao.Should().Be(100);
        response.TotalLacunas.Should().Be(2);
        response.Acertos.Should().Be(2);
        response.Erros.Should().Be(0);
        response.Detalhes.Should().HaveCount(2);
        response.Detalhes.All(d => d.Correto).Should().BeTrue();
    }

    [Fact]
    public async Task SubmeterResposta_ComErrosOrtograficos_DeveIdentificarTipoErro()
    {
        // Arrange
        var ditadoId = await CriarDitadoTeste();
        var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
        var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

        var request = new SubmeterRespostaRequest
        {
            Respostas = new List<RespostaSegmentoDto>
            {
                new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "cachoro" }, // Erro ortográfico
                new() { SegmentoId = lacunas[1].SegmentoId!.Value, Resposta = "gato" }
            }
        };

        // Act
        var response = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{ditadoId}/submeter", request);

        // Assert
        response.Should().NotBeNull();
        response!.Pontuacao.Should().Be(50);
        response.Acertos.Should().Be(1);
        response.Erros.Should().Be(1);
        
        var erroOrtografico = response.Detalhes.First(d => !d.Correto);
        erroOrtografico.RespostaFornecida.Should().Be("cachoro");
        erroOrtografico.RespostaEsperada.Should().Be("cachorro");
        erroOrtografico.TipoErro.Should().Be("Ortografico");
    }

    [Fact]
    public async Task SubmeterResposta_ComErroAcentuacao_DeveIdentificarComoAcentuacao()
    {
        // Arrange - Criar ditado com palavra acentuada
        var ditadoId = await CriarDitadoComAcentuacao();
        var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
        var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

        var request = new SubmeterRespostaRequest
        {
            Respostas = new List<RespostaSegmentoDto>
            {
                new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "arvore" } // Sem acento
            }
        };

        // Act
        var response = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{ditadoId}/submeter", request);

        // Assert
        response.Should().NotBeNull();
        var erro = response!.Detalhes.First();
        erro.Correto.Should().BeFalse();
        erro.TipoErro.Should().Be("Acentuacao");
        erro.RespostaEsperada.Should().Be("árvore");
    }

    [Fact]
    public async Task SubmeterResposta_ComRespostaVazia_DeveIdentificarComoOmissao()
    {
        // Arrange
        var ditadoId = await CriarDitadoTeste();
        var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
        var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

        var request = new SubmeterRespostaRequest
        {
            Respostas = new List<RespostaSegmentoDto>
            {
                new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "" },
                new() { SegmentoId = lacunas[1].SegmentoId!.Value, Resposta = "gato" }
            }
        };

        // Act
        var response = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{ditadoId}/submeter", request);

        // Assert
        var omissao = response!.Detalhes.First(d => d.RespostaFornecida == "");
        omissao.TipoErro.Should().Be("Omissao");
    }

    [Fact]
    public async Task ListarDitados_DeveRetornarApenasAtivos()
    {
        // Arrange - Criar alguns ditados
        await CriarDitadoTeste();
        await CriarDitadoTeste();

        // Act
        var response = await GetAsync<List<DitadoResponse>>("/api/ditados");

        // Assert
        response.Should().NotBeNull();
        response!.Should().HaveCountGreaterOrEqualTo(2);
        response.All(d => d.Id > 0).Should().BeTrue();
        response.All(d => !string.IsNullOrEmpty(d.Titulo)).Should().BeTrue();
    }

    [Fact]
    public async Task SubmeterResposta_ParaDitadoInexistente_DeveRetornarNotFound()
    {
        // Arrange
        var request = new SubmeterRespostaRequest
        {
            Respostas = new List<RespostaSegmentoDto>()
        };

        // Act
        var response = await PostAsyncRaw("/api/ditados/99999/submeter", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Métodos auxiliares privados
    private async Task<int> CriarDitadoTeste()
    {
        var audioBase64 = "data:audio/mpeg;base64,SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk=";
        var request = new CriarDitadoRequest
        {
            Titulo = "Teste Ditado",
            Descricao = "Descrição teste",
            TextoComMarcacoes = "O [cachorro] late muito quando vê o [gato].",
            AudioBase64 = audioBase64
        };

        var response = await PostAsync<DitadoResponse>("/api/ditados", request);
        return response!.Id;
    }

    private async Task<int> CriarDitadoComAcentuacao()
    {
        var audioBase64 = "data:audio/mpeg;base64,SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk=";
        var request = new CriarDitadoRequest
        {
            Titulo = "Teste Acentuação",
            Descricao = "Teste com palavras acentuadas",
            TextoComMarcacoes = "A [árvore] é bonita.",
            AudioBase64 = audioBase64
        };

        var response = await PostAsync<DitadoResponse>("/api/ditados", request);
        return response!.Id;
    }
}