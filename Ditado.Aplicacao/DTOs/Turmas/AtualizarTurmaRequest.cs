using System.ComponentModel.DataAnnotations;

namespace Ditado.Aplicacao.DTOs.Turmas;

public class AtualizarTurmaRequest
{
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Nome deve ter entre 3 e 100 caracteres.")]
    public string? Nome { get; set; }

    [Range(1, 9, ErrorMessage = "Série deve ser de 1 a 9.")]
    public int? Serie { get; set; }

    [Range(2000, 2100, ErrorMessage = "Ano letivo inválido.")]
    public int? AnoLetivo { get; set; }

    [StringLength(16, ErrorMessage = "Semestre não pode exceder 16 caracteres.")]
    public string? Semestre { get; set; }

    [StringLength(500, ErrorMessage = "Descrição não pode exceder 500 caracteres.")]
    public string? Descricao { get; set; }

    public int? ProfessorResponsavelId { get; set; }

    public List<int>? AlunosIds { get; set; }

    public bool? Ativo { get; set; }
}