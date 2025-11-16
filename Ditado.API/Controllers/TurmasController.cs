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
[Produces("application/json")]
public class TurmasController : ControllerBase
{
    private readonly TurmaService _turmaService;

    public TurmasController(TurmaService turmaService)
    {
        _turmaService = turmaService;
    }

    /// <summary>
    /// Cria uma nova turma
    /// </summary>
    /// <remarks>
    /// Apenas Administradores e Professores podem criar turmas.
    /// 
    /// Exemplo de requisição:
    /// 
    ///     POST /api/turmas
    ///     {
    ///        "nome": "5º Ano A",
    ///        "serie": 5,
    ///        "anoLetivo": 2024,
    ///        "semestre": "1º Semestre",
    ///        "descricao": "Turma da manhã",
    ///        "professorResponsavelId": 1,
    ///        "alunosIds": [2, 3, 4]
    ///     }
    /// </remarks>
    /// <param name="request">Dados da turma a ser criada</param>
    /// <returns>Turma criada com sucesso</returns>
    /// <response code="201">Turma criada com sucesso</response>
    /// <response code="400">Dados inválidos (professor inválido, alunos inválidos, etc.)</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    [HttpPost]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(typeof(TurmaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>
    /// Obtém uma turma por ID
    /// </summary>
    /// <remarks>
    /// Retorna os dados completos de uma turma específica, incluindo:
    /// - Informações básicas (nome, série, ano letivo)
    /// - Professor responsável
    /// - Lista completa de alunos matriculados
    /// </remarks>
    /// <param name="id">ID da turma</param>
    /// <returns>Dados da turma</returns>
    /// <response code="200">Turma encontrada</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="404">Turma não encontrada</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TurmaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TurmaResponse>> ObterTurma(int id)
    {
        var turma = await _turmaService.ObterPorIdAsync(id);

        if (turma == null)
            return NotFound(new { mensagem = "Turma não encontrada." });

        return Ok(turma);
    }

    /// <summary>
    /// Lista todas as turmas
    /// </summary>
    /// <remarks>
    /// Retorna uma lista de turmas, com opção de filtrar apenas turmas ativas.
    /// Por padrão, retorna apenas turmas ativas (Ativo = true).
    /// </remarks>
    /// <param name="apenasAtivas">Filtrar apenas turmas ativas (padrão: true)</param>
    /// <returns>Lista de turmas</returns>
    /// <response code="200">Lista de turmas retornada com sucesso</response>
    /// <response code="401">Não autenticado</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<TurmaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TurmaResponse>>> ListarTurmas([FromQuery] bool? apenasAtivas = true)
    {
        var turmas = await _turmaService.ListarTurmasAsync(apenasAtivas);
        return Ok(turmas);
    }

    /// <summary>
    /// Lista turmas de um professor específico
    /// </summary>
    /// <remarks>
    /// Retorna apenas as turmas onde o professor especificado é o responsável.
    /// Apenas Administradores e Professores podem acessar este endpoint.
    /// </remarks>
    /// <param name="professorId">ID do professor</param>
    /// <returns>Lista de turmas do professor</returns>
    /// <response code="200">Lista de turmas retornada com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    [HttpGet("professor/{professorId}")]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(typeof(List<TurmaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TurmaResponse>>> ListarTurmasPorProfessor(int professorId)
    {
        var turmas = await _turmaService.ListarTurmasPorProfessorAsync(professorId);
        return Ok(turmas);
    }

    /// <summary>
    /// Atualiza uma turma existente
    /// </summary>
    /// <remarks>
    /// Permite atualizar qualquer campo da turma:
    /// - Nome, série, ano letivo, semestre, descrição
    /// - Professor responsável
    /// - Lista de alunos (substitui completamente a lista atual)
    /// - Status ativo/inativo
    /// 
    /// Apenas Administradores e Professores podem atualizar turmas.
    /// 
    /// Exemplo de requisição para adicionar alunos:
    /// 
    ///     PUT /api/turmas/1
    ///     {
    ///        "alunosIds": [2, 3, 4, 5]
    ///     }
    ///     
    /// Para remover todos os alunos:
    /// 
    ///     PUT /api/turmas/1
    ///     {
    ///        "alunosIds": []
    ///     }
    /// </remarks>
    /// <param name="id">ID da turma</param>
    /// <param name="request">Dados a serem atualizados (apenas campos preenchidos serão alterados)</param>
    /// <returns>Turma atualizada</returns>
    /// <response code="200">Turma atualizada com sucesso</response>
    /// <response code="400">Dados inválidos</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    /// <response code="404">Turma não encontrada</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(typeof(TurmaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Exclui uma turma
    /// </summary>
    /// <remarks>
    /// Remove permanentemente uma turma do sistema.
    /// 
    /// **Regras de permissão:**
    /// - **Administrador**: Pode excluir qualquer turma
    /// - **Professor**: Pode excluir apenas turmas onde ele é o responsável
    /// 
    ///  **ATENÇÃO**: Esta operação é irreversível! 
    /// Considere desativar a turma ao invés de excluí-la (usando PUT com "ativo": false).
    /// </remarks>
    /// <param name="id">ID da turma</param>
    /// <response code="204">Turma excluída com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (professor tentando excluir turma de outro professor)</response>
    /// <response code="404">Turma não encontrada</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            return StatusCode(403, new { mensagem = ex.Message });
        }
    }
}