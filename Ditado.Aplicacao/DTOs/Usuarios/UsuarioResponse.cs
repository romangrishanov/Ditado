namespace Ditado.Aplicacao.DTOs.Usuarios;

public class UsuarioResponse
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataUltimoAcesso { get; set; }
}