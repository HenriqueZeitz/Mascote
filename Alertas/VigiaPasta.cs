using System.IO;

namespace Mascote.Alertas;

/// <summary>
/// Vigia a pasta de mensagens: o Windows avisa na hora quando aparece um arquivo novo (inclusive em pasta de rede),
/// e isso "acorda" a thread de alertas, que não precisa esperar a próxima verificação.
/// </summary>
public sealed class VigiaPasta
{
    FileSystemWatcher? w;
    public volatile bool Falhou;

    public void Vigiar(string pasta, AutoResetEvent sinal)
    {
        Parar(); Falhou = false;
        try
        {
            w = new FileSystemWatcher(pasta, "*.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                InternalBufferSize = 65536
            };
            w.Created += (_, _) => sinal.Set(); w.Renamed += (_, _) => sinal.Set(); w.Changed += (_, _) => sinal.Set();
            w.Error += (_, _) => { Falhou = true; sinal.Set(); };   // rede caiu etc.: a thread recria
            w.EnableRaisingEvents = true;
        }
        catch { w = null; Falhou = true; }
    }

    public void Parar()
    {
        if (w == null) return;
        try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
        w = null;
    }
}
