using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mascote.Nucleo;

/// <summary>Leitura e gravação dos arquivos .json.</summary>
public static class Json
{
    public static readonly UTF8Encoding Utf8 = new(false);

    public static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // acentos legíveis no arquivo
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static readonly JsonSerializerOptions Compacto = new(Opcoes) { WriteIndented = false };

    public static T? Ler<T>(string arquivo) => JsonSerializer.Deserialize<T>(File.ReadAllText(arquivo, Encoding.UTF8), Opcoes);

    public static void Gravar<T>(string arquivo, T valor, bool compacto = false) =>
        File.WriteAllText(arquivo, JsonSerializer.Serialize(valor, compacto ? Compacto : Opcoes), Utf8);
}
