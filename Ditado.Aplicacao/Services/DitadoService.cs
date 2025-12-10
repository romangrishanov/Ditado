using Ditado.Aplicacao.DTOs;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Dominio.Extensions;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Ditado.Aplicacao.Services;

public class DitadoService
{
	private readonly DitadoDbContext _context;
	private readonly CategoriaService _categoriaService;

	public DitadoService(DitadoDbContext context, CategoriaService categoriaService)
	{
		_context = context;
		_categoriaService = categoriaService;
	}

	public async Task<DitadoResponse> CriarDitadoAsync(CriarDitadoRequest request, int autorId)
	{
		var segmentos = ParsearTextoComLacunas(request.TextoComMarcacoes);
		var audioDados = ExtrairAudioDeBase64(request.AudioBase64);

		var ditado = new Dominio.Entidades.Ditado
		{
			Titulo = request.Titulo,
			Descricao = request.Descricao,
			AudioLeitura = audioDados,
			DataCriacao = DateTime.UtcNow,
			Ativo = true,
			AutorId = autorId,
			Segmentos = segmentos
		};

		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		// ADICIONAR: Associar categorias ao ditado
		if (request.CategoriaIds != null && request.CategoriaIds.Any())
		{
			await _categoriaService.ValidarCategoriasAsync(request.CategoriaIds);

			foreach (var categoriaId in request.CategoriaIds.Distinct())
			{
				_context.DitadoCategorias.Add(new DitadoCategoria
				{
					DitadoId = ditado.Id,
					CategoriaId = categoriaId,
					DataAssociacao = DateTime.UtcNow
				});
			}

			await _context.SaveChangesAsync();
		}

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<DitadoParaRealizarResponse?> ObterDitadoParaRealizarAsync(int id)
	{
		var ditado = await _context.Ditados
			.Include(d => d.Segmentos)
			.FirstOrDefaultAsync(d => d.Id == id && d.Ativo);

		if (ditado == null)
			return null;

		var audioBase64 = $"data:{Dominio.Entidades.Ditado.AudioMimeTypePadrao};base64,{Convert.ToBase64String(ditado.AudioLeitura)}";

		return new DitadoParaRealizarResponse
		{
			Id = ditado.Id,
			Titulo = ditado.Titulo,
			AudioBase64 = audioBase64,
			Segmentos = ditado.Segmentos.OrderBy(s => s.Ordem).Select(s => new SegmentoParaRealizarDto
			{
				Ordem = s.Ordem,
				Tipo = s.Tipo.ToString(),
				Conteudo = s.Tipo == TipoSegmento.Texto ? s.Conteudo : null,
				SegmentoId = s.Tipo == TipoSegmento.Lacuna ? s.Id : null
			}).ToList()
		};
	}

	public async Task<ResultadoDitadoResponse?> SubmeterRespostaAsync(int ditadoId, SubmeterRespostaRequest request, int alunoId)
	{
		var ditado = await _context.Ditados
			.Include(d => d.Segmentos)
			.FirstOrDefaultAsync(d => d.Id == ditadoId && d.Ativo);

		if (ditado == null)
			return null;

		var respostaDitado = new RespostaDitado
		{
			DitadoId = ditadoId,
			AlunoId = alunoId,
			DataRealizacao = DateTime.UtcNow
		};

		var detalhes = new List<DetalheRespostaDto>();
		int acertos = 0;
		int totalLacunas = 0;

		foreach (var resposta in request.Respostas)
		{
			var segmento = ditado.Segmentos.FirstOrDefault(s => s.Id == resposta.SegmentoId);
			if (segmento == null || segmento.Tipo != TipoSegmento.Lacuna)
				continue;

			totalLacunas++;
			var correto = NormalizarTexto(resposta.Resposta) == NormalizarTexto(segmento.Conteudo);
			if (correto) acertos++;

			var tipoErro = correto ? TipoErro.Nenhum : IdentificarTipoErro(resposta.Resposta, segmento.Conteudo);

			var respostaSegmento = new RespostaSegmento
			{
				SegmentoId = segmento.Id,
				RespostaFornecida = resposta.Resposta,
				Correto = correto,
				TipoErro = correto ? null : tipoErro
			};

			respostaDitado.RespostasSegmentos.Add(respostaSegmento);

			detalhes.Add(new DetalheRespostaDto
			{
				SegmentoId = segmento.Id,
				RespostaFornecida = resposta.Resposta,
				RespostaEsperada = segmento.Conteudo,
				Correto = correto,
				TipoErro = correto ? null : tipoErro.ObterDescricao()
			});
		}

		respostaDitado.Nota = totalLacunas > 0 ? (decimal)acertos / totalLacunas * 100 : 0;

		_context.RespostaDitados.Add(respostaDitado);
		await _context.SaveChangesAsync();

		return new ResultadoDitadoResponse
		{
			RespostaDitadoId = respostaDitado.Id,
			Nota = respostaDitado.Nota,
			TotalLacunas = totalLacunas,
			Acertos = acertos,
			Erros = totalLacunas - acertos,
			Detalhes = detalhes
		};
	}

	public async Task<List<DitadoResponse>> ListarDitadosAsync()
	{
		return await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.ThenInclude(dc => dc.Categoria)
			.Include(d => d.Autor)
			.Where(d => d.Ativo)
			.OrderByDescending(d => d.DataCriacao)
			.Select(d => new DitadoResponse
			{
				Id = d.Id,
				Titulo = d.Titulo,
				Descricao = d.Descricao,
				DataCriacao = d.DataCriacao,
				AutorId = d.AutorId,
				AutorNome = d.Autor != null ? d.Autor.Nome : null, 
				Categorias = d.DitadoCategorias
					.Select(dc => new CategoriaSimplificadaDto
					{
						Id = dc.Categoria.Id,
						Nome = dc.Categoria.Nome
					})
					.ToList()
			})
			.ToListAsync();
	}

	private async Task<DitadoResponse> ObterDitadoComCategoriasAsync(int ditadoId)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.ThenInclude(dc => dc.Categoria)
			.Include(d => d.Autor)
			.FirstOrDefaultAsync(d => d.Id == ditadoId);

		if (ditado == null)
			throw new InvalidOperationException("Ditado não encontrado.");

		return new DitadoResponse
		{
			Id = ditado.Id,
			Titulo = ditado.Titulo,
			Descricao = ditado.Descricao,
			DataCriacao = ditado.DataCriacao,
			AutorId = ditado.AutorId,
			AutorNome = ditado.Autor?.Nome,
			Categorias = ditado.DitadoCategorias
				.Select(dc => new CategoriaSimplificadaDto
				{
					Id = dc.Categoria.Id,
					Nome = dc.Categoria.Nome
				})
				.ToList()
		};
	}

