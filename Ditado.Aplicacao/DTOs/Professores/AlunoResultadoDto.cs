namespace Ditado.Aplicacao.DTOs.Professores;

public class AlunoResultadoDto
{
    public int AlunoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Matricula { get; set; }
    public bool Fez { get; set; }
    public DateTime? DataEntrega { get; set; } // Data da 1ª tentativa
    public decimal? Nota { get; set; } // Nota da 1ª tentativa
    public string? ErroMaisComum { get; set; } // Descrição do erro mais frequente
    public bool Atrasado { get; set; } // Se entregou depois da data limite
}