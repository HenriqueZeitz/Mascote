using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mascote.Nucleo;

/// <summary>O config.json: personagem e guarda-chuva em uso, cidade do clima e pasta dos alertas.</summary>
public sealed class Configuracao
{
    [JsonPropertyName("personagem")] public string? Personagem { get; set; }
    [JsonPropertyName("guardaChuva")] public string? GuardaChuva { get; set; }
    [JsonPropertyName("climaCidade")] public string? ClimaCidade { get; set; }
    [JsonPropertyName("climaLat")] public double? ClimaLat { get; set; }
    [JsonPropertyName("climaLon")] public double? ClimaLon { get; set; }
    [JsonPropertyName("pastaAlertas")] public string? PastaAlertas { get; set; }
    [JsonPropertyName("nomeAlertas")] public string? NomeAlertas { get; set; }

    // chaves que esta versão não conhece continuam no arquivo
    [JsonExtensionData] public Dictionary<string, JsonElement>? Outros { get; set; }

    public static Configuracao Ler()
    {
        try { return Json.Ler<Configuracao>(Caminhos.ArqConfig) ?? new Configuracao(); }
        catch { return new Configuracao(); }
    }

    /// <summary>Grava só o que for alterado em <paramref name="mudanca"/>, mantendo o resto do config.json.</summary>
    public static void Alterar(Action<Configuracao> mudanca)
    {
        var c = Ler();
        mudanca(c);
        Json.Gravar(Caminhos.ArqConfig, c);
    }
}
