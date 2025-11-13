using Ditado.Aplicacao.DTOs;
using Ditado.Aplicacao.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Protege TODOS os métodos do controller
public class DitadosController : ControllerBase
{
    private readonly DitadoService _ditadoService;

    public DitadosController(DitadoService ditadoService)
    {
        _ditadoService = ditadoService;
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Professor")] // Apenas Admin e Professor podem criar
    public async Task<ActionResult<DitadoResponse>> CriarDitado([FromBody] CriarDitadoRequest request)
    {
        var resultado = await _ditadoService.CriarDitadoAsync(request);
        return CreatedAtAction(nameof(ObterDitadoParaRealizar), new { id = resultado.Id }, resultado);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Professor,Aluno")] // Todos logados podem listar
    public async Task<ActionResult<List<DitadoResponse>>> ListarDitados()
    {
        var ditados = await _ditadoService.ListarDitadosAsync();
        return Ok(ditados);
    }

    [HttpGet("{id}/realizar")]
    [Authorize(Roles = "Administrador,Professor,Aluno")] // Todos logados podem acessar
    public async Task<ActionResult<DitadoParaRealizarResponse>> ObterDitadoParaRealizar(int id)
    {
        var ditado = await _ditadoService.ObterDitadoParaRealizarAsync(id);
        
        if (ditado == null)
            return NotFound(new { mensagem = "Ditado não encontrado." });
        
        return Ok(ditado);
    }

    [HttpPost("{id}/submeter")]
    [Authorize(Roles = "Administrador,Professor,Aluno")] // Todos logados podem submeter
    public async Task<ActionResult<ResultadoDitadoResponse>> SubmeterResposta(int id, [FromBody] SubmeterRespostaRequest request)
    {
        var resultado = await _ditadoService.SubmeterRespostaAsync(id, request);
        
        if (resultado == null)
            return NotFound(new { mensagem = "Ditado não encontrado." });
        
        return Ok(resultado);
    }
}