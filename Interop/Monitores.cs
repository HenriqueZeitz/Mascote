using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Mascote.Interop;

public static class Monitores
{
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFO mi);

    /// <summary>
    /// Área útil (sem a barra de tarefas) do monitor que contém o ponto x,y — em unidades do WPF, as mesmas de Window.Left/Top.
    /// <paramref name="visual"/>: qualquer janela já aberta (para converter pixels &lt;-&gt; unidades conforme o DPI).
    /// </summary>
    public static Rect AreaUtil(double x, double y, Visual? visual)
    {
        var alvo = visual != null ? PresentationSource.FromVisual(visual)?.CompositionTarget : null;
        var paraPx = alvo?.TransformToDevice ?? Matrix.Identity;
        var dePx = alvo?.TransformFromDevice ?? Matrix.Identity;
        var p = paraPx.Transform(new Point(x, y));
        var tela = MonitorFromPoint(new POINT { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) }, 2);   // o mais próximo, se estiver fora
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(tela, ref mi)) return SystemParameters.WorkArea;
        var w = mi.rcWork;
        return new Rect(dePx.Transform(new Point(w.Left, w.Top)), dePx.Transform(new Point(w.Right, w.Bottom)));
    }
}