	public async Task<DitadoResponse?> AtualizarCategoriasAsync(int ditadoId, List<int> categoriaIds)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.FirstOrDefaultAsync(d => d.Id == ditadoId);

		if (ditado == null)
			return null;

		// Validar categorias
		await _categoriaService.ValidarCategoriasAsync(categoriaIds);

		// Remover associações antigas
		_context.DitadoCategorias.RemoveRange(ditado.DitadoCategorias);

		// Adicionar novas associações
		foreach (var categoriaId in categoriaIds.Distinct())
		{
			_context.DitadoCategorias.Add(new DitadoCategoria
			{
				DitadoId = ditado.Id,
				CategoriaId = categoriaId,
				DataAssociacao = DateTime.UtcNow
			});
		}

		await _context.SaveChangesAsync();

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<DitadoResponse?> AtualizarDitadoAsync(int id, AtualizarDitadoRequest request)
	{
		var ditado = await _context.Ditados
			.Include(d => d.DitadoCategorias)
			.FirstOrDefaultAsync(d => d.Id == id);

		if (ditado == null)
			return null;

		// Atualizar título se fornecido
		if (!string.IsNullOrWhiteSpace(request.Titulo))
			ditado.Titulo = request.Titulo;

		// Atualizar descrição se fornecido (permite limpar)
		if (request.Descricao != null)
			ditado.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao;

		// Atualizar status ativo se fornecido
		if (request.Ativo.HasValue)
			ditado.Ativo = request.Ativo.Value;

		// Atualizar categorias se fornecido (permite 0 categorias)
		if (request.CategoriaIds != null)
		{
			// Validar categorias se houver alguma
			if (request.CategoriaIds.Any())
				await _categoriaService.ValidarCategoriasAsync(request.CategoriaIds);

			// Remover associações antigas
			_context.DitadoCategorias.RemoveRange(ditado.DitadoCategorias);

			// Adicionar novas associações (pode ser lista vazia)
			foreach (var categoriaId in request.CategoriaIds.Distinct())
			{
				_context.DitadoCategorias.Add(new DitadoCategoria
				{
					DitadoId = ditado.Id,
					CategoriaId = categoriaId,
					DataAssociacao = DateTime.UtcNow
				});
			}
		}

		await _context.SaveChangesAsync();

		return await ObterDitadoComCategoriasAsync(ditado.Id);
	}

