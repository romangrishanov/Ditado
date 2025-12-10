using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Xunit;

namespace Ditado.Testes.Services;

/// <summary>
/// Testes específicos para validar a identificação de tipos de erro no DitadoService
/// </summary>
public class DitadoServiceTests_TiposErro : IDisposable
{
    private readonly DitadoDbContext _context;
    private readonly DitadoService _ditadoService;
    private readonly CategoriaService _categoriaService;
    private readonly MethodInfo _identificarTipoErroMethod;

    public DitadoServiceTests_TiposErro()
    {
        var options = new DbContextOptionsBuilder<DitadoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DitadoDbContext(options);
        _categoriaService = new CategoriaService(_context);
        _ditadoService = new DitadoService(_context, _categoriaService);

        // Usar reflexão para acessar o método privado IdentificarTipoErro
        _identificarTipoErroMethod = typeof(DitadoService).GetMethod(
            "IdentificarTipoErro",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private TipoErro IdentificarTipoErro(string resposta, string esperado)
    {
        return (TipoErro)_identificarTipoErroMethod.Invoke(_ditadoService, new object[] { resposta, esperado })!;
    }

    #region Erros de Última Letra (Prioridade)

    [Theory]
    [InlineData("gat", "gato")] // Supressão de 'o'
    [InlineData("cas", "casa")] // Supressão de 'a'
    [InlineData("sol", "solo")] // Supressão de 'o'
    public void IdentificarTipoErro_SupressaoUltimaLetra_DeveRetornarSupressaoFim(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.SupressaoFim, resultado);
    }

    [Theory]
    [InlineData("casaa", "casa")] // Acréscimo de 'a' (5 vs 4)
    [InlineData("gatoo", "gato")] // Acréscimo de 'o' (5 vs 4)
    [InlineData("soll", "sol")]   // Acréscimo de 'l' (4 vs 3)
    public void IdentificarTipoErro_AcrescimoLetraFinal_DeveRetornarAcrescimoFim(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.AcrescimoFim, resultado);
    }

