using System.Runtime.InteropServices;

namespace Mascote.Interop;

public static class JanelaNativa
{
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

    /// <summary>Põe a janela acima de tudo (inclusive da barra de tarefas), sem mover nem ativar.</summary>
    public static void KeepOnTop(IntPtr h) => SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);

    /// <summary>Some do Alt+Tab.</summary>
    public static void ToolWindow(IntPtr h) => SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x80);
}
