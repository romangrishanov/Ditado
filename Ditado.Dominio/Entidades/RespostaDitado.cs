namespace Ditado.Dominio.Entidades;

public class RespostaDitado
{
    public int Id { get; set; }
    public int DitadoId { get; set; }
    public DateTime DataRealizacao { get; set; } = DateTime.UtcNow;
    public decimal Pontuacao { get; set; }
    
    public Ditado Ditado { get; set; } = null!;
    public ICollection<RespostaSegmento> RespostasSegmentos { get; set; } = new List<RespostaSegmento>();
}