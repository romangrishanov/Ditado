using System.Net;
using Ditado.Aplicacao.DTOs;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Enums;
using Ditado.Testes.Infra;
using FluentAssertions;
using Xunit;

namespace Ditado.Testes.Controllers;

public class DitadosControllerTests : TesteIntegracaoBase
{
	private string? _tokenAdmin;

	public DitadosControllerTests(CustomWebApplicationFactory factory) : base(factory)
	{
	}

	// Método auxiliar para fazer login e obter token
	private async Task<string> ObterTokenAdminAsync()
	{
		if (_tokenAdmin != null)
			return _tokenAdmin;

		// Tenta fazer login com o admin temporário (criado pela migration)
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

		// Se o login falhar, criar um usuário admin para os testes
		// (isso só funcionará se o endpoint POST /usuarios permitir criar o primeiro admin sem auth)
		throw new InvalidOperationException("Não foi possível obter token de admin. Certifique-se de que o usuário admin@admin.com existe ou configure os testes adequadamente.");
	}

	[Fact]
	public async Task CriarDitado_ComDadosValidos_DeveRetornarSucesso()
	{
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var audioBase64 = "data:audio/mpeg;base64,SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk=";
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
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var ditadoId = await CriarDitadoTeste();

		// Act
		var response = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");

		// Assert
		response.Should().NotBeNull();
		response!.Id.Should().Be(ditadoId);
		response.Titulo.Should().NotBeNullOrEmpty();
		response.AudioBase64.Should().StartWith("data:audio/mpeg;base64,");
		response.Segmentos.Should().NotBeEmpty();

		var lacunas = response.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();
		lacunas.Should().NotBeEmpty();
		lacunas.All(l => l.Conteudo == null).Should().BeTrue();
		lacunas.All(l => l.SegmentoId.HasValue).Should().BeTrue();

		var textos = response.Segmentos.Where(s => s.Tipo == "Texto").ToList();
		textos.Should().NotBeEmpty();
		textos.All(t => !string.IsNullOrEmpty(t.Conteudo)).Should().BeTrue();
	}

	[Fact]
	public async Task ObterDitadoParaRealizar_ComIdInexistente_DeveRetornarNotFound()
	{
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		// Act
		var response = await GetAsyncRaw("/api/ditados/99999/realizar");

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task SubmeterResposta_ComRespostasCorretas_DeveRetornarPontuacao100()
	{
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var ditadoId = await CriarDitadoTeste();

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
		response!.Nota.Should().Be(100);
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
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var ditadoId = await CriarDitadoTeste();
		var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
		var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

		var request = new SubmeterRespostaRequest
		{
			Respostas = new List<RespostaSegmentoDto>
			{
				new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "cachoro" },
				new() { SegmentoId = lacunas[1].SegmentoId!.Value, Resposta = "gato" }
			}
		};

		// Act
		var response = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{ditadoId}/submeter", request);

		// Assert
		response.Should().NotBeNull();
		response!.Nota.Should().Be(50);
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
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var ditadoId = await CriarDitadoComAcentuacao();
		var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{ditadoId}/realizar");
		var lacunas = ditado!.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();

		var request = new SubmeterRespostaRequest
		{
			Respostas = new List<RespostaSegmentoDto>
			{
				new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "arvore" }
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
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

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
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

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
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var request = new SubmeterRespostaRequest
		{
			Respostas = new List<RespostaSegmentoDto>()
		};

		// Act
		var response = await PostAsyncRaw("/api/ditados/99999/submeter", request);

		// Assert
		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task CriarDitado_ComPontuacaoVariada_DeveLimparCorretamente()
	{
		// Arrange
		var token = await ObterTokenAdminAsync();
		AdicionarTokenAutorizacao(token);

		var audioBase64 = "data:audio/mpeg;base64,SUQzAwAAAAAAJlRQRTEAAAAcAAAAU291bmRKYXk=";
		var request = new CriarDitadoRequest
		{
			Titulo = "Teste Pontuação",
			Descricao = "Teste com pontos e maiúsculas",
			TextoComMarcacoes = "bla[. Nova] palavra e [Gato.] final",
			AudioBase64 = audioBase64
		};

		// Act
		var response = await PostAsync<DitadoResponse>("/api/ditados", request);
		var ditado = await GetAsync<DitadoParaRealizarResponse>($"/api/ditados/{response!.Id}/realizar");

		// Assert - Verificar segmentos
		ditado!.Segmentos.Should().HaveCount(5);
		ditado.Segmentos[0].Conteudo.Should().Be("bla. ");
		ditado.Segmentos[1].Tipo.Should().Be("Lacuna");
		ditado.Segmentos[2].Conteudo.Should().Be(" palavra e ");
		ditado.Segmentos[3].Tipo.Should().Be("Lacuna");
		ditado.Segmentos[4].Conteudo.Should().Be(". final");

		// Testar comparação case-insensitive
		var lacunas = ditado.Segmentos.Where(s => s.Tipo == "Lacuna").ToList();
		var respostaRequest = new SubmeterRespostaRequest
		{
			Respostas = new List<RespostaSegmentoDto>
			{
				new() { SegmentoId = lacunas[0].SegmentoId!.Value, Resposta = "nova" },
				new() { SegmentoId = lacunas[1].SegmentoId!.Value, Resposta = "gato" }
			}
		};

		var resultado = await PostAsync<ResultadoDitadoResponse>($"/api/ditados/{response.Id}/submeter", respostaRequest);
		resultado!.Nota.Should().Be(100);
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