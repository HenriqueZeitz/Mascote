using System.Text.Json.Serialization;

namespace Mascote.Personagens;

/// <summary>O rig.json de um personagem.</summary>
public sealed class RigJson
{
    [JsonPropertyName("nome")] public string Nome { get; set; } = "";
    [JsonPropertyName("largura")] public double Largura { get; set; }
    [JsonPropertyName("altura")] public double Altura { get; set; }
    [JsonPropertyName("alturaTela")] public double AlturaTela { get; set; }
    [JsonPropertyName("olhando")] public string Olhando { get; set; } = "direita";
    [JsonPropertyName("base")] public string Base { get; set; } = "base.png";
    [JsonPropertyName("original")] public string Original { get; set; } = "original.png";
    [JsonPropertyName("partes")] public List<ParteJson> Partes { get; set; } = [];
    /// <summary>Frases que este personagem fala (null/vazio = usa as frases padrão).</summary>
    [JsonPropertyName("frases")] public List<string>? Frases { get; set; }
}

public sealed class ParteJson
{
    [JsonPropertyName("papel")] public string Papel { get; set; } = "braco";
    [JsonPropertyName("forma")] public string Forma { get; set; } = "ret";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("w")] public double W { get; set; }
    [JsonPropertyName("h")] public double H { get; set; }
    /// <summary>Forma "livre": contorno x0,y0,x1,y1... (x,y,w,h é a caixa em volta dele).</summary>
    [JsonPropertyName("pontos")] public double[]? Pontos { get; set; }
    [JsonPropertyName("pivoX")] public double PivoX { get; set; }
    [JsonPropertyName("pivoY")] public double PivoY { get; set; }
    [JsonPropertyName("preencher")] public bool Preencher { get; set; }
    [JsonPropertyName("pai")] public int Pai { get; set; } = -1;
    [JsonPropertyName("arquivo")] public string Arquivo { get; set; } = "";
}
