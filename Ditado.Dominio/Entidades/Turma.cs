namespace Ditado.Dominio.Entidades;

public class Turma
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty; // Ex: "5º Ano A"
    public int Serie { get; set; } // 1 a 11
    public int AnoLetivo { get; set; } // Ex: 2024
    public string? Semestre { get; set; } // Ex: "1º Semestre", "2024.1", opcional até 16 chars
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    // Relacionamentos
    public int ProfessorResponsavelId { get; set; }
    public Usuario ProfessorResponsavel { get; set; } = null!;
    
    public ICollection<Usuario> Alunos { get; set; } = new List<Usuario>();
}