namespace Ditado.Dominio.Enums;

public enum TipoErro
{
    Nenhum = 0,
    Ortografico = 1,
    Acentuacao = 2,
    Omissao = 3, // Resposta vazia ou muito diferente
    
    // Erros de PRIMEIRA LETRA
    SupressaoInicio = 4,
    AcrescimoInicio = 5,
    TrocaInicio = 6,
    IrregularidadeHInicio = 7,
    ContextualSCInicio = 8,
    
    // Erros de ÚLTIMA LETRA (prioridade na detecção)
    SupressaoFim = 9,
    AcrescimoFim = 10,
    TrocaFim = 11,
    InversaoFim = 12,
    HFinalIndevido = 13,
    ConfusaoSZ = 14,
    ConfusaoSSS = 15,
    ConfusaoSC = 16,
    ConfusaoSX = 17
}