using System.ComponentModel.DataAnnotations;
using Ditado.Aplicacao.DTOs.Usuarios;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
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

    // Criar usuário (Admin cria diretamente com tipo específico)
    public async Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request)
    {
        if (!new EmailAddressAttribute().IsValid(request.Login))
            throw new InvalidOperationException("Login deve ser um email válido.");

        if (await _context.Usuarios.AnyAsync(u => u.Login == request.Login.ToLowerInvariant()))
            throw new InvalidOperationException("Login já está em uso.");

        // Exige Tipo quando usado pelo Admin
        if (!request.Tipo.HasValue)
            throw new InvalidOperationException("Tipo de usuário é obrigatório.");

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Login = request.Login.ToLowerInvariant(),
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Matricula = request.Matricula,
            Tipo = request.Tipo.Value,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    public async Task<UsuarioResponse> SolicitarAcessoAsync(CriarUsuarioRequest request)
    {
        if (!new EmailAddressAttribute().IsValid(request.Login))
            throw new InvalidOperationException("Login deve ser um email válido.");

        var loginNormalizado = request.Login.ToLowerInvariant();

        if (await _context.Usuarios.AnyAsync(u => u.Login == loginNormalizado))
            throw new InvalidOperationException("Este email já está cadastrado no sistema.");

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Login = loginNormalizado,
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Matricula = request.Matricula,
            Tipo = TipoUsuario.AcessoSolicitado, // Ignora request.Tipo
            Ativo = false, // Inativo até aprovação
            DataCriacao = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    public async Task<List<UsuarioResponse>> ListarSolicitacoesPendentesAsync()
    {
        return await _context.Usuarios
            .Where(u => u.Tipo == TipoUsuario.AcessoSolicitado && !u.Ativo)
            .OrderBy(u => u.DataCriacao)
            .Select(u => MapearParaResponse(u))
            .ToListAsync();
    }

    public async Task<UsuarioResponse?> AprovarAcessoAsync(int usuarioId, AprovarAcessoRequest request, TipoUsuario tipoAprovador)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        
        if (usuario == null)
            return null;

        if (usuario.Tipo != TipoUsuario.AcessoSolicitado)
            throw new InvalidOperationException("Este usuário não está pendente de aprovação.");

        // Validação: Professor só pode aprovar como Aluno ou Professor
        if (tipoAprovador == TipoUsuario.Professor)
        {
            if (request.NovoTipo != TipoUsuario.Aluno && request.NovoTipo != TipoUsuario.Professor)
                throw new InvalidOperationException("Professores só podem aprovar usuários como Aluno ou Professor.");
        }

        // Validação: Não pode aprovar como AcessoSolicitado
        if (request.NovoTipo == TipoUsuario.AcessoSolicitado)
            throw new InvalidOperationException("Não é possível aprovar usuário com tipo 'AcessoSolicitado'.");

        usuario.Tipo = request.NovoTipo;
        usuario.Ativo = true;

        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var loginNormalizado = request.Login.ToLowerInvariant();
        
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Login == loginNormalizado);

        if (usuario == null || !_passwordHasher.Verify(request.Senha, usuario.SenhaHash))
            return null;

        // Bloquear login de usuários com acesso pendente
        if (usuario.Tipo == TipoUsuario.AcessoSolicitado)
            throw new InvalidOperationException("Seu acesso ainda não foi aprovado. Aguarde a aprovação de um administrador ou professor.");

        // Bloquear login de usuários inativos
        if (!usuario.Ativo)
            throw new InvalidOperationException("Sua conta está inativa. Entre em contato com o administrador.");

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

        if (request.Matricula != null)
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