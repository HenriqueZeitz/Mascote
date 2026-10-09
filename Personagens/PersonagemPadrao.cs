using System.IO;
using System.Reflection;

namespace Mascote.Personagens;

/// <summary>
/// Os personagens que vão embutidos no executável (pastas Recursos\Pato e Recursos\Lontra do projeto).
/// O pato é o personagem usado enquanto o usuário não escolhe outro.
/// </summary>
public static class PersonagemPadrao
{
    public const string Nome = "Pato";
    const string Lontra = "Lontra";

    /// <summary>
    /// Grava os personagens embutidos em Dados\personagens. O pato só é gravado quando não há nenhum personagem
    /// (primeira execução); a lontra é gravada sempre que a pasta dela não existe, para quem já usa o mascote
    /// também ganhar o personagem novo.
    /// </summary>
    public static void InstalarSeFaltar()
    {
        bool semNenhum = !RepositorioPersonagens.Listar().Any();
        if (semNenhum) Instalar(Nome);
        if (!File.Exists(Path.Combine(RepositorioPersonagens.Pasta(Lontra), "rig.json"))) Instalar(Lontra);
    }

    // Copia os recursos embutidos "<nome>/..." (nome lógico definido em Mascote.csproj) para Dados\personagens\<nome>
    static void Instalar(string nome)
    {
        var prefixo = nome + "/";
        var dir = RepositorioPersonagens.Pasta(nome);
        Directory.CreateDirectory(dir);
        var asm = Assembly.GetExecutingAssembly();
        // rig.json por último: é ele que marca a pasta como um personagem completo
        foreach (var recurso in asm.GetManifestResourceNames().Where(r => r.StartsWith(prefixo)).OrderBy(r => r.EndsWith("rig.json")))
        {
            using var origem = asm.GetManifestResourceStream(recurso)!;
            using var destino = File.Create(Path.Combine(dir, recurso[prefixo.Length..]));
            origem.CopyTo(destino);
        }
    }
}
