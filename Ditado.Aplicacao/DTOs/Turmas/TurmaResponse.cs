using Ditado.Aplicacao.DTOs.Usuarios;

namespace Ditado.Aplicacao.DTOs.Turmas;

public class TurmaResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Serie { get; set; }
    public int AnoLetivo { get; set; }
    public string? Semestre { get; set; }
    public string? Descricao { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }

    public int ProfessorResponsavelId { get; set; }
    public string ProfessorResponsavelNome { get; set; } = string.Empty;

    public int TotalAlunos { get; set; }
    public List<UsuarioResponse> Alunos { get; set; } = new List<UsuarioResponse>();
}