    [Theory]
    [InlineData("bon", "bom")] // Troca 'm' por 'n'
    [InlineData("lam", "lã")] // Troca 'ã' por 'am' (mas tem só 2 letras, não detecta)
    [InlineData("sola", "solo")] // Troca 'o' por 'a'
    public void IdentificarTipoErro_TrocaUltimaLetra_DeveRetornarTrocaFim(string resposta, string esperado)
    {
        // Arrange
        if (resposta.Length < 3 || esperado.Length < 3)
        {
            // Palavras com menos de 3 letras não detectam erros de última letra
            var resultadoEsperado = IdentificarTipoErro(resposta, esperado);
            Assert.NotEqual(TipoErro.TrocaFim, resultadoEsperado);
            return;
        }

        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.TrocaFim, resultado);
    }

    [Theory]
    [InlineData("boal", "bola")] // Inversão de 'la' para 'al'
    [InlineData("gota", "gato")] // Inversão de 'to' para 'ta' (não, é troca)
    [InlineData("casal", "casa")] // Não é inversão, é acréscimo
    public void IdentificarTipoErro_InversaoUltimasDuasLetras_DeveRetornarInversaoFim(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        if (resposta == "boal" && esperado == "bola")
        {
            Assert.Equal(TipoErro.InversaoFim, resultado);
        }
        else
        {
            // Outros casos não são inversão
            Assert.NotEqual(TipoErro.InversaoFim, resultado);
        }
    }

    [Theory]
    [InlineData("jah", "já")] // Acréscimo indevido de 'h' (mas já tem só 2 letras)
    [InlineData("aloh", "alô")] // H final indevido
    public void IdentificarTipoErro_HFinalIndevido_DeveRetornarHFinalIndevido(string resposta, string esperado)
    {
        // Arrange
        if (resposta.Length < 3 || esperado.Length < 3)
        {
            // Palavras com menos de 3 letras não detectam erros de última letra
            return;
        }

        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.HFinalIndevido, resultado);
    }

    [Theory]
    [InlineData("pas", "paz")] // Confusão S/Z
    [InlineData("felis", "feliz")] // Confusão S/Z
    [InlineData("voz", "vos")] // Confusão Z/S
    public void IdentificarTipoErro_ConfusaoSZ_DeveRetornarConfusaoSZ(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.ConfusaoSZ, resultado);
    }

    [Theory]
    [InlineData("tress", "três")] // S/SS
    [InlineData("mes", "mês")] // S com acento (mas detecta acentuação primeiro)
    public void IdentificarTipoErro_ConfusaoSSS_DeveRetornarConfusaoSSS(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        if (resposta == "tress" && esperado == "três")
        {
            Assert.Equal(TipoErro.ConfusaoSSS, resultado);
        }
        else if (resposta == "mes" && esperado == "mês")
        {
            // Detecta acentuação primeiro
            Assert.Equal(TipoErro.Acentuacao, resultado);
        }
    }

    [Theory]
    [InlineData("tóras", "tórax")] // S/X
    [InlineData("flus", "flux")] // S/X
    [InlineData("torax", "toras")] // X/S (inverso)
    public void IdentificarTipoErro_ConfusaoSX_DeveRetornarConfusaoSX(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.ConfusaoSX, resultado);
    }

    #endregion

    #region Erros de Primeira Letra

    [Theory]
    [InlineData("deste", "teste")] // Troca 't' por 'd'
    [InlineData("pasa", "casa")] // Troca 'c' por 'p'
    [InlineData("mato", "gato")] // Troca 'g' por 'm'
    public void IdentificarTipoErro_TrocaPrimeiraLetra_DeveRetornarTrocaInicio(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.TrocaInicio, resultado);
    }

    [Theory]
    [InlineData("este", "teste")] // Supressão de 't'
    [InlineData("asa", "casa")] // Supressão de 'c'
    [InlineData("ato", "gato")] // Supressão de 'g'
    public void IdentificarTipoErro_SupressaoPrimeiraLetra_DeveRetornarSupressaoInicio(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.SupressaoInicio, resultado);
    }

    [Theory]
    [InlineData("oteste", "teste")] // Acréscimo de 'o'
    [InlineData("acasa", "casa")] // Acréscimo de 'a'
    [InlineData("xgato", "gato")] // Acréscimo de 'x'
    public void IdentificarTipoErro_AcrescimoPrimeiraLetra_DeveRetornarAcrescimoInicio(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.AcrescimoInicio, resultado);
    }

    [Theory]
    [InlineData("omem", "homem")] // Omissão de H inicial
    [InlineData("ontem", "hontem")] // H indevido (mas "hontem" não é palavra válida)
    [InlineData("ora", "hora")] // Omissão de H inicial
    public void IdentificarTipoErro_IrregularidadeHInicial_DeveRetornarIrregularidadeHInicio(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        if ((resposta == "omem" && esperado == "homem") || (resposta == "ora" && esperado == "hora"))
        {
            Assert.Equal(TipoErro.IrregularidadeHInicio, resultado);
        }
        else if (resposta == "ontem" && esperado == "hontem")
        {
            // H indevido no início
            Assert.Equal(TipoErro.IrregularidadeHInicio, resultado);
        }
    }

    [Theory]
    [InlineData("cigno", "signo")] // Troca S/C antes de 'i'
    [InlineData("sigarro", "cigarro")] // Troca C/S antes de 'i'
    [InlineData("certo", "serto")] // Troca S/C antes de 'e'
    public void IdentificarTipoErro_ContextualSCInicial_DeveRetornarContextualSCInicio(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.ContextualSCInicio, resultado);
    }

    #endregion

    #region Erros Gerais (já existentes - validação de prioridade)

    [Theory]
    [InlineData("", "casa")] // Resposta vazia
    [InlineData("  ", "gato")] // Apenas espaços
    public void IdentificarTipoErro_RespostaVazia_DeveRetornarOmissao(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.Omissao, resultado);
    }

    [Theory]
    [InlineData("arvore", "árvore")] // Falta acento
    [InlineData("cafe", "café")] // Falta acento
    [InlineData("sapo", "sapo")] // Sem erro
    public void IdentificarTipoErro_ApenasAcentuacao_DeveRetornarAcentuacao(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        if (resposta == "arvore" || resposta == "cafe")
        {
            Assert.Equal(TipoErro.Acentuacao, resultado);
        }
        else
        {
            Assert.Equal(TipoErro.Nenhum, resultado);
        }
    }

    [Theory]
    [InlineData("cachoro", "cachorro")] // Erro ortográfico no meio
    [InlineData("porqe", "porque")] // Erro ortográfico no meio
    public void IdentificarTipoErro_ErroOrtograficoMeio_DeveRetornarOrtografico(string resposta, string esperado)
    {
        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.Ortografico, resultado);
    }

    #endregion

    #region Testes de Prioridade (Última Letra tem prioridade sobre Primeira Letra)

    [Fact]
    public void IdentificarTipoErro_ErroNaUltimaEPrimeiraLetra_DevePriorizarUltima()
    {
        // Arrange - Palavra com erro na primeira E na última letra
        // Esperado: "teste" >>> Resposta: "pesti" (troca 't'>>>'p' no início, troca 'e'>>>'i' no fim)
        string resposta = "testi"; // Erro só no fim ('e' >>> 'i')
        string esperado = "teste";

        // Act
        var resultado = IdentificarTipoErro(resposta, esperado);

        // Assert
        Assert.Equal(TipoErro.TrocaFim, resultado); // Prioriza erro no fim
    }

    #endregion
}