namespace Ditado.Aplicacao.DTOs.Alunos;

public class DitadoAlunoResponse
{
	public int DitadoId { get; set; }
	public string Titulo { get; set; } = string.Empty;
	public string? Descricao { get; set; }
	public DateTime DataLimite { get; set; }
	public bool Atrasado { get; set; }
	public List<TurmaAtribuicaoDto> Turmas { get; set; } = new();
	public StatusDitadoDto Status { get; set; } = new();
	public List<string> CategoriasNomes { get; set; } = new();
}

public class TurmaAtribuicaoDto
{
	public int TurmaId { get; set; }
	public string TurmaNome { get; set; } = string.Empty;
	public DateTime DataAtribuicao { get; set; }
}

public class StatusDitadoDto
{
	public bool JaTentou { get; set; }
	public int Tentativas { get; set; }
	public DateTime? UltimaTentativaEm { get; set; }
	public decimal? MelhorNota { get; set; }
}