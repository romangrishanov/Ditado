using Ditado.Aplicacao.DTOs.Categorias;
using Ditado.Dominio.Entidades;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Aplicacao.Services;

public class CategoriaService
{
    private readonly DitadoDbContext _context;

    public CategoriaService(DitadoDbContext context)
    {
        _context = context;
    }

    public async Task<CategoriaResponse> CriarCategoriaAsync(CriarCategoriaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new InvalidOperationException("Nome da categoria é obrigatório.");

        var nomeNormalizado = request.Nome.Trim();

        if (await _context.Categorias.AnyAsync(c => c.Nome.ToLower() == nomeNormalizado.ToLower()))
            throw new InvalidOperationException("Já existe uma categoria com este nome.");

        var categoria = new Categoria
        {
            Nome = nomeNormalizado,
            DataCriacao = DateTime.UtcNow
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return MapearParaResponse(categoria);
    }

    public async Task<List<CategoriaResponse>> ListarCategoriasAsync()
    {
        return await _context.Categorias
            .Select(c => new CategoriaResponse
            {
                Id = c.Id,
                Nome = c.Nome,
                DataCriacao = c.DataCriacao,
                TotalDitados = c.DitadoCategorias.Count
            })
            .OrderBy(c => c.Nome)
            .ToListAsync();
    }

    public async Task<CategoriaResponse?> ObterPorIdAsync(int id)
    {
        var categoria = await _context.Categorias
            .Include(c => c.DitadoCategorias)
            .FirstOrDefaultAsync(c => c.Id == id);

        return categoria == null ? null : MapearParaResponse(categoria);
    }

    public async Task<CategoriaResponse?> AtualizarCategoriaAsync(int id, AtualizarCategoriaRequest request)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null)
            return null;

        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new InvalidOperationException("Nome da categoria é obrigatório.");

        var nomeNormalizado = request.Nome.Trim();

        if (await _context.Categorias.AnyAsync(c => c.Id != id && c.Nome.ToLower() == nomeNormalizado.ToLower()))
            throw new InvalidOperationException("Já existe outra categoria com este nome.");

        categoria.Nome = nomeNormalizado;
        await _context.SaveChangesAsync();

        return MapearParaResponse(categoria);
    }

    public async Task<bool> DeletarCategoriaAsync(int id)
    {
        var categoria = await _context.Categorias
            .Include(c => c.DitadoCategorias)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            return false;

        // Remove as associações com ditados primeiro
        _context.DitadoCategorias.RemoveRange(categoria.DitadoCategorias);
        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<int>> ValidarCategoriasAsync(List<int> categoriaIds)
    {
        if (categoriaIds == null || categoriaIds.Count == 0)
            return new List<int>();

        var categoriasExistentes = await _context.Categorias
            .Where(c => categoriaIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        var categoriasInvalidas = categoriaIds.Except(categoriasExistentes).ToList();
        
        if (categoriasInvalidas.Any())
            throw new InvalidOperationException($"Categorias não encontradas: {string.Join(", ", categoriasInvalidas)}");

        return categoriasExistentes;
    }

    private static CategoriaResponse MapearParaResponse(Categoria categoria)
    {
        return new CategoriaResponse
        {
            Id = categoria.Id,
            Nome = categoria.Nome,
            DataCriacao = categoria.DataCriacao,
            TotalDitados = categoria.DitadoCategorias?.Count ?? 0
        };
    }
}