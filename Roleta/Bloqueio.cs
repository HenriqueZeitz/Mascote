using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mascote.Roleta;

/// <summary>Bloqueio da tela do Windows (igual Win+L — nada é fechado nem perdido), usado quando a roleta cai na bala.</summary>
public static class Bloqueio
{
    [DllImport("user32.dll")] static extern bool LockWorkStation();

    /// <summary>true = não bloqueia de verdade (testes: --roleta-simular).</summary>
    public static bool Simular;
    /// <summary>"bala" | "vazio" para forçar o resultado (testes: --roleta-forcar=bala).</summary>
    public static string? Forcar;

    /// <summary>Tela bloqueada? (enquanto a tela de bloqueio está aberta, o processo LogonUI fica rodando)</summary>
    public static bool TelaBloqueada()
    {
        var ps = Process.GetProcessesByName("LogonUI");
        foreach (var p in ps) p.Dispose();
        return ps.Length > 0;
    }

    public static void Bloquear()
    {
        if (Simular) return;
        LockWorkStation();
    }
}
