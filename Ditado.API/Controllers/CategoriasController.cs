using Ditado.Aplicacao.DTOs.Categorias;
using Ditado.Aplicacao.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CategoriasController : ControllerBase
{
    private readonly CategoriaService _categoriaService;

    public CategoriasController(CategoriaService categoriaService)
    {
        _categoriaService = categoriaService;
    }

    /// <summary>
    /// Cria uma nova categoria
    /// </summary>
    /// <remarks>
    /// Apenas Administradores e Professores podem criar categorias.
    /// 
    /// Categorias servem como tags para organizar ditados.
    /// Um ditado pode ter várias categorias.
    /// 
    /// **Exemplo de requisição:**
    /// 
    ///     POST /api/categorias
    ///     {
    ///        "nome": "Ortografia"
    ///     }
    /// </remarks>
    /// <param name="request">Dados da categoria</param>
    /// <returns>Categoria criada</returns>
    /// <response code="201">Categoria criada com sucesso</response>
    /// <response code="400">Dados inválidos (nome vazio ou duplicado)</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    [HttpPost]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CategoriaResponse>> CriarCategoria([FromBody] CriarCategoriaRequest request)
    {
        try
        {
            var categoria = await _categoriaService.CriarCategoriaAsync(request);
            return CreatedAtAction(nameof(ObterCategoria), new { id = categoria.Id }, categoria);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Lista todas as categorias
    /// </summary>
    /// <remarks>
    /// Retorna todas as categorias cadastradas no sistema.
    /// Qualquer usuário autenticado pode acessar.
    /// 
    /// Inclui a contagem de ditados em cada categoria.
    /// </remarks>
    /// <returns>Lista de categorias</returns>
    /// <response code="200">Lista retornada com sucesso</response>
    /// <response code="401">Não autenticado</response>
    [HttpGet]
    [Authorize(Roles = "Administrador,Professor,Aluno")]
    [ProducesResponseType(typeof(List<CategoriaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CategoriaResponse>>> ListarCategorias()
    {
        var categorias = await _categoriaService.ListarCategoriasAsync();
        return Ok(categorias);
    }

    /// <summary>
    /// Obtém uma categoria por ID
    /// </summary>
    /// <remarks>
    /// Retorna os detalhes de uma categoria específica.
    /// </remarks>
    /// <param name="id">ID da categoria</param>
    /// <returns>Dados da categoria</returns>
    /// <response code="200">Categoria encontrada</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="404">Categoria não encontrada</response>
    [HttpGet("{id}")]
    [Authorize(Roles = "Administrador,Professor,Aluno")]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaResponse>> ObterCategoria(int id)
    {
        var categoria = await _categoriaService.ObterPorIdAsync(id);

        if (categoria == null)
            return NotFound(new { mensagem = "Categoria não encontrada." });

        return Ok(categoria);
    }

    /// <summary>
    /// Atualiza uma categoria
    /// </summary>
    /// <remarks>
    /// Permite alterar o nome de uma categoria existente.
    /// Apenas Administradores e Professores podem atualizar.
    /// 
    /// **Exemplo de requisição:**
    /// 
    ///     PUT /api/categorias/5
    ///     {
    ///        "nome": "Ortografia Avançada"
    ///     }
    /// </remarks>
    /// <param name="id">ID da categoria</param>
    /// <param name="request">Dados atualizados</param>
    /// <returns>Categoria atualizada</returns>
    /// <response code="200">Categoria atualizada com sucesso</response>
    /// <response code="400">Dados inválidos (nome vazio ou duplicado)</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    /// <response code="404">Categoria não encontrada</response>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(typeof(CategoriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaResponse>> AtualizarCategoria(int id, [FromBody] AtualizarCategoriaRequest request)
    {
        try
        {
            var categoria = await _categoriaService.AtualizarCategoriaAsync(id, request);

            if (categoria == null)
                return NotFound(new { mensagem = "Categoria não encontrada." });

            return Ok(categoria);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Exclui uma categoria
    /// </summary>
    /// <remarks>
    /// Remove permanentemente uma categoria do sistema.
    /// Apenas Administradores e Professores podem excluir.
    /// 
    /// **ATENÇÃO**: Esta operação remove a categoria de todos os ditados associados.
    /// </remarks>
    /// <param name="id">ID da categoria</param>
    /// <response code="204">Categoria excluída com sucesso</response>
    /// <response code="401">Não autenticado</response>
    /// <response code="403">Sem permissão (apenas Admin e Professor)</response>
    /// <response code="404">Categoria não encontrada</response>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador,Professor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletarCategoria(int id)
    {
        var sucesso = await _categoriaService.DeletarCategoriaAsync(id);

        if (!sucesso)
            return NotFound(new { mensagem = "Categoria não encontrada." });

        return NoContent();
    }
}