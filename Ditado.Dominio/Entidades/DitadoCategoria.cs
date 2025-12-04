namespace Ditado.Dominio.Entidades;

public class DitadoCategoria
{
    public int DitadoId { get; set; }
    public Ditado Ditado { get; set; } = null!;
    
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    
    public DateTime DataAssociacao { get; set; } = DateTime.UtcNow;
}