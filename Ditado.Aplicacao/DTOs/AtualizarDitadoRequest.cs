namespace Ditado.Aplicacao.DTOs;

public class AtualizarDitadoRequest
{
    public string? Titulo { get; set; }
    public string? Descricao { get; set; }
    public bool? Ativo { get; set; }
    public List<int>? CategoriaIds { get; set; }
}