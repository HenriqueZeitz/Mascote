using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Mascote.Nucleo;

namespace Mascote.Clima;

/// <summary>
/// Consulta o tempo atual na cidade configurada (Open-Meteo, gratuito e sem cadastro).
/// A consulta roda em segundo plano (a cada 15 min) para o mascote não travar.
/// </summary>
public sealed class ServicoClima
{
    static readonly HttpClient http = new(new HttpClientHandler { DefaultProxyCredentials = CredentialCache.DefaultNetworkCredentials })   // proxy da empresa
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    Task<string>? tarefa;
    DateTime proxima = DateTime.MinValue;

    public string Cidade { get; private set; } = "";
    public double? Lat { get; private set; }
    public double? Lon { get; private set; }
    public bool Ok { get; private set; }
    public int Codigo { get; private set; } = -1;
    public double Temp { get; private set; }
    public bool Chovendo { get; private set; }
    public bool Trovoada { get; private set; }
    public string Erro { get; private set; } = "";

    public bool Configurado => Lat != null;

    public static Task<string> Baixar(string url) => http.GetStringAsync(url);

    public void Iniciar()
    {
        var cfg = Configuracao.Ler();
        if (cfg.ClimaLat != null && cfg.ClimaLon != null)
        {
            Cidade = cfg.ClimaCidade ?? ""; Lat = cfg.ClimaLat; Lon = cfg.ClimaLon;
            proxima = DateTime.MinValue;   // consulta já
        }
    }

    /// <summary>Grava a cidade no config.json e consulta de novo.</summary>
    public void Configurar(string cidade, double lat, double lon)
    {
        Configuracao.Alterar(c => { c.ClimaCidade = cidade; c.ClimaLat = lat; c.ClimaLon = lon; });
        Cidade = cidade; Lat = lat; Lon = lon;
        Ok = false; tarefa = null; proxima = DateTime.MinValue;
    }

    /// <summary>Chame periodicamente (no timer). Dispara/recolhe a consulta; devolve true quando chegou um resultado novo.</summary>
    public bool Atualizar()
    {
        if (Lat == null || Lon == null) return false;
        if (tarefa != null)
        {
            if (!tarefa.IsCompleted) return false;
            var t = tarefa; tarefa = null;
            if (!t.IsCompletedSuccessfully)
            {
                Erro = t.Exception?.GetBaseException().Message ?? "A consulta demorou demais.";
                proxima = DateTime.Now.AddMinutes(5);   // tenta de novo daqui a pouco
                return false;
            }
            try
            {
                using var doc = JsonDocument.Parse(t.Result);
                var r = doc.RootElement.GetProperty("current");
                Codigo = r.GetProperty("weather_code").GetInt32(); Temp = r.GetProperty("temperature_2m").GetDouble();
                int c = Codigo;
                Chovendo = (c >= 51 && c <= 67) || (c >= 80 && c <= 82) || c >= 95 || r.GetProperty("precipitation").GetDouble() >= 0.1;
                Trovoada = c >= 95;
                Ok = true; Erro = "";
            }
            catch (Exception ex) { Erro = ex.Message; }
            proxima = DateTime.Now.AddMinutes(15);
            return true;
        }
        if (DateTime.Now >= proxima)
        {
            var inv = CultureInfo.InvariantCulture;
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={Lat.Value.ToString(inv)}&longitude={Lon.Value.ToString(inv)}" +
                      "&current=temperature_2m,weather_code,precipitation&timezone=auto";
            try { tarefa = Baixar(url); }
            catch (Exception ex) { Erro = ex.Message; proxima = DateTime.Now.AddMinutes(5); }
        }
        return false;
    }

    public string Fala()
    {
        if (!Ok) return Erro != "" ? $"Não consegui ver o tempo: {Erro}" : "Ainda estou olhando o tempo...";
        var txt = $"Agora{(Cidade != "" ? $" em {Cidade}" : "")}: {Math.Round(Temp)}°C, {Descricao(Codigo)}.";
        if (Trovoada) txt += " Fica longe da janela!";
        else if (Chovendo) txt += " Leva o guarda-chuva!";
        else if (Temp >= 30) txt += " Que calor!";
        else if (Temp <= 15) txt += " Tá frio, hein!";
        return txt;
    }

    /// <summary>Códigos de tempo da OMM (usados pelo Open-Meteo).</summary>
    public static string Descricao(int c) => c switch
    {
        0 => "céu limpo", 1 => "quase sem nuvens", 2 => "parcialmente nublado", 3 => "nublado",
        45 or 48 => "neblina",
        51 or 53 or 55 => "garoa", 56 or 57 => "garoa congelante",
        61 => "chuva fraca", 63 => "chuva", 65 => "chuva forte", 66 or 67 => "chuva congelante",
        71 or 73 or 75 or 77 => "neve",
        80 => "pancadas de chuva fracas", 81 => "pancadas de chuva", 82 => "pancadas de chuva fortes",
        85 or 86 => "pancadas de neve",
        95 => "trovoada", 96 or 99 => "trovoada com granizo",
        _ => "tempo desconhecido"
    };
}
