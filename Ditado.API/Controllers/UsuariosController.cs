using System.Security.Claims;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Aplicacao.Services;
using Ditado.Dominio.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ditado.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<UsuarioResponse>> CriarUsuario([FromBody] CriarUsuarioRequest request)
    {
        try
        {
            var usuario = await _usuarioService.CriarUsuarioAsync(request);
            return CreatedAtAction(nameof(ObterUsuario), new { id = usuario.Id }, usuario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPost("solicitar-acesso")]
    [AllowAnonymous]
    public async Task<ActionResult<UsuarioResponse>> SolicitarAcesso([FromBody] CriarUsuarioRequest request)
    {
        try
        {
            var usuario = await _usuarioService.SolicitarAcessoAsync(request);
            return Ok(new 
            { 
                mensagem = "Solicitação de acesso enviada com sucesso! Aguarde a aprovação de um administrador ou professor.",
                usuario 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpGet("solicitacoes-pendentes")]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult<List<UsuarioResponse>>> ListarSolicitacoesPendentes()
    {
        var solicitacoes = await _usuarioService.ListarSolicitacoesPendentesAsync();
        return Ok(solicitacoes);
    }

    [HttpPost("{id}/aprovar-acesso")]
    [Authorize(Roles = "Administrador,Professor")]
    public async Task<ActionResult<UsuarioResponse>> AprovarAcesso(int id, [FromBody] AprovarAcessoRequest request)
    {
        try
        {
            // Obtém o tipo do usuário logado
            var tipoUsuarioLogado = User.FindFirst(ClaimTypes.Role)?.Value;
            var tipoEnum = Enum.Parse<TipoUsuario>(tipoUsuarioLogado ?? "Aluno");

            var usuario = await _usuarioService.AprovarAcessoAsync(id, request, tipoEnum);

            if (usuario == null)
                return NotFound(new { mensagem = "Usuário não encontrado." });

            return Ok(new 
            { 
                mensagem = "Acesso aprovado com sucesso!",
                usuario 
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var resultado = await _usuarioService.LoginAsync(request);

            if (resultado == null)
                return Unauthorized(new { mensagem = "Login ou senha inválidos." });

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            // Retorna erro específico para acesso não aprovado
            return Unauthorized(new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<UsuarioResponse>> ObterUsuario(int id)
    {
        var usuario = await _usuarioService.ObterPorIdAsync(id);

        if (usuario == null)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        return Ok(usuario);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<List<UsuarioResponse>>> ListarUsuarios()
    {
        var usuarios = await _usuarioService.ListarUsuariosAsync();
        return Ok(usuarios);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<UsuarioResponse>> AtualizarUsuario(int id, [FromBody] AtualizarUsuarioRequest request)
    {
        try
        {
            var usuario = await _usuarioService.AtualizarUsuarioAsync(id, request);

            if (usuario == null)
                return NotFound(new { mensagem = "Usuário não encontrado." });

            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    [HttpPatch("{id}/bloquear")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> BloquearUsuario(int id)
    {
        var sucesso = await _usuarioService.BloquearUsuarioAsync(id);

        if (!sucesso)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        return NoContent();
    }

    [HttpPatch("{id}/desbloquear")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> DesbloquearUsuario(int id)
    {
        var sucesso = await _usuarioService.DesbloquearUsuarioAsync(id);

        if (!sucesso)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> DeletarUsuario(int id)
    {
        var sucesso = await _usuarioService.DeletarUsuarioAsync(id);

        if (!sucesso)
            return NotFound(new { mensagem = "Usuário não encontrado." });

        return NoContent();
    }
}