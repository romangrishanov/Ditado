using Ditado.Aplicacao.DTOs;
using Ditado.Aplicacao.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DitadosController : ControllerBase
{
	private readonly DitadoService _ditadoService;

	public DitadosController(DitadoService ditadoService)
	{
		_ditadoService = ditadoService;
	}

	/// <summary>
	/// Cria um novo ditado
	/// </summary>
	/// <remarks>
	/// Apenas Administradores e Professores podem criar ditados.
	/// 
	/// **Como funciona:**
	/// 1. Envie um texto corrido com palavras entre **colchetes** `[palavra]`
	/// 2. O sistema automaticamente transforma palavras entre colchetes em **lacunas** (campos vazios)
	/// 3. O restante do texto permanece como **texto fixo** (não editável)
	/// 4. Envie o áudio completo da leitura do ditado em Base64
	/// 
	/// **Exemplo de texto com marcações:**
	/// 
	///     "O [cachorro] late muito alto. A [gata] mia baixinho."
	///     
	/// **Resultado para o aluno:**
	/// - Texto fixo: "O "
	/// - **Lacuna 1**: campo vazio (resposta esperada: "cachorro")
	/// - Texto fixo: " late muito alto. A "
	/// - **Lacuna 2**: campo vazio (resposta esperada: "gata")
	/// - Texto fixo: " mia baixinho."
	/// 
	/// **Regras:**
	/// - Use colchetes [palavra] para marcar lacunas
	/// - Mantenha pontuação e espaços fora dos colchetes
	/// - O áudio deve ser o ditado completo (não apenas as palavras isoladas)
	/// - Formatos de áudio recomendado: Opus
	/// </remarks>
	/// <param name="request">Dados do ditado a ser criado</param>
	/// <returns>Ditado criado</returns>
	/// <response code="201">Ditado criado com sucesso</response>
	/// <response code="400">Dados inválidos (texto vazio, áudio inválido, etc.)</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Admin e Professor)</response>
	[HttpPost]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(DitadoResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<DitadoResponse>> CriarDitado([FromBody] CriarDitadoRequest request)
	{
		var resultado = await _ditadoService.CriarDitadoAsync(request);
		return CreatedAtAction(nameof(ObterDitadoParaRealizar), new { id = resultado.Id }, resultado);
	}

	/// <summary>
	/// Lista todos os ditados disponíveis
	/// </summary>
	/// <remarks>
	/// Retorna uma lista de todos os ditados cadastrados no sistema.
	/// Qualquer usuário autenticado pode acessar este endpoint.
	/// 
	/// A lista contém informações resumidas dos ditados:
	/// - ID, Título, Descrição
	/// - Data de criação
	/// 
	/// Não inclui o conteúdo completo nem o áudio.
	/// </remarks>
	/// <returns>Lista de ditados</returns>
	/// <response code="200">Lista retornada com sucesso</response>
	/// <response code="401">Não autenticado</response>
	[HttpGet]
	[Authorize(Roles = "Administrador,Professor,Aluno")]
	[ProducesResponseType(typeof(List<DitadoResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<List<DitadoResponse>>> ListarDitados()
	{
		var ditados = await _ditadoService.ListarDitadosAsync();
		return Ok(ditados);
	}

	/// <summary>
	/// Obtém um ditado para realização
	/// </summary>
	/// <remarks>
	/// Retorna o ditado formatado para o aluno realizar:
	/// 
	/// **O que é retornado:**
	/// - Título do ditado
	/// - Áudio completo em Base64 (para reproduzir)
	/// - Segmentos ordenados:
	///   - **Texto fixo**: Exibido como está (ex: "O ", " late muito alto. A ")
	///   - **Lacuna**: Campo vazio para o aluno preencher (ex: `[cachorro]` vira um campo input)
	/// 
	/// **O que NÃO é retornado:**
	/// - ❌ Respostas esperadas (para evitar "cola")
	/// - ❌ Texto original com marcações
	/// 
	/// **Exemplo de resposta:**
	/// 
	///     {
	///        "id": 1,
	///        "titulo": "Ditado sobre animais",
	///        "audioBase64": "data:audio/mpeg;base64,//uQxAAA...",
	///        "segmentos": [
	///           { "id": 1, "ordem": 1, "tipo": "TextoFixo", "conteudo": "O " },
	///           { "id": 2, "ordem": 2, "tipo": "Lacuna", "conteudo": "" },
	///           { "id": 3, "ordem": 3, "tipo": "TextoFixo", "conteudo": " late muito alto. A " },
	///           { "id": 4, "ordem": 4, "tipo": "Lacuna", "conteudo": "" },
	///           { "id": 5, "ordem": 5, "tipo": "TextoFixo", "conteudo": " mia baixinho." }
	///        ]
	///     }
	/// </remarks>
	/// <param name="id">ID do ditado</param>
	/// <returns>Ditado pronto para ser realizado</returns>
	/// <response code="200">Ditado encontrado</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpGet("{id}/realizar")]
	[Authorize(Roles = "Administrador,Professor,Aluno")]
	[ProducesResponseType(typeof(DitadoParaRealizarResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<DitadoParaRealizarResponse>> ObterDitadoParaRealizar(int id)
	{
		var ditado = await _ditadoService.ObterDitadoParaRealizarAsync(id);

		if (ditado == null)
			return NotFound(new { mensagem = "Ditado não encontrado." });

		return Ok(ditado);
	}

	/// <summary>
	/// Submete as respostas de um ditado
	/// </summary>
	/// <remarks>
	/// Após o aluno preencher todas as lacunas, este endpoint:
	/// 1. Recebe as respostas do aluno (apenas para os segmentos tipo "Lacuna")
	/// 2. Compara com as respostas esperadas (palavras que estavam entre colchetes)
	/// 3. Classifica os erros
	/// 4. Calcula a pontuação (0-100)
	/// 5. Retorna o resultado detalhado com feedback para cada lacuna
	/// 
	/// **Exemplo de requisição:**
	/// 
	///     POST /api/ditados/1/submeter
	///     {
	///        "respostas": [
	///           {
	///              "segmentoId": 2,
	///              "respostaAluno": "cachoro"
	///           },
	///           {
	///              "segmentoId": 4,
	///              "respostaAluno": "gata"
	///           }
	///        ]
	///     }
	///     
	/// **Exemplo de resposta:**
	/// 
	///     {
	///        "respostaDitadoId": 15,
	///        "pontuacao": 50.0,
	///        "totalLacunas": 2,
	///        "acertos": 1,
	///        "erros": 1,
	///        "detalhes": [
	///           {
	///              "segmentoId": 2,
	///              "ordem": 2,
	///              "respostaAluno": "cachoro",
	///              "respostaEsperada": "cachorro",
	///              "correto": false,
	///              "tipoErro": "Ortografico"
	///           },
	///           {
	///              "segmentoId": 4,
	///              "ordem": 4,
	///              "respostaAluno": "gata",
	///              "respostaEsperada": "gata",
	///              "correto": true,
	///              "tipoErro": "Nenhum"
	///           }
	///        ]
	///     }
	/// 
	/// **Observações:**
	/// - Apenas segmentos tipo "Lacuna" precisam ser enviados
	/// - Segmentos tipo "TextoFixo" serão ignorados
	/// - A ordem das respostas não importa (usa `segmentoId` para mapear)
	/// - Se uma lacuna não for enviada, conta como "Omissão"
	/// </remarks>
	/// <param name="id">ID do ditado</param>
	/// <param name="request">Respostas do aluno (apenas para lacunas)</param>
	/// <returns>Resultado da correção com feedback detalhado</returns>
	/// <response code="200">Respostas corrigidas com sucesso</response>
	/// <response code="400">Dados inválidos (segmento não existe, etc.)</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpPost("{id}/submeter")]
	[Authorize(Roles = "Administrador,Professor,Aluno")]
	[ProducesResponseType(typeof(ResultadoDitadoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ResultadoDitadoResponse>> SubmeterResposta(int id, [FromBody] SubmeterRespostaRequest request)
	{
		var resultado = await _ditadoService.SubmeterRespostaAsync(id, request);

		if (resultado == null)
			return NotFound(new { mensagem = "Ditado não encontrado." });

		return Ok(resultado);
	}
}