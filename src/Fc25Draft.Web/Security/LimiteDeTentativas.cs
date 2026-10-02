using System.Collections.Concurrent;

namespace Fc25Draft.Web.Security;

/// <summary>
/// Trava quem tenta adivinhar token: 3 tokens errados em 15 minutos bloqueiam aquele IP por 15 minutos.
/// Acertar não zera a conta (senão bastaria intercalar o próprio token com os chutes).
/// </summary>
public sealed class LimiteDeTentativas
{
    public const int Maximo = 3;
    public static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Registro> _porOrigem = new();
    private readonly ILogger<LimiteDeTentativas> _logger;
    private readonly TimeProvider _relogio;

    public LimiteDeTentativas(ILogger<LimiteDeTentativas> logger, TimeProvider? relogio = null)
    {
        _logger = logger;
        _relogio = relogio ?? TimeProvider.System;
    }

    /// <summary>Quanto falta para liberar, ou nulo se a origem não está bloqueada.</summary>
    public TimeSpan? BloqueadoPor(string origem)
    {
        if (!_porOrigem.TryGetValue(origem, out var registro)) return null;
        var agora = _relogio.GetUtcNow();
        lock (registro)
            return registro.Ate is DateTimeOffset ate && ate > agora ? ate - agora : null;
    }

    /// <summary>Conta um token errado. Retorna quantas tentativas ainda restam (0 = acabou de bloquear).</summary>
    public int RegistrarErro(string origem)
    {
        var agora = _relogio.GetUtcNow();
        LimparAntigos(agora);

        var registro = _porOrigem.GetOrAdd(origem, _ => new Registro());
        lock (registro)
        {
            if (registro.Ate is DateTimeOffset ate && ate > agora) return 0;

            // Bloqueio vencido ou erros antigos: começa a contar de novo.
            if (registro.Ate is not null || agora - registro.Inicio > Bloqueio)
            {
                registro.Erros = 0;
                registro.Ate = null;
            }
            if (registro.Erros == 0) registro.Inicio = agora;

            registro.Erros++;
            if (registro.Erros < Maximo) return Maximo - registro.Erros;

            registro.Ate = agora + Bloqueio;
            _logger.LogWarning("Login bloqueado por {Minutos} min para {Origem}: {Erros} tokens errados",
                Bloqueio.TotalMinutes, origem, registro.Erros);
            return 0;
        }
    }

    public static string MensagemDeErro(int restam) => restam > 0
        ? $"Token inválido. Você ainda tem {restam} tentativa{(restam == 1 ? "" : "s")}."
        : $"Token inválido. Foram {Maximo} tentativas erradas: o acesso ficou bloqueado por {Bloqueio.TotalMinutes:0} minutos.";

    public static string MensagemDeBloqueio(TimeSpan falta)
    {
        var minutos = Math.Max(1, (int)Math.Ceiling(falta.TotalMinutes));
        return $"Muitas tentativas com token errado. Tente de novo em {minutos} minuto{(minutos == 1 ? "" : "s")}.";
    }

    private void LimparAntigos(DateTimeOffset agora)
    {
        if (_porOrigem.Count < 500) return;
        foreach (var (origem, registro) in _porOrigem)
        {
            bool vencido;
            lock (registro) vencido = (registro.Ate ?? registro.Inicio + Bloqueio) < agora;
            if (vencido) _porOrigem.TryRemove(origem, out _);
        }
    }

    private sealed class Registro
    {
        public int Erros;
        public DateTimeOffset Inicio;
        public DateTimeOffset? Ate;
    }
}
