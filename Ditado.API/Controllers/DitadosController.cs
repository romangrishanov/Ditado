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
		var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
		var resultado = await _ditadoService.CriarDitadoAsync(request, usuarioId);
		return CreatedAtAction(nameof(ObterDitadoParaRealizar), new { id = resultado.Id }, resultado);
	}

	/// <summary>
	/// Lista todos os ditados disponíveis
	/// </summary>
	/// <remarks>
	/// Retorna uma lista de ditados com diferentes visibilidades por perfil:
	/// 
	/// **Administrador/Professor:**
	/// - Vê todos os ditados cadastrados no sistema
	/// 
	/// **Aluno:**
	/// - Vê apenas ditados atribuídos às suas turmas
	/// - Não vê ditados não atribuídos
	/// 
	/// **Informações retornadas:**
	/// - ID, Título, Descrição
	/// - Autor do ditado
	/// - Categorias associadas
	/// - Data de criação
	/// 
	/// **Não inclui:** Conteúdo completo nem áudio.
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
	/// Retorna o ditado formatado para o aluno realizar.
	/// 
	/// **Permissões:**
	/// - **Administrador/Professor**: Podem acessar qualquer ditado
	/// - **Aluno**: Pode acessar apenas ditados atribuídos às suas turmas
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
	/// <response code="403">Aluno tentando acessar ditado não atribuído à sua turma</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpGet("{id}/realizar")]
	[Authorize(Roles = "Administrador,Professor,Aluno")]
	[ProducesResponseType(typeof(DitadoParaRealizarResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
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
	/// 4. Calcula a nota (0-100)
	/// 5. Retorna o resultado detalhado com feedback para cada lacuna
	/// 
	/// **Permissões:**
	/// - **Aluno**: Pode submeter apenas ditados atribuídos às suas turmas
	/// - **Professor/Admin**: Podem submeter qualquer ditado (para testes)
	/// 
	/// **Observação importante:**
	/// - O aluno pode refazer o mesmo ditado múltiplas vezes
	/// - Todas as tentativas ficam registradas
	/// - Não há limite de tentativas
	/// 
	/// **Exemplo de requisição:**
	/// 
	///     POST /api/ditados/1/submeter
	///     {
	///        "respostas": [
	///           {
	///              "segmentoId": 2,
	///              "resposta": "cachoro"
	///           },
	///           {
	///              "segmentoId": 4,
	///              "resposta": "gata"
	///           }
	///        ]
	///     }
	///     
	/// **Exemplo de resposta:**
	/// 
	///     {
	///        "respostaDitadoId": 15,
	///        "nota": 50.0,
	///        "totalLacunas": 2,
	///        "acertos": 1,
	///        "erros": 1,
	///        "detalhes": [
	///           {
	///              "segmentoId": 2,
	///              "ordem": 2,
	///              "respostaFornecida": "cachoro",
	///              "respostaEsperada": "cachorro",
	///              "correto": false,
	///              "tipoErro": "Ortografico"
	///           },
	///           {
	///              "segmentoId": 4,
	///              "ordem": 4,
	///              "respostaFornecida": "gata",
	///              "respostaEsperada": "gata",
	///              "correto": true,
	///              "tipoErro": null
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
	/// <response code="403">Aluno tentando submeter ditado não atribuído à sua turma</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpPost("{id}/submeter")]
	[Authorize(Roles = "Administrador,Professor,Aluno")]
	[ProducesResponseType(typeof(ResultadoDitadoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ResultadoDitadoResponse>> SubmeterResposta(int id, [FromBody] SubmeterRespostaRequest request)
	{
		var alunoId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
		var resultado = await _ditadoService.SubmeterRespostaAsync(id, request, alunoId);

		if (resultado == null)
			return NotFound(new { mensagem = "Ditado não encontrado." });

		return Ok(resultado);
	}

	/// <summary>
	/// Atualiza um ditado
	/// </summary>
	/// <remarks>
	/// Permite atualizar informações do ditado, incluindo suas categorias.
	/// Apenas Administradores e Professores podem atualizar.
	/// 
	/// **Exemplo de requisição:**
	/// 
	///     PUT /api/ditados/5
	///     {
	///        "titulo": "Ditado sobre animais - Atualizado",
	///        "descricao": "Descrição atualizada",
	///        "categoriaIds": [1, 3, 5],
	///        "ativo": true
	///     }
	/// </remarks>
	/// <param name="id">ID do ditado</param>
	/// <param name="request">Dados atualizados</param>
	/// <returns>Ditado atualizado</returns>
	/// <response code="200">Ditado atualizado com sucesso</response>
	/// <response code="400">Dados inválidos</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Admin e Professor)</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpPut("{id}")]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(DitadoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<DitadoResponse>> AtualizarDitado(int id, [FromBody] AtualizarDitadoRequest request)
	{
		try
		{
			var ditado = await _ditadoService.AtualizarDitadoAsync(id, request);

			if (ditado == null)
				return NotFound(new { mensagem = "Ditado não encontrado." });

			return Ok(ditado);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { mensagem = ex.Message });
		}
	}

	/// <summary>
	/// Exclui um ditado permanentemente
	/// </summary>
	/// <remarks>
	/// Remove um ditado do sistema de forma irreversível.
	/// 
	/// **Permissões:**
	/// - **Administrador**: Pode excluir qualquer ditado
	/// - **Professor**: Pode excluir apenas ditados que ele mesmo criou
	/// - **Aluno**: Não pode excluir ditados
	/// 
	/// **ATENÇÃO**: Esta operação é permanente e não pode ser desfeita!
	/// Considere desativar o ditado ao invés de excluir para manter o histórico.
	/// </remarks>
	/// <param name="id">ID do ditado</param>
	/// <response code="204">Ditado excluído com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas autor ou Admin)</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpDelete("{id}")]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult> DeletarDitado(int id)
	{
		try
		{
			var usuarioId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
			var tipoUsuario = Enum.Parse<Dominio.Enums.TipoUsuario>(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Aluno");

			var sucesso = await _ditadoService.DeletarDitadoAsync(id, usuarioId, tipoUsuario);

			if (!sucesso)
				return NotFound(new { mensagem = "Ditado não encontrado." });

			return NoContent();
		}
		catch (InvalidOperationException ex)
		{
			return Forbid();
		}
	}

	/// <summary>
	/// Obtém um ditado completo por ID (incluindo palavras das lacunas)
	/// </summary>
	/// <remarks>
	/// Retorna a representação completa de um ditado, incluindo:
	/// - Todos os metadados (título, descrição, autor, categorias)
	/// - Áudio em base64
	/// - **Todos os segmentos com conteúdo visível** (inclusive palavras das lacunas)
	/// 
	/// **Diferença do endpoint `/realizar`:**
	/// - `/realizar`: Lacunas aparecem ocultas (sem a palavra)
	/// - `/visualizar` (este): Lacunas aparecem com a palavra visível
	/// 
	/// **Acesso:**
	/// - Professores/Administradores: Podem visualizar qualquer ditado ativo
	/// - Alunos: Sem acesso
	/// 
	/// **Exemplo de resposta:**
	/// 
	///     {
	///       "id": 10,
	///       "titulo": "Ortografia Básica",
	///       "descricao": "Ditado sobre uso de SS, S, Z",
	///       "dataCriacao": "2024-12-01T10:00:00Z",
	///       "autorId": 5,
	///       "autorNome": "Prof. João Silva",
	///       "categorias": [
	///         { "id": 2, "nome": "Ortografia" },
	///         { "id": 5, "nome": "5º Ano" }
	///       ],
	///       "audioBase64": "data:audio/mpeg;base64,SUQzAwAAAAAAJ...",
	///       "segmentos": [
	///         {
	///           "id": 45,
	///           "ordem": 1,
	///           "tipo": "Texto",
	///           "conteudo": "O "
	///         },
	///         {
	///           "id": 46,
	///           "ordem": 2,
	///           "tipo": "Lacuna",
	///           "conteudo": "cachorro"
	///         },
	///         {
	///           "id": 47,
	///           "ordem": 3,
	///           "tipo": "Texto",
	///           "conteudo": " late muito."
	///         }
	///       ]
	///     }
	/// </remarks>
	/// <param name="id">ID do ditado</param>
	/// <returns>Ditado completo com todas as informações</returns>
	/// <response code="200">Ditado retornado com sucesso</response>
	/// <response code="401">Não autenticado</response>
	/// <response code="403">Sem permissão (apenas Professor e Administrador)</response>
	/// <response code="404">Ditado não encontrado</response>
	[HttpGet("{id}/visualizar")]
	[Authorize(Roles = "Administrador,Professor")]
	[ProducesResponseType(typeof(DitadoCompletoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<DitadoCompletoDto>> ObterDitadoCompleto(int id)
	{
		var ditado = await _ditadoService.ObterDitadoCompletoPorIdAsync(id);

		if (ditado == null)
			return NotFound(new { mensagem = "Ditado não encontrado." });

		return Ok(ditado);
	}
}