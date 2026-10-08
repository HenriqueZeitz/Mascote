using System.IO;
using System.Reflection;

namespace Mascote.Personagens;

/// <summary>
/// O pato de exemplo, que vai embutido no executável (pasta Recursos\Pato do projeto).
/// É o personagem usado enquanto o usuário não escolhe outro.
/// </summary>
public static class PersonagemPadrao
{
    public const string Nome = "Pato";
    const string Prefixo = "Pato/";   // nome lógico dos recursos embutidos (ver Mascote.csproj)

    /// <summary>Se não houver nenhum personagem, grava o pato em Dados\personagens\Pato.</summary>
    public static void InstalarSeFaltar()
    {
        if (RepositorioPersonagens.Listar().Any()) return;
        var dir = RepositorioPersonagens.Pasta(Nome);
        Directory.CreateDirectory(dir);
        var asm = Assembly.GetExecutingAssembly();
        // rig.json por último: é ele que marca a pasta como um personagem completo
        foreach (var recurso in asm.GetManifestResourceNames().Where(r => r.StartsWith(Prefixo)).OrderBy(r => r.EndsWith("rig.json")))
        {
            using var origem = asm.GetManifestResourceStream(recurso)!;
            using var destino = File.Create(Path.Combine(dir, recurso[Prefixo.Length..]));
            origem.CopyTo(destino);
        }
    }
}
