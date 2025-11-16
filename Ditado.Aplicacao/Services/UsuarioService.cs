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

    public async Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request)
    {
        if (!new EmailAddressAttribute().IsValid(request.Login))
            throw new InvalidOperationException("Login deve ser um email válido.");

        if (await _context.Usuarios.AnyAsync(u => u.Login == request.Login.ToLowerInvariant()))
            throw new InvalidOperationException("Login já está em uso.");

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
            Tipo = TipoUsuario.AcessoSolicitado,
            Ativo = false,
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

        if (tipoAprovador == TipoUsuario.Professor)
        {
            if (request.NovoTipo != TipoUsuario.Aluno && request.NovoTipo != TipoUsuario.Professor)
                throw new InvalidOperationException("Professores só podem aprovar usuários como Aluno ou Professor.");
        }

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

        if (usuario.Tipo == TipoUsuario.AcessoSolicitado)
            throw new InvalidOperationException("Seu acesso ainda não foi aprovado. Aguarde a aprovação de um administrador ou professor.");

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

    public async Task<UsuarioResponse?> AtualizarUsuarioAsync(int id, AtualizarUsuarioRequest request, int usuarioLogadoId, TipoUsuario tipoUsuarioLogado)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
            return null;

        var isEdicaoPropria = id == usuarioLogadoId;

        // EDIÇÃO PRÓPRIA: Apenas Nome, Senha e Matrícula
        if (isEdicaoPropria)
        {
            if (request.Tipo.HasValue)
                throw new InvalidOperationException("Você não pode alterar seu próprio tipo.");
            
            if (request.Ativo.HasValue)
                throw new InvalidOperationException("Você não pode alterar seu próprio status ativo.");
        }

        // Nome (todos podem alterar próprio ou Admin/Prof podem alterar de outros)
        if (!string.IsNullOrWhiteSpace(request.Nome))
            usuario.Nome = request.Nome;

        // Matrícula (todos podem alterar próprio ou Admin/Prof podem alterar de outros)
        if (request.Matricula != null)
            usuario.Matricula = string.IsNullOrWhiteSpace(request.Matricula) ? null : request.Matricula;

        // Senha (todos podem alterar própria)
        if (!string.IsNullOrWhiteSpace(request.SenhaAtual) && !string.IsNullOrWhiteSpace(request.SenhaNova))
        {
            if (!_passwordHasher.Verify(request.SenhaAtual, usuario.SenhaHash))
                throw new InvalidOperationException("Senha atual incorreta.");

            usuario.SenhaHash = _passwordHasher.Hash(request.SenhaNova);
        }

        // ATIVO (apenas para edição de outros)
        if (!isEdicaoPropria && request.Ativo.HasValue)
        {
            ValidarAlteracaoAtivo(tipoUsuarioLogado, usuario.Tipo);
            usuario.Ativo = request.Ativo.Value;
        }

        // TIPO (apenas para edição de outros)
        if (!isEdicaoPropria && request.Tipo.HasValue)
        {
            ValidarAlteracaoTipo(tipoUsuarioLogado, usuario.Tipo, request.Tipo.Value);
            usuario.Tipo = request.Tipo.Value;
        }

        await _context.SaveChangesAsync();

        return MapearParaResponse(usuario);
    }

    // VALIDAÇÃO: Alteração de Ativo
    private void ValidarAlteracaoAtivo(TipoUsuario tipoLogado, TipoUsuario tipoAlvo)
    {
        // Aluno não pode alterar ninguém
        if (tipoLogado == TipoUsuario.Aluno)
            throw new InvalidOperationException("Alunos não podem alterar status de outros usuários.");

        // Professor só pode alterar Alunos e AcessoSolicitado
        if (tipoLogado == TipoUsuario.Professor)
        {
            if (tipoAlvo != TipoUsuario.Aluno && tipoAlvo != TipoUsuario.AcessoSolicitado)
                throw new InvalidOperationException("Professores só podem alterar status de Alunos e usuários com AcessoSolicitado.");
        }

        // Admin pode alterar qualquer um (sem validação adicional)
    }

    // VALIDAÇÃO: Alteração de Tipo
    private void ValidarAlteracaoTipo(TipoUsuario tipoLogado, TipoUsuario tipoAtual, TipoUsuario tipoNovo)
    {
        // Não pode reduzir para AcessoSolicitado
        if (tipoNovo == TipoUsuario.AcessoSolicitado)
            throw new InvalidOperationException("Não é possível alterar usuário para tipo 'AcessoSolicitado'. Para bloquear acesso, desative o usuário.");

        var hierarquia = new Dictionary<TipoUsuario, int>
        {
            { TipoUsuario.AcessoSolicitado, 0 },
            { TipoUsuario.Aluno, 1 },
            { TipoUsuario.Professor, 2 },
            { TipoUsuario.Administrador, 3 }
        };

        var nivelAtual = hierarquia[tipoAtual];
        var nivelNovo = hierarquia[tipoNovo];
        var isAumento = nivelNovo > nivelAtual;
        var isReducao = nivelNovo < nivelAtual;

        // Aluno não pode alterar tipo de ninguém
        if (tipoLogado == TipoUsuario.Aluno)
            throw new InvalidOperationException("Alunos não podem alterar tipo de outros usuários.");

        // Professor pode aumentar, mas não reduzir
        if (tipoLogado == TipoUsuario.Professor)
        {
            if (isReducao)
                throw new InvalidOperationException("Apenas administradores podem reduzir tipo de usuário.");

            // Professor só pode aumentar Aluno > Professor (não pode criar Admin)
            if (isAumento && tipoNovo == TipoUsuario.Administrador)
                throw new InvalidOperationException("Professores não podem promover usuários a Administrador.");

            // Professor só pode alterar AcessoSolicitado e Alunos
            if (tipoAtual != TipoUsuario.AcessoSolicitado && tipoAtual != TipoUsuario.Aluno)
                throw new InvalidOperationException("Professores só podem alterar tipo de usuários com AcessoSolicitado ou Alunos.");
        }

        // Admin pode fazer qualquer alteração (exceto para AcessoSolicitado, já validado acima)
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