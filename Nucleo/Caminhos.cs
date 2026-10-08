using System.IO;

namespace Mascote.Nucleo;

/// <summary>Pastas e arquivos de dados do mascote (personagens, guarda-chuvas, mochila e config.json).</summary>
public static class Caminhos
{
    public static string PastaDados { get; } = AcharPastaDados();
    public static string PastaPersonagens => Path.Combine(PastaDados, "personagens");
    public static string PastaGuardaChuvas => Path.Combine(PastaDados, "guarda-chuvas");
    public static string PastaMochila => Path.Combine(PastaDados, "mochila");
    public static string ArqConfig => Path.Combine(PastaDados, "config.json");
    public static string ArqErros => Path.Combine(PastaDados, "erros.log");

    /// <summary>Cria a estrutura de pastas padrão, se ainda não existir.</summary>
    public static void CriarPastas()
    {
        Directory.CreateDirectory(PastaPersonagens);
        Directory.CreateDirectory(PastaGuardaChuvas);
        Directory.CreateDirectory(PastaMochila);
    }

    // "Dados" ao lado do executável. Rodando pelo Visual Studio / dotnet run, sobe de bin\... até a raiz do projeto,
    // se já houver uma pasta Dados lá.
    static string AcharPastaDados()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "Dados");
            if (Directory.Exists(Path.Combine(p, "personagens"))) return p;
        }
        var aoLado = Path.Combine(AppContext.BaseDirectory, "Dados");
        if (DaParaGravar(aoLado)) return aoLado;
        // executável numa pasta protegida (ex.: Arquivos de Programas): os dados vão para a pasta do usuário
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mascote", "Dados");
    }

    static bool DaParaGravar(string pasta)
    {
        try
        {
            Directory.CreateDirectory(pasta);
            var teste = Path.Combine(pasta, $"teste_{Environment.ProcessId}.tmp");
            File.WriteAllText(teste, ""); File.Delete(teste);
            return true;
        }
        catch { return false; }
    }
}
