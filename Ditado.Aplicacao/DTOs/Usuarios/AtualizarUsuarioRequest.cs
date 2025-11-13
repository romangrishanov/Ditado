namespace Ditado.Aplicacao.DTOs.Usuarios;

public class AtualizarUsuarioRequest
{
    public string? Nome { get; set; }
    public string? SenhaAtual { get; set; }
    public string? SenhaNova { get; set; }
}