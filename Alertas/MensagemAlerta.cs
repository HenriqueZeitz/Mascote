using System.Text.Json.Serialization;

namespace Mascote.Alertas;

/// <summary>Um alerta gravado em &lt;pasta&gt;\mensagens.</summary>
public sealed class MensagemAlerta
{
    [JsonPropertyName("de")] public string De { get; set; } = "";
    [JsonPropertyName("nome")] public string Nome { get; set; } = "";
    [JsonPropertyName("texto")] public string Texto { get; set; } = "";
    /// <summary>Ids de quem deve receber; lista vazia = todos.</summary>
    [JsonPropertyName("para")] public List<string>? Para { get; set; }
    [JsonPropertyName("quando")] public string? Quando { get; set; }
}

/// <summary>Quem está com o mascote aberto (um arquivo por pessoa em &lt;pasta&gt;\online).</summary>
public sealed record Presenca([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("nome")] string Nome);
