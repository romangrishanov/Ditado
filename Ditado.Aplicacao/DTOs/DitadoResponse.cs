namespace Ditado.Aplicacao.DTOs;

public class DitadoResponse
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime DataCriacao { get; set; }
    public int? AutorId { get; set; }
    public string? AutorNome { get; set; }
    public List<CategoriaSimplificadaDto> Categorias { get; set; } = new();
}

public class CategoriaSimplificadaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
}