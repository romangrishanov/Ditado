namespace Ditado.Dominio.Entidades;

public class RespostaDitado
{
    public int Id { get; set; }
    public int DitadoId { get; set; }
    public int AlunoId { get; set; }
    public DateTime DataRealizacao { get; set; } = DateTime.UtcNow;
    public decimal Nota { get; set; }
    
    public Ditado Ditado { get; set; } = null!;
    public Usuario Aluno { get; set; } = null!;
    public ICollection<RespostaSegmento> RespostasSegmentos { get; set; } = new List<RespostaSegmento>();
}