using System.Security.Claims;
using Ditado.Aplicacao.DTOs.Turmas;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TurmasController : ControllerBase
{
    private readonly TurmaService _turmaService;

    public TurmasController(TurmaService turmaService)
    {
        _turmaService = turmaService;
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult<TurmaResponse>> CriarTurma([FromBody] CriarTurmaRequest request)
    {
        try
        {
            var turma = await _turmaService.CriarTurmaAsync(request);
            return CreatedAtAction(nameof(ObterTurma), new { id = turma.Id }, turma);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TurmaResponse>> ObterTurma(int id)
    {
        var turma = await _turmaService.ObterPorIdAsync(id);

        if (turma == null)
            return NotFound(new { mensagem = "Turma não encontrada." });

        return Ok(turma);
    }

    [HttpGet]
    public async Task<ActionResult<List<TurmaResponse>>> ListarTurmas([FromQuery] bool? apenasAtivas = true)
    {
        var turmas = await _turmaService.ListarTurmasAsync(apenasAtivas);
        return Ok(turmas);
    }

    [HttpGet("professor/{professorId}")]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult<List<TurmaResponse>>> ListarTurmasPorProfessor(int professorId)
    {
        var turmas = await _turmaService.ListarTurmasPorProfessorAsync(professorId);
        return Ok(turmas);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult<TurmaResponse>> AtualizarTurma(int id, [FromBody] AtualizarTurmaRequest request)
    {
        try
        {
            var turma = await _turmaService.AtualizarTurmaAsync(id, request);

            if (turma == null)
                return NotFound(new { mensagem = "Turma não encontrada." });

            return Ok(turma);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult> ExcluirTurma(int id)
    {
        try
        {
            // Obter ID e Tipo do usuário logado
            var usuarioLogadoId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var tipoUsuarioLogado = Enum.Parse<TipoUsuario>(User.FindFirst(ClaimTypes.Role)?.Value ?? "Aluno");

            var sucesso = await _turmaService.ExcluirTurmaAsync(id, usuarioLogadoId, tipoUsuarioLogado);

            if (!sucesso)
                return NotFound(new { mensagem = "Turma não encontrada." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { mensagem = ex.Message }); // HTTP 403 Forbidden com body JSON
        }
    }
}