using Ditado.Aplicacao.DTOs.Alunos;
using Ditado.Aplicacao.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AlunosController : ControllerBase
{
	private readonly AlunoService _alunoService;

	public AlunosController(AlunoService alunoService)
	{
		_alunoService = alunoService;
	}

	/// <summary>
	/// Lista ditados atribuídos ao aluno logado
	/// </summary>
	/// <remarks>
	/// Retorna todos os ditados atribuídos às turmas do aluno, ordenados por data limite.
	/// 
	/// **Inclui:**
	/// - Ditados ainda não feitos
	/// - Ditados já realizados (com status e tentativas)
	/// - Turmas que atribuíram o ditado
	/// - Status: se está atrasado, quantas tentativas, melhor nota
	/// 
	/// **Ordenação:** Data limite mais próxima primeiro (mais urgente)
	/// </remarks>
	/// <returns>Lista de ditados do aluno</returns>
	/// <response code="200">Lista retornada com sucesso</response>
	/// <response code="401">Não autenticado</response>
	[HttpGet("meus-ditados")]
	[Authorize(Roles = "Aluno")]
	[ProducesResponseType(typeof(List<DitadoAlunoResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<List<DitadoAlunoResponse>>> ListarMeusDitados()
	{
		var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
		var ditados = await _alunoService.ListarMeusDitadosAsync(alunoId);
		return Ok(ditados);
	}

	/// <summary>
	/// Lista tentativas do aluno em um ditado específico
	/// </summary>
	/// <remarks>
	/// Retorna todas as tentativas do aluno no ditado, ordenadas da primeira para a última.
	/// 
	/// **Importante:**
	/// - Aluno vê suas tentativas mesmo após o ditado ser desatribuído da turma
	/// - Histórico de tentativas permanece disponível permanentemente
	/// 
	/// **Inclui:**
	/// - Data de realização
	/// - Nota obtida (0-100)
	/// - Total de acertos e erros
	/// - Se foi entregue atrasado (após data limite)
	/// 
	/// **Ordenação:** Primeira tentativa primeiro, última tentativa por último
	/// </remarks>
	/// <param name="ditadoId">ID do ditado</param>
	/// <returns>Lista de tentativas</returns>
	/// <response code="200">Lista retornada com sucesso</response>
	/// <response code="401">Não autenticado</response>
	[HttpGet("ditados/{ditadoId}/minhas-tentativas")]
	[Authorize(Roles = "Aluno")]
	[ProducesResponseType(typeof(List<TentativaDitadoResponse>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<ActionResult<List<TentativaDitadoResponse>>> ListarMinhasTentativas(int ditadoId)
	{
		var alunoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
		var tentativas = await _alunoService.ListarMinhasTentativasAsync(alunoId, ditadoId);
		return Ok(tentativas);
	}
}