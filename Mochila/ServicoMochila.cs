using System.IO;
using Mascote.Nucleo;
using Microsoft.VisualBasic.FileIO;

namespace Mascote.Mochila;

/// <summary>
/// Mochila: arraste arquivos/pastas até o mascote e ele guarda uma CÓPIA em Dados\mochila (o original fica onde estava).
/// </summary>
public static class ServicoMochila
{
    public static FileSystemInfo[] Itens()
    {
        if (!Directory.Exists(Caminhos.PastaMochila)) return [];
        return new DirectoryInfo(Caminhos.PastaMochila).GetFileSystemInfos().OrderByDescending(i => i.LastWriteTime).ToArray();
    }

    /// <summary>O que foi arrastado, sem o que já está dentro da mochila.</summary>
    public static string[] DeFora(IEnumerable<string> caminhos) =>
        caminhos.Where(c => !c.StartsWith(Caminhos.PastaMochila, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>"relatorio.pdf" -> "relatorio (2).pdf" se já existir.</summary>
    public static string NomeLivre(string pasta, string nome)
    {
        var alvo = Path.Combine(pasta, nome);
        if (!Path.Exists(alvo)) return alvo;
        string b = Path.GetFileNameWithoutExtension(nome), ext = Path.GetExtension(nome);
        for (int i = 2; ; i++)
        {
            alvo = Path.Combine(pasta, $"{b} ({i}){ext}");
            if (!Path.Exists(alvo)) return alvo;
        }
    }

    /// <summary>Copia os itens para a mochila (o Windows mostra a barra de progresso se demorar). Devolve os nomes guardados.</summary>
    public static List<string> Adicionar(IEnumerable<string> caminhos)
    {
        Directory.CreateDirectory(Caminhos.PastaMochila);
        var ok = new List<string>();
        foreach (var c in caminhos)
        {
            try
            {
                var alvo = NomeLivre(Caminhos.PastaMochila, Path.GetFileName(c.TrimEnd('\\')));
                if (Directory.Exists(c)) FileSystem.CopyDirectory(c, alvo, UIOption.AllDialogs, UICancelOption.DoNothing);
                else FileSystem.CopyFile(c, alvo, UIOption.AllDialogs, UICancelOption.DoNothing);
                if (Path.Exists(alvo)) ok.Add(Path.GetFileName(alvo));
            }
            catch { }
        }
        return ok;
    }

    /// <summary>Tira um item da mochila para outra pasta.</summary>
    public static void Mover(string item, string pastaDestino)
    {
        var alvo = NomeLivre(pastaDestino, Path.GetFileName(item));
        if (Directory.Exists(item)) FileSystem.MoveDirectory(item, alvo, UIOption.AllDialogs, UICancelOption.DoNothing);
        else FileSystem.MoveFile(item, alvo, UIOption.AllDialogs, UICancelOption.DoNothing);
    }

    public static void MandarParaLixeira(string item)
    {
        if (Directory.Exists(item)) FileSystem.DeleteDirectory(item, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        else FileSystem.DeleteFile(item, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    public static double Tamanho(FileSystemInfo item)
    {
        if (item is FileInfo f) return f.Length;
        try
        {
            var opcoes = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
            return ((DirectoryInfo)item).EnumerateFiles("*", opcoes).Sum(a => (double)a.Length);
        }
        catch { return 0; }
    }

    public static string FormatarTamanho(double b) =>
        b >= 1L << 30 ? $"{b / (1L << 30):N1} GB" : b >= 1 << 20 ? $"{b / (1 << 20):N1} MB" : b >= 1 << 10 ? $"{b / (1 << 10):N0} KB" : $"{b} bytes";
}
