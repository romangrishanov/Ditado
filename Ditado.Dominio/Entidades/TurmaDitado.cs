namespace Ditado.Dominio.Entidades;

public class TurmaDitado
{
    public int TurmaId { get; set; }
    public Turma Turma { get; set; } = null!;
    
    public int DitadoId { get; set; }
    public Ditado Ditado { get; set; } = null!;
    
    public DateTime DataAtribuicao { get; set; } = DateTime.UtcNow;
    public DateTime DataLimite { get; set; }
}