	public async Task<bool> DeletarDitadoAsync(int ditadoId, int usuarioLogadoId, TipoUsuario tipoUsuarioLogado)
	{
		var ditado = await _context.Ditados.FindAsync(ditadoId);

		if (ditado == null)
			return false;

		if (tipoUsuarioLogado != TipoUsuario.Administrador && ditado.AutorId != usuarioLogadoId)
			throw new InvalidOperationException("Apenas o autor do ditado ou um administrador podem excluí-lo.");

		_context.Ditados.Remove(ditado);
		await _context.SaveChangesAsync();

		return true;
	}

	private List<DitadoSegmento> ParsearTextoComLacunas(string texto)
	{
		var segmentos = new List<DitadoSegmento>();
		var regex = new Regex(@"\[([^\]]+)\]");
		int ordem = 1;
		int ultimoIndice = 0;

		foreach (Match match in regex.Matches(texto))
		{
			// Extrai o conteúdo dentro dos colchetes (pode ter espaços/pontuação)
			var conteudoBruto = match.Groups[1].Value;

			// Separa palavra limpa de prefixo/sufixo (espaços e pontuação)
			var (prefixo, palavraLimpa, sufixo) = ExtrairPalavraComContexto(conteudoBruto);

			// Adiciona texto ANTES da lacuna (incluindo o que estava antes dos colchetes + prefixo)
			var textoAntes = texto.Substring(ultimoIndice, match.Index - ultimoIndice) + prefixo;

			if (!string.IsNullOrEmpty(textoAntes))
			{
				segmentos.Add(new DitadoSegmento
				{
					Ordem = ordem++,
					Tipo = TipoSegmento.Texto,
					Conteudo = textoAntes
				});
			}

			// Adiciona a LACUNA com apenas a palavra limpa (preserva maiúscula/minúscula original)
			segmentos.Add(new DitadoSegmento
			{
				Ordem = ordem++,
				Tipo = TipoSegmento.Lacuna,
				Conteudo = palavraLimpa
			});

			// Atualiza índice
			ultimoIndice = match.Index + match.Length;

			// Se tem sufixo, ele será o início do próximo segmento de texto
			if (!string.IsNullOrEmpty(sufixo))
			{
				// Guarda o sufixo para concatenar com o próximo texto
				var proximoTextoInicio = ultimoIndice;
				var proximoTextoFim = ultimoIndice < texto.Length ?
					(regex.Match(texto, ultimoIndice).Success ? regex.Match(texto, ultimoIndice).Index : texto.Length)
					: texto.Length;

				var proximoTexto = proximoTextoInicio < proximoTextoFim ?
					texto.Substring(proximoTextoInicio, proximoTextoFim - proximoTextoInicio) : string.Empty;

				var textoComSufixo = sufixo + proximoTexto;

				if (!string.IsNullOrEmpty(textoComSufixo) && proximoTextoInicio < texto.Length)
				{
					segmentos.Add(new DitadoSegmento
					{
						Ordem = ordem++,
						Tipo = TipoSegmento.Texto,
						Conteudo = textoComSufixo
					});

					// Atualiza índice para pular o texto já processado
					ultimoIndice = proximoTextoFim;
				}
				else if (!string.IsNullOrEmpty(sufixo))
				{
					// Apenas sufixo, sem texto adicional
					segmentos.Add(new DitadoSegmento
					{
						Ordem = ordem++,
						Tipo = TipoSegmento.Texto,
						Conteudo = sufixo
					});
				}
			}
		}

		// Adiciona texto restante após a última lacuna
		if (ultimoIndice < texto.Length)
		{
			segmentos.Add(new DitadoSegmento
			{
				Ordem = ordem,
				Tipo = TipoSegmento.Texto,
				Conteudo = texto.Substring(ultimoIndice)
			});
		}

		return segmentos;
	}

