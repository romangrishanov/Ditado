using System.ComponentModel.DataAnnotations;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Entidades;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Aplicacao.Services;

public class UsuarioService
{
    private readonly DitadoDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly TokenService _tokenService;

    public UsuarioService(DitadoDbContext context, PasswordHasher passwordHasher, TokenService tokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request)
    {
        // Validação adicional de email (caso Data Annotations sejam ignoradas)
        if (!new EmailAddressAttribute().IsValid(request.Login))
            throw new InvalidOperationException("Login deve ser um email válido.");

        // Validação de unicidade
        if (await _context.Usuarios.AnyAsync(u => u.Login == request.Login))
            throw new InvalidOperationException("Login já está em uso.");

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Login = request.Login.ToLowerInvariant(),
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Matricula = request.Matricula,
            Tipo = request.Tipo,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var loginNormalizado = request.Login.ToLowerInvariant();
        
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Login == loginNormalizado && u.Ativo);

        if (usuario == null || !_passwordHasher.Verify(request.Senha, usuario.SenhaHash))
            return null;

        usuario.DataUltimoAcesso = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = _tokenService.GerarToken(usuario);

        return new LoginResponse
        {
            Token = token,
            Usuario = MapearParaResponse(usuario)
        };
    }

    public async Task<UsuarioResponse?> ObterPorIdAsync(int id)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == id);

        return usuario == null ? null : MapearParaResponse(usuario);
    }

    public async Task<List<UsuarioResponse>> ListarUsuariosAsync()
    {
        return await _context.Usuarios
            .OrderBy(u => u.Nome)
            .Select(u => MapearParaResponse(u))
            .ToListAsync();
    }

    public async Task<UsuarioResponse?> AtualizarUsuarioAsync(int id, AtualizarUsuarioRequest request)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Nome))
            usuario.Nome = request.Nome;

        if (request.Matricula != null) // Permite limpar matrícula enviando string vazia
            usuario.Matricula = string.IsNullOrWhiteSpace(request.Matricula) ? null : request.Matricula;

        if (!string.IsNullOrWhiteSpace(request.SenhaAtual) && !string.IsNullOrWhiteSpace(request.SenhaNova))
        {
            if (!_passwordHasher.Verify(request.SenhaAtual, usuario.SenhaHash))
                throw new InvalidOperationException("Senha atual incorreta.");

            usuario.SenhaHash = _passwordHasher.Hash(request.SenhaNova);
        }

        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    public async Task<bool> BloquearUsuarioAsync(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return false;

        usuario.Ativo = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DesbloquearUsuarioAsync(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return false;

        usuario.Ativo = true;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeletarUsuarioAsync(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return false;

        _context.Usuarios.Remove(usuario);
        await _context.SaveChangesAsync();

        return true;
    }

    private static UsuarioResponse MapearParaResponse(Usuario usuario)
    {
        return new UsuarioResponse
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Login = usuario.Login,
            Matricula = usuario.Matricula,
            Tipo = usuario.Tipo.ToString(),
            Ativo = usuario.Ativo,
            DataCriacao = usuario.DataCriacao,
            DataUltimoAcesso = usuario.DataUltimoAcesso
        };
    }
}