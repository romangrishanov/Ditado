namespace Ditado.Aplicacao.DTOs.Usuarios;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UsuarioResponse Usuario { get; set; } = null!;
}