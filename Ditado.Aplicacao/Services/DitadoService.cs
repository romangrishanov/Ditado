using System.Text.RegularExpressions;
using Ditado.Aplicacao.DTOs;
using Ditado.Dominio.Entidades;
using Ditado.Dominio.Enums;
using Ditado.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Ditado.Aplicacao.Services;

public class DitadoService
{
	private readonly DitadoDbContext _context;

	public DitadoService(DitadoDbContext context)
	{
		_context = context;
	}

	public async Task<DitadoResponse> CriarDitadoAsync(CriarDitadoRequest request)
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
			Segmentos = segmentos
		};

		_context.Ditados.Add(ditado);
		await _context.SaveChangesAsync();

		return new DitadoResponse
		{
			Id = ditado.Id,
			Titulo = ditado.Titulo,
			Descricao = ditado.Descricao,
			DataCriacao = ditado.DataCriacao
		};
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

	public async Task<ResultadoDitadoResponse?> SubmeterRespostaAsync(int ditadoId, SubmeterRespostaRequest request)
	{
		var ditado = await _context.Ditados
			.Include(d => d.Segmentos)
			.FirstOrDefaultAsync(d => d.Id == ditadoId && d.Ativo);

		if (ditado == null)
			return null;

		var respostaDitado = new RespostaDitado
		{
			DitadoId = ditadoId,
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
				TipoErro = correto ? null : tipoErro.ToString()
			});
		}

		respostaDitado.Pontuacao = totalLacunas > 0 ? (decimal)acertos / totalLacunas * 100 : 0;

		_context.RespostaDitados.Add(respostaDitado);
		await _context.SaveChangesAsync();

		return new ResultadoDitadoResponse
		{
			RespostaDitadoId = respostaDitado.Id,
			Pontuacao = respostaDitado.Pontuacao,
			TotalLacunas = totalLacunas,
			Acertos = acertos,
			Erros = totalLacunas - acertos,
			Detalhes = detalhes
		};
	}

	public async Task<List<DitadoResponse>> ListarDitadosAsync()
	{
		return await _context.Ditados
			.Where(d => d.Ativo)
			.OrderByDescending(d => d.DataCriacao)
			.Select(d => new DitadoResponse
			{
				Id = d.Id,
				Titulo = d.Titulo,
				Descricao = d.Descricao,
				DataCriacao = d.DataCriacao
			})
			.ToListAsync();
	}

	private List<DitadoSegmento> ParsearTextoComLacunas(string texto)
	{
		var segmentos = new List<DitadoSegmento>();
		var regex = new Regex(@"\[([^\]]+)\]");
		int ordem = 1;
		int ultimoIndice = 0;

		foreach (Match match in regex.Matches(texto))
		{
			if (match.Index > ultimoIndice)
			{
				segmentos.Add(new DitadoSegmento
				{
					Ordem = ordem++,
					Tipo = TipoSegmento.Texto,
					Conteudo = texto.Substring(ultimoIndice, match.Index - ultimoIndice)
				});
			}

			segmentos.Add(new DitadoSegmento
			{
				Ordem = ordem++,
				Tipo = TipoSegmento.Lacuna,
				Conteudo = match.Groups[1].Value
			});

			ultimoIndice = match.Index + match.Length;
		}

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

	private byte[] ExtrairAudioDeBase64(string audioBase64)
	{
		var base64Data = audioBase64.Contains(",") ? audioBase64.Split(',')[1] : audioBase64;
		return Convert.FromBase64String(base64Data);
	}

	private string NormalizarTexto(string texto)
	{
		return texto.Trim().ToLowerInvariant();
	}

	private TipoErro IdentificarTipoErro(string resposta, string esperado)
	{
		var respostaNorm = NormalizarTexto(resposta);
		var esperadoNorm = NormalizarTexto(esperado);

		// 1. Verifica omiss„o (resposta vazia ou muito curta)
		if (string.IsNullOrWhiteSpace(respostaNorm))
			return TipoErro.Omissao;

		// 2. Verifica erro de acentuaÁ„o (se remover acentos ficam iguais)
		if (RemoverAcentos(respostaNorm) == RemoverAcentos(esperadoNorm))
			return TipoErro.Acentuacao;

		// 3. Verifica se È erro ortogr·fico puro (mesmo comprimento, letras diferentes)
		if (respostaNorm.Length == esperadoNorm.Length)
		{
			// Conta quantos caracteres s„o diferentes
			int diferencas = 0;
			for (int i = 0; i < respostaNorm.Length; i++)
			{
				if (respostaNorm[i] != esperadoNorm[i])
					diferencas++;
			}

			// Se tem diferenÁas mas mesmo tamanho, È ortogr·fico (troca de letras)
			if (diferencas > 0)
				return TipoErro.Ortografico;
		}

		// 4. DiferenÁa de comprimento pode ser omiss„o OU ortogr·fico
		// Vamos usar dist‚ncia de Levenshtein simplificada:
		// Se a diferenÁa È pequena (1-2 chars) e as palavras s„o similares, È ortogr·fico
		var diferencaComprimento = Math.Abs(respostaNorm.Length - esperadoNorm.Length);
		
		if (diferencaComprimento <= 2)
		{
			// Verifica similaridade: quantos caracteres em comum?
			var caracteresComuns = respostaNorm.Intersect(esperadoNorm).Count();
			var tamanhoMenor = Math.Min(respostaNorm.Length, esperadoNorm.Length);
			
			// Se mais de 70% dos caracteres s„o comuns, È ortogr·fico
			if (tamanhoMenor > 0 && (double)caracteresComuns / tamanhoMenor >= 0.7)
				return TipoErro.Ortografico;
		}

		// 5. Grande diferenÁa de comprimento = omiss„o
		return TipoErro.Omissao;
	}

	private string RemoverAcentos(string texto)
	{
		var acentos = "·‡‚„‰ÈËÍÎÌÏÓÔÛÚÙıˆ˙˘˚¸Á¡¿¬√ƒ…» ÀÕÃŒœ”“‘’÷⁄Ÿ€‹«";
		var semAcentos = "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC";

		for (int i = 0; i < acentos.Length; i++)
		{
			texto = texto.Replace(acentos[i], semAcentos[i]);
		}

		return texto;
	}
}