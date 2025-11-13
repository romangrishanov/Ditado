using Ditado.Dominio.Enums;

namespace Ditado.Aplicacao.DTOs.Usuarios;

public class CriarUsuarioRequest
{
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public TipoUsuario Tipo { get; set; }
}