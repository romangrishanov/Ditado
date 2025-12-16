using Ditado.Aplicacao.Services;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ditado.Testes.Services;

public class ProfessorServiceTests : IDisposable
{
    private readonly DitadoDbContext _context;
    private readonly ProfessorService _professorService;
    private readonly PasswordHasher _passwordHasher;

    public ProfessorServiceTests()
    {
        var options = new DbContextOptionsBuilder<DitadoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new DitadoDbContext(options);
        _passwordHasher = new PasswordHasher();
        _professorService = new ProfessorService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<Usuario> CriarUsuarioAsync(TipoUsuario tipo, string nome = "Usuário Teste")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Login = $"{tipo.ToString().ToLower()}{Guid.NewGuid()}@teste.com",
            SenhaHash = _passwordHasher.Hash("senha123"),
            Tipo = tipo,
            Ativo = true
        };
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        return usuario;
    }

    private async Task<Turma> CriarTurmaAsync(int professorId, string nome = "5º Ano A")
    {
        var turma = new Turma
        {
            Nome = nome,
            Serie = 5,
            AnoLetivo = 2024,
            ProfessorResponsavelId = professorId,
            Ativo = true
        };
        _context.Turmas.Add(turma);
        await _context.SaveChangesAsync();
        return turma;
    }

    private async Task AdicionarAlunoNaTurmaAsync(int alunoId, int turmaId)
    {
        var aluno = await _context.Usuarios.FindAsync(alunoId);
        var turma = await _context.Turmas.Include(t => t.Alunos).FirstAsync(t => t.Id == turmaId);
        turma.Alunos.Add(aluno!);
        await _context.SaveChangesAsync();
    }

    private async Task<Dominio.Entidades.Ditado> CriarDitadoAsync(int autorId, string titulo = "Ditado Teste")
    {
        var ditado = new Dominio.Entidades.Ditado
        {
            Titulo = titulo,
            AudioLeitura = new byte[] { 1, 2, 3 },
            AutorId = autorId,
            Ativo = true
        };

        // Adicionar segmentos para o ditado
        ditado.Segmentos.Add(new DitadoSegmento
        {
            Ordem = 1,
            Tipo = TipoSegmento.Texto,
            Conteudo = "O "
        });
        ditado.Segmentos.Add(new DitadoSegmento
        {
            Ordem = 2,
            Tipo = TipoSegmento.Lacuna,
            Conteudo = "gato"
        });

        _context.Ditados.Add(ditado);
        await _context.SaveChangesAsync();
        return ditado;
    }

    private async Task AtribuirDitadoAsync(int turmaId, int ditadoId, DateTime dataLimite)
    {
        var turmaDitado = new TurmaDitado
        {
            TurmaId = turmaId,
            DitadoId = ditadoId,
            DataAtribuicao = DateTime.UtcNow,
            DataLimite = dataLimite
        };
        _context.TurmaDitados.Add(turmaDitado);
        await _context.SaveChangesAsync();
    }

    private async Task<RespostaDitado> CriarRespostaDitadoAsync(int ditadoId, int alunoId, decimal nota, params TipoErro[] erros)
    {
        var ditado = await _context.Ditados.Include(d => d.Segmentos).FirstAsync(d => d.Id == ditadoId);
        var lacunas = ditado.Segmentos.Where(s => s.Tipo == TipoSegmento.Lacuna).ToList();

        var resposta = new RespostaDitado
        {
            DitadoId = ditadoId,
            AlunoId = alunoId,
            DataRealizacao = DateTime.UtcNow,
            Nota = nota
        };

        // Criar respostas de segmentos com os erros especificados
        for (int i = 0; i < lacunas.Count; i++)
        {
            var correto = i >= erros.Length || erros[i] == TipoErro.Nenhum;
            resposta.RespostasSegmentos.Add(new RespostaSegmento
            {
                SegmentoId = lacunas[i].Id,
                RespostaFornecida = correto ? lacunas[i].Conteudo : "erro",
                Correto = correto,
                TipoErro = correto ? null : erros[i]
            });
        }

        _context.RespostaDitados.Add(resposta);
        await _context.SaveChangesAsync();
        return resposta;
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_ProfessorSemTurmas_DeveRetornarListaVazia()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_TurmaSemDitados_DeveRetornarListaVazia()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        await CriarTurmaAsync(professor.Id);

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_ComDitadoAtribuido_DeveRetornarResumo()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var ditado = await CriarDitadoAsync(professor.Id, "Ortografia Básica");
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Single(resultado);
        Assert.Equal(turma.Id, resultado[0].TurmaId);
        Assert.Equal(ditado.Id, resultado[0].DitadoId);
        Assert.Equal("Ortografia Básica", resultado[0].DitadoTitulo);
        Assert.Equal(0, resultado[0].TotalAlunos);
        Assert.Equal(0, resultado[0].AlunosQueFizeram);
        Assert.Equal(0, resultado[0].PercentualConclusao);
        Assert.Null(resultado[0].NotaMedia);
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_ComAlunosNaTurma_DeveCalcularPercentualCorreto()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno1 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 1");
        var aluno2 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 2");
        var aluno3 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 3");

        await AdicionarAlunoNaTurmaAsync(aluno1.Id, turma.Id);
        await AdicionarAlunoNaTurmaAsync(aluno2.Id, turma.Id);
        await AdicionarAlunoNaTurmaAsync(aluno3.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Apenas 2 alunos fizeram (primeira tentativa)
        await CriarRespostaDitadoAsync(ditado.Id, aluno1.Id, 80.0m);
        await CriarRespostaDitadoAsync(ditado.Id, aluno2.Id, 90.0m);

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Single(resultado);
        Assert.Equal(3, resultado[0].TotalAlunos);
        Assert.Equal(2, resultado[0].AlunosQueFizeram);
        Assert.Equal(66.67m, resultado[0].PercentualConclusao); // 2/3 * 100 = 66.67
        Assert.Equal(85.0m, resultado[0].NotaMedia); // (80 + 90) / 2 = 85
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_ApenasConsideraPrimeiraTentativa()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
        await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Primeira tentativa: 60
        await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 60.0m);
        await Task.Delay(100); // Garantir ordem temporal

        // Segunda tentativa: 80 (NÃO deve ser considerada)
        await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 80.0m);

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Equal(60.0m, resultado[0].NotaMedia); // Apenas primeira tentativa
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_OrdenaPorDataLimiteAsc()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var ditado1 = await CriarDitadoAsync(professor.Id, "Ditado 1");
        var ditado2 = await CriarDitadoAsync(professor.Id, "Ditado 2");
        var ditado3 = await CriarDitadoAsync(professor.Id, "Ditado 3");

        // Atribuir com datas limite diferentes
        await AtribuirDitadoAsync(turma.Id, ditado1.Id, DateTime.UtcNow.AddDays(10)); // Mais distante
        await AtribuirDitadoAsync(turma.Id, ditado2.Id, DateTime.UtcNow.AddDays(-2)); // Vencido
        await AtribuirDitadoAsync(turma.Id, ditado3.Id, DateTime.UtcNow.AddDays(3)); // Próximo

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.Equal(3, resultado.Count);
        Assert.Equal("Ditado 2", resultado[0].DitadoTitulo); // Vencido primeiro
        Assert.Equal("Ditado 3", resultado[1].DitadoTitulo); // Próximo
        Assert.Equal("Ditado 1", resultado[2].DitadoTitulo); // Mais distante
    }

    [Fact]
    public async Task ListarMeusDitadosAtribuidos_MarcaDitadoComoVencido()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(-1)); // Vencido

        // Act
        var resultado = await _professorService.ListarMeusDitadosAtribuidosAsync(professor.Id);

        // Assert
        Assert.True(resultado[0].Vencido);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_TurmaNaoPertenceAoProfessor_DeveRetornarNull()
    {
        // Arrange
        var professor1 = await CriarUsuarioAsync(TipoUsuario.Professor);
        var professor2 = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor2.Id);
        var ditado = await CriarDitadoAsync(professor2.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor1.Id);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_AtribuicaoNaoExiste_DeveRetornarNull()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var ditado = await CriarDitadoAsync(professor.Id);

        // Act (sem atribuir ditado)
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ComDadosValidos_DeveRetornarDetalhes()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id, "5º Ano A");
        var aluno1 = await CriarUsuarioAsync(TipoUsuario.Aluno, "João Silva");
        aluno1.Matricula = "2024001";
        await AdicionarAlunoNaTurmaAsync(aluno1.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id, "Ortografia");
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        await CriarRespostaDitadoAsync(ditado.Id, aluno1.Id, 85.0m, TipoErro.Acentuacao);

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("5º Ano A", resultado.TurmaNome);
        Assert.Equal("Ortografia", resultado.DitadoTitulo);
        Assert.Equal(1, resultado.TotalAlunos);
        Assert.Equal(1, resultado.AlunosQueFizeram);
        Assert.Equal(100.0m, resultado.PercentualConclusao);
        Assert.Equal(85.0m, resultado.NotaMedia);

        Assert.Single(resultado.Alunos);
        Assert.Equal("João Silva", resultado.Alunos[0].Nome);
        Assert.Equal("2024001", resultado.Alunos[0].Matricula);
        Assert.True(resultado.Alunos[0].Fez);
        Assert.Equal(85.0m, resultado.Alunos[0].Nota);
        Assert.NotNull(resultado.Alunos[0].ErroMaisComum);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ComAlunoQueNaoFez_DeveMostrarPendente()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno, "Maria Santos");
        await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Act (aluno não fez)
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.Single(resultado!.Alunos);
        Assert.False(resultado.Alunos[0].Fez);
        Assert.Null(resultado.Alunos[0].DataEntrega);
        Assert.Null(resultado.Alunos[0].Nota);
        Assert.Null(resultado.Alunos[0].ErroMaisComum);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ComEntregaAtrasada_Devemarcar()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
        await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        var dataLimite = DateTime.UtcNow.AddDays(-2); // Limite há 2 dias atrás
        await AtribuirDitadoAsync(turma.Id, ditado.Id, dataLimite);

        // Aluno fez hoje (atrasado)
        var resposta = await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 70.0m);
        resposta.DataRealizacao = DateTime.UtcNow; // Hoje (depois do limite)
        await _context.SaveChangesAsync();

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.True(resultado!.Alunos[0].Atrasado);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_DeveAgregarErrosPorTipo()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno1 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 1");
        var aluno2 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 2");
        await AdicionarAlunoNaTurmaAsync(aluno1.Id, turma.Id);
        await AdicionarAlunoNaTurmaAsync(aluno2.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Aluno 1: erro de acentuação
        await CriarRespostaDitadoAsync(ditado.Id, aluno1.Id, 0m, TipoErro.Acentuacao);

        // Aluno 2: erro de acentuação também
        await CriarRespostaDitadoAsync(ditado.Id, aluno2.Id, 0m, TipoErro.Acentuacao);

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotEmpty(resultado!.ErrosPorTipo);
        var erroAcentuacao = resultado.ErrosPorTipo.First(e => e.TipoErroId == (int)TipoErro.Acentuacao);
        Assert.Equal(2, erroAcentuacao.Quantidade); // 2 alunos erraram acentuação
        Assert.Equal("Erro de acentuação", erroAcentuacao.Descricao);
        Assert.Equal("Acentuação", erroAcentuacao.DescricaoCurta);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ErrosOrdenadosPorQuantidade()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno1 = await CriarUsuarioAsync(TipoUsuario.Aluno);
        await AdicionarAlunoNaTurmaAsync(aluno1.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        
        // Adicionar mais lacunas ANTES de usar o ditado
        ditado.Segmentos.Add(new DitadoSegmento { DitadoId = ditado.Id, Ordem = 3, Tipo = TipoSegmento.Lacuna, Conteudo = "casa" });
        ditado.Segmentos.Add(new DitadoSegmento { DitadoId = ditado.Id, Ordem = 4, Tipo = TipoSegmento.Lacuna, Conteudo = "sol" });
        ditado.Segmentos.Add(new DitadoSegmento { DitadoId = ditado.Id, Ordem = 5, Tipo = TipoSegmento.Lacuna, Conteudo = "lua" });
        await _context.SaveChangesAsync();

        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Aluno 1: 3 erros de acentuação (lacunas 0, 1, 2), 1 ortográfico (lacuna 3), 1 acerto (lacuna 4)
        await CriarRespostaDitadoAsync(ditado.Id, aluno1.Id, 20m, 
            TipoErro.Acentuacao,   // Lacuna 1
            TipoErro.Acentuacao,   // Lacuna 2
            TipoErro.Acentuacao,   // Lacuna 3
            TipoErro.Ortografico,  // Lacuna 4
            TipoErro.Nenhum);      // Lacuna 5 (acerto)

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.ErrosPorTipo.Count); // 2 tipos de erro (Acentuacao e Ortografico)
        Assert.Equal((int)TipoErro.Acentuacao, resultado.ErrosPorTipo[0].TipoErroId); // Mais frequente primeiro
        Assert.Equal(3, resultado.ErrosPorTipo[0].Quantidade);
        Assert.Equal((int)TipoErro.Ortografico, resultado.ErrosPorTipo[1].TipoErroId);
        Assert.Equal(1, resultado.ErrosPorTipo[1].Quantidade);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_DeveCalcularErrosPorPalavra()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno1 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 1");
        var aluno2 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 2");
        var aluno3 = await CriarUsuarioAsync(TipoUsuario.Aluno, "Aluno 3");
        
        await AdicionarAlunoNaTurmaAsync(aluno1.Id, turma.Id);
        await AdicionarAlunoNaTurmaAsync(aluno2.Id, turma.Id);
        await AdicionarAlunoNaTurmaAsync(aluno3.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        
        // Adicionar mais lacunas
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 3, 
            Tipo = TipoSegmento.Lacuna, 
            Conteudo = "cachorro" 
        });
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 4, 
            Tipo = TipoSegmento.Lacuna, 
            Conteudo = "árvore" 
        });
        await _context.SaveChangesAsync();

        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Aluno 1: erra "gato" e "árvore"
        await CriarRespostaDitadoAsync(ditado.Id, aluno1.Id, 33.33m, 
            TipoErro.Ortografico,  // "gato"
            TipoErro.Nenhum,       // "cachorro" (acerto)
            TipoErro.Acentuacao);  // "árvore"

        // Aluno 2: erra "gato" e "cachorro"
        await CriarRespostaDitadoAsync(ditado.Id, aluno2.Id, 33.33m, 
            TipoErro.Ortografico,  // "gato"
            TipoErro.Ortografico,  // "cachorro"
            TipoErro.Nenhum);      // "árvore" (acerto)

        // Aluno 3: acerta tudo
        await CriarRespostaDitadoAsync(ditado.Id, aluno3.Id, 100m, 
            TipoErro.Nenhum,       // "gato"
            TipoErro.Nenhum,       // "cachorro"
            TipoErro.Nenhum);      // "árvore"

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(3, resultado.ErrosPorPalavra.Count); // 3 lacunas

        // Primeira palavra: "gato" - 2 alunos erraram de 3 = 66.67%
        var palavraGato = resultado.ErrosPorPalavra[0];
        Assert.Equal("gato", palavraGato.Palavra);
        Assert.Equal(2, palavraGato.QuantidadeErros);
        Assert.Equal(66.67m, palavraGato.PercentualErro);

        // Segunda palavra: "cachorro" - 1 aluno errou de 3 = 33.33%
        var palavraCachorro = resultado.ErrosPorPalavra[1];
        Assert.Equal("cachorro", palavraCachorro.Palavra);
        Assert.Equal(1, palavraCachorro.QuantidadeErros);
        Assert.Equal(33.33m, palavraCachorro.PercentualErro);

        // Terceira palavra: "árvore" - 1 aluno errou de 3 = 33.33%
        var palavraArvore = resultado.ErrosPorPalavra[2];
        Assert.Equal("árvore", palavraArvore.Palavra);
        Assert.Equal(1, palavraArvore.QuantidadeErros);
        Assert.Equal(33.33m, palavraArvore.PercentualErro);
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ErrosPorPalavra_ApenasConsideraPrimeiraTentativa()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
        await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Primeira tentativa: erro na palavra "gato"
        await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 0m, TipoErro.Ortografico);
        await Task.Delay(100);

        // Segunda tentativa: acertou "gato" (NÃO deve ser considerada)
        await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 100m, TipoErro.Nenhum);

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Single(resultado.ErrosPorPalavra);
        
        var palavraGato = resultado.ErrosPorPalavra[0];
        Assert.Equal("gato", palavraGato.Palavra);
        Assert.Equal(1, palavraGato.QuantidadeErros); // Considerou apenas 1ª tentativa (erro)
        Assert.Equal(100.0m, palavraGato.PercentualErro); // 1 de 1 = 100%
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ErrosPorPalavra_SemAlunos_DeveRetornarZero()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var ditado = await CriarDitadoAsync(professor.Id);
        
        // Adicionar mais uma lacuna
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 3, 
            Tipo = TipoSegmento.Lacuna, 
            Conteudo = "casa" 
        });
        await _context.SaveChangesAsync();

        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));

        // Act (nenhum aluno fez)
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.ErrosPorPalavra.Count); // 2 lacunas

        foreach (var erro in resultado.ErrosPorPalavra)
        {
            Assert.Equal(0, erro.QuantidadeErros);
            Assert.Equal(0m, erro.PercentualErro);
        }
    }

    [Fact]
    public async Task ObterDetalhesAtribuicao_ErrosPorPalavra_OrdenadoPorOrdemDoDitado()
    {
        // Arrange
        var professor = await CriarUsuarioAsync(TipoUsuario.Professor);
        var turma = await CriarTurmaAsync(professor.Id);
        var aluno = await CriarUsuarioAsync(TipoUsuario.Aluno);
        await AdicionarAlunoNaTurmaAsync(aluno.Id, turma.Id);

        var ditado = await CriarDitadoAsync(professor.Id);
        
        // Adicionar lacunas com ordens específicas
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 3, 
            Tipo = TipoSegmento.Texto, 
            Conteudo = " e o " 
        });
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 4, 
            Tipo = TipoSegmento.Lacuna, 
            Conteudo = "cachorro" // Segunda lacuna
        });
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 5, 
            Tipo = TipoSegmento.Texto, 
            Conteudo = " na " 
        });
        ditado.Segmentos.Add(new DitadoSegmento 
        { 
            DitadoId = ditado.Id, 
            Ordem = 6, 
            Tipo = TipoSegmento.Lacuna, 
            Conteudo = "árvore" // Terceira lacuna
        });
        await _context.SaveChangesAsync();

        await AtribuirDitadoAsync(turma.Id, ditado.Id, DateTime.UtcNow.AddDays(7));
    
        await CriarRespostaDitadoAsync(ditado.Id, aluno.Id, 0m, 
            TipoErro.Nenhum, 
            TipoErro.Nenhum, 
            TipoErro.Nenhum);

        // Act
        var resultado = await _professorService.ObterDetalhesAtribuicaoAsync(turma.Id, ditado.Id, professor.Id);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(3, resultado.ErrosPorPalavra.Count);
        
        // Verificar ordem
        Assert.Equal("gato", resultado.ErrosPorPalavra[0].Palavra); // Ordem 2
        Assert.Equal("cachorro", resultado.ErrosPorPalavra[1].Palavra); // Ordem 4
        Assert.Equal("árvore", resultado.ErrosPorPalavra[2].Palavra); // Ordem 6
    }
}