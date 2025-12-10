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

            // Erros de PRIMEIRA LETRA
            TipoErro.SupressaoInicio => "Supressão da PRIMEIRA letra",
            TipoErro.AcrescimoInicio => "Acréscimo de letra INICIAL",
            TipoErro.TrocaInicio => "Troca da PRIMEIRA letra",
            TipoErro.IrregularidadeHInicio => "Irregularidade (H INICIAL)",
            TipoErro.ContextualSCInicio => "Erro contextual (S/C INICIAL antes de E/I)",

            // Erros de ÚLTIMA LETRA
            TipoErro.SupressaoFim => "Supressão da ÚLTIMA letra",
            TipoErro.AcrescimoFim => "Acréscimo de letra FINAL",
            TipoErro.TrocaFim => "Troca da ÚLTIMA letra",
            TipoErro.InversaoFim => "Inversão das duas ÚLTIMAS letras",
            TipoErro.HFinalIndevido => "Uso indevido de H FINAL",
            TipoErro.ConfusaoSZ => "Confusão entre S e Z FINAL",
            TipoErro.ConfusaoSSS => "Confusão entre S e SS FINAL",
            TipoErro.ConfusaoSC => "Confusão entre S e Ç FINAL",
            TipoErro.ConfusaoSX => "Confusão entre S e X FINAL",

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

            // Erros de PRIMEIRA LETRA
            TipoErro.SupressaoInicio => "Supressão (início)",
            TipoErro.AcrescimoInicio => "Acréscimo (início)",
            TipoErro.TrocaInicio => "Troca (início)",
            TipoErro.IrregularidadeHInicio => "H inicial",
            TipoErro.ContextualSCInicio => "S/C inicial",

            // Erros de ÚLTIMA LETRA
            TipoErro.SupressaoFim => "Supressão (fim)",
            TipoErro.AcrescimoFim => "Acréscimo (fim)",
            TipoErro.TrocaFim => "Troca (fim)",
            TipoErro.InversaoFim => "Inversão (fim)",
            TipoErro.HFinalIndevido => "H final indevido",
            TipoErro.ConfusaoSZ => "S/Z",
            TipoErro.ConfusaoSSS => "S/SS",
            TipoErro.ConfusaoSC => "S/Ç",
            TipoErro.ConfusaoSX => "S/X",

            _ => "Desconhecido"
        };
    }
}