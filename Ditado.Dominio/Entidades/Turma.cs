namespace Ditado.Dominio.Entidades;

public class Turma
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Serie { get; set; }
    public int AnoLetivo { get; set; }
    public string? Semestre { get; set; }
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public int ProfessorResponsavelId { get; set; }
    public Usuario ProfessorResponsavel { get; set; } = null!;
    
    public ICollection<Usuario> Alunos { get; set; } = new List<Usuario>();
    public ICollection<TurmaDitado> TurmaDitados { get; set; } = new List<TurmaDitado>(); // ADICIONADO
}