	/// <summary>
	/// Extrai a palavra limpa e separa prefixo/sufixo (espaços e pontuação).
	/// Preserva a capitalização original da palavra.
	/// Exemplo: ", Gato. " > (", ", "Gato", ". ")
	/// </summary>
	private (string prefixo, string palavra, string sufixo) ExtrairPalavraComContexto(string conteudo)
	{
		// Regex que captura: (prefixo) (palavra) (sufixo)
		// Prefixo: espaços e pontuação (incluindo ponto) no início
		// Palavra: letras (com acentos), números, apóstrofo e hífen
		// Sufixo: espaços e pontuação (incluindo ponto) no fim
		var match = Regex.Match(
			conteudo,
			@"^([\s,\.;:!?\-—'""«»]*)([a-zA-ZáàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ'\-]+)([\s,\.;:!?\-—'""«»]*)$"
		);

		if (match.Success)
		{
			return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
		}

		// Fallback: Se não conseguiu fazer match, tenta pelo menos separar espaços
		var trimmed = conteudo.Trim();
		var prefixoEspacos = conteudo.Substring(0, conteudo.IndexOf(trimmed[0]));
		var sufixoEspacos = conteudo.Substring(conteudo.LastIndexOf(trimmed[^1]) + 1);

		return (prefixoEspacos, trimmed, sufixoEspacos);
	}

	private byte[] ExtrairAudioDeBase64(string audioBase64)
	{
		var base64Data = audioBase64.Contains(",") ? audioBase64.Split(',')[1] : audioBase64;
		return Convert.FromBase64String(base64Data);
	}

	private string NormalizarTexto(string texto)
	{
		// ignora maiúsculas/minúsculas
		return texto.Trim().ToLowerInvariant();
	}

	private TipoErro IdentificarTipoErro(string resposta, string esperado)
	{
		var respostaNorm = NormalizarTexto(resposta);
		var esperadoNorm = NormalizarTexto(esperado);
		
		// 1. Verifica omissão completa (resposta vazia)
		if (string.IsNullOrWhiteSpace(respostaNorm))
			return TipoErro.Omissao;

		// 2. Se são iguais (acerto perfeito)
		if (respostaNorm == esperadoNorm)
			return TipoErro.Nenhum;

		// 3. Criar versões sem acentos para comparações de estrutura
		var respostaSemAcento = RemoverAcentos(respostaNorm);
		var esperadoSemAcento = RemoverAcentos(esperadoNorm);

		// === PRIORIDADE: ERROS DE ÚLTIMA LETRA ===
		if (respostaSemAcento.Length >= 3 && esperadoSemAcento.Length >= 3)
		{
			var ultimaLetraResposta = respostaSemAcento[^1];
			var ultimaLetraEsperado = esperadoSemAcento[^1];
			var penultimaLetraResposta = respostaSemAcento.Length >= 2 ? respostaSemAcento[^2] : '\0';
			var penultimaLetraEsperado = esperadoSemAcento.Length >= 2 ? esperadoSemAcento[^2] : '\0';

			// A. Inversão (bola > boal)
			if (respostaSemAcento.Length == esperadoSemAcento.Length &&
				respostaSemAcento.Length >= 3 &&
				ultimaLetraResposta == penultimaLetraEsperado &&
				penultimaLetraResposta == ultimaLetraEsperado &&
				respostaSemAcento[..^2] == esperadoSemAcento[..^2])
			{
				return TipoErro.InversaoFim;
			}

			// B. Supressão (gato > gat)
			if (respostaSemAcento.Length == esperadoSemAcento.Length - 1 &&
				respostaSemAcento == esperadoSemAcento[..^1])
			{
				return TipoErro.SupressaoFim;
			}

			// C. Confusão S/SS (três > tress)
			if (respostaSemAcento.Length == esperadoSemAcento.Length + 1 &&
				ultimaLetraResposta == 's' &&
				penultimaLetraResposta == 's' &&
				ultimaLetraEsperado == 's' &&
				respostaSemAcento[..^2] == esperadoSemAcento[..^1])
			{
				return TipoErro.ConfusaoSSS;
			}
			if (respostaSemAcento.Length == esperadoSemAcento.Length - 1 &&
				ultimaLetraResposta == 's' &&
				ultimaLetraEsperado == 's' &&
				penultimaLetraEsperado == 's' &&
				respostaSemAcento[..^1] == esperadoSemAcento[..^2])
			{
				return TipoErro.ConfusaoSSS;
			}

			// D. H Final Indevido (alô > aloh)
			// ANTES de Acréscimo genérico
			if (respostaSemAcento.Length == esperadoSemAcento.Length + 1 &&
				ultimaLetraResposta == 'h' &&
				respostaSemAcento[..^1] == esperadoSemAcento)
			{
				return TipoErro.HFinalIndevido;
			}

			// E. Acréscimo (casa > casaa)
			if (respostaSemAcento.Length == esperadoSemAcento.Length + 1 &&
				esperadoSemAcento == respostaSemAcento[..^1])
			{
				return TipoErro.AcrescimoFim;
			}

			// F. Troca da última letra (bom > bon)
			if (respostaSemAcento.Length == esperadoSemAcento.Length &&
				ultimaLetraResposta != ultimaLetraEsperado &&
				respostaSemAcento[..^1] == esperadoSemAcento[..^1])
			{
				// F.1. Confusão S/Z
				if ((ultimaLetraResposta == 's' && ultimaLetraEsperado == 'z') ||
					(ultimaLetraResposta == 'z' && ultimaLetraEsperado == 's'))
				{
					return TipoErro.ConfusaoSZ;
				}

				// F.2. Confusão S/X
				if ((ultimaLetraResposta == 's' && ultimaLetraEsperado == 'x') ||
					(ultimaLetraResposta == 'x' && ultimaLetraEsperado == 's'))
				{
					return TipoErro.ConfusaoSX;
				}

				// F.3. Confusão S/Ç
				if ((ultimaLetraResposta == 's' && ultimaLetraEsperado == 'ç') ||
					(ultimaLetraResposta == 'ç' && ultimaLetraEsperado == 's'))
				{
					return TipoErro.ConfusaoSC;
				}

				return TipoErro.TrocaFim;
			}
		}

		// === ERROS DE PRIMEIRA LETRA (USAR VERSÕES SEM ACENTO) ===

		// 3. Omissão de "H" inicial (ex: homem > omem)
		if (esperadoSemAcento.Length > 0 && esperadoSemAcento[0] == 'h' &&
			respostaSemAcento.Length > 0 && respostaSemAcento[0] != 'h' &&
			esperadoSemAcento.Substring(1) == respostaSemAcento)
		{
			return TipoErro.IrregularidadeHInicio;
		}

		// 4. Acréscimo de "H" inicial (ex: ontem > hontem)
		if (respostaSemAcento.Length > 0 && respostaSemAcento[0] == 'h' &&
			esperadoSemAcento.Length > 0 && esperadoSemAcento[0] != 'h' &&
			respostaSemAcento.Substring(1) == esperadoSemAcento)
		{
			return TipoErro.IrregularidadeHInicio;
		}

		// 5. Troca S/C iniciais antes de E/I
		if (respostaSemAcento.Length > 1 && esperadoSemAcento.Length > 1)
		{
			var primeiraLetraResposta = respostaSemAcento[0];
			var primeiraLetraEsperado = esperadoSemAcento[0];
			var segundaLetraResposta = respostaSemAcento[1];
			var segundaLetraEsperado = esperadoSemAcento[1];

			if ((primeiraLetraResposta == 's' || primeiraLetraResposta == 'c') &&
				(primeiraLetraEsperado == 's' || primeiraLetraEsperado == 'c') &&
				primeiraLetraResposta != primeiraLetraEsperado &&
				(segundaLetraResposta == 'e' || segundaLetraResposta == 'i') &&
				segundaLetraResposta == segundaLetraEsperado &&
				respostaSemAcento.Substring(1) == esperadoSemAcento.Substring(1))
			{
				return TipoErro.ContextualSCInicio;
			}
		}

		// 6. Supressão da primeira letra (ex: teste > este)
		if (respostaSemAcento.Length == esperadoSemAcento.Length - 1 &&
			esperadoSemAcento.Substring(1) == respostaSemAcento)
		{
			return TipoErro.SupressaoInicio;
		}

		// 7. Acréscimo de letra inicial (ex: teste > oteste)
		if (respostaSemAcento.Length == esperadoSemAcento.Length + 1 &&
			respostaSemAcento.Substring(1) == esperadoSemAcento)
		{
			return TipoErro.AcrescimoInicio;
		}

		// 8. Troca da primeira letra (ex: teste > deste)
		if (respostaSemAcento.Length == esperadoSemAcento.Length &&
			respostaSemAcento.Length > 0 &&
			respostaSemAcento[0] != esperadoSemAcento[0] &&
			respostaSemAcento.Substring(1) == esperadoSemAcento.Substring(1))
		{
			return TipoErro.TrocaInicio;
		}

		// === VERIFICAÇÃO DE ACENTUAÇÃO (DEPOIS dos erros específicos!) ===
		// Se sem acentos ficam iguais, o único erro é acentuação
		if (respostaSemAcento == esperadoSemAcento)
			return TipoErro.Acentuacao;

		// === ERROS ORTOGRÁFICOS GENÉRICOS ===

		// 9. Erro ortográfico puro (mesmo comprimento, letras diferentes)
		if (respostaSemAcento.Length == esperadoSemAcento.Length)
		{
			int diferencas = 0;
			for (int i = 0; i < respostaSemAcento.Length; i++)
			{
				if (respostaSemAcento[i] != esperadoSemAcento[i])
					diferencas++;
			}

			if (diferencas > 0)
				return TipoErro.Ortografico;
		}

		// 10. Diferença de comprimento
		var diferencaComprimento = Math.Abs(respostaSemAcento.Length - esperadoSemAcento.Length);

		if (diferencaComprimento <= 2)
		{
			var caracteresComuns = respostaSemAcento.Intersect(esperadoSemAcento).Count();
			var tamanhoMenor = Math.Min(respostaSemAcento.Length, esperadoSemAcento.Length);

			if (tamanhoMenor > 0 && (double)caracteresComuns / tamanhoMenor >= 0.7)
				return TipoErro.Ortografico;
		}

		// 11. Grande diferença = omissão genérica
		return TipoErro.Omissao;
	}

	private string RemoverAcentos(string texto)
	{
		var acentos = "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ";
		var semAcentos = "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC";

		for (int i = 0; i < acentos.Length; i++)
		{
			texto = texto.Replace(acentos[i], semAcentos[i]);
		}

		return texto;
	}
}