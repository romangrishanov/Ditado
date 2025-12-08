namespace Ditado.Dominio.Extensions;

using Ditado.Dominio.Enums;

public static class TipoErroExtensions
{
    /// <summary>
    /// Converte o enum TipoErro para uma descrição amigável em português
    /// </summary>
    public static string ObterDescricao(this TipoErro tipoErro)
    {
        return tipoErro switch
        {
            TipoErro.Nenhum => "Nenhum erro",
            TipoErro.Ortografico => "Erro ortográfico",
            TipoErro.Acentuacao => "Erro de acentuação",
            TipoErro.Omissao => "Omissão de letra(s)",
            TipoErro.Troca => "Troca da primeira letra",
            TipoErro.Acrescimo => "Acréscimo de letra inicial",
            TipoErro.Supressao => "Supressão da primeira letra",
            TipoErro.Irregularidade => "Irregularidade (H inicial)",
            TipoErro.Contextual => "Erro contextual (S/C antes de E/I)",
            TipoErro.TrocaLetra => "Troca de letra",
            _ => "Erro desconhecido"
        };
    }

    /// <summary>
    /// Converte o enum TipoErro para uma descrição curta
    /// </summary>
    public static string ObterDescricaoCurta(this TipoErro tipoErro)
    {
        return tipoErro switch
        {
            TipoErro.Nenhum => "OK",
            TipoErro.Ortografico => "Ortografia",
            TipoErro.Acentuacao => "Acentuação",
            TipoErro.Omissao => "Omissão",
            TipoErro.Troca => "Troca",
            TipoErro.Acrescimo => "Acréscimo",
            TipoErro.Supressao => "Supressão",
            TipoErro.Irregularidade => "Irregularidade",
            TipoErro.Contextual => "Contextual",
            TipoErro.TrocaLetra => "Troca",
            _ => "Desconhecido"
        };
    }
}