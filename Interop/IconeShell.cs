using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mascote.Interop;

public static class IconeShell
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string caminho, uint atributos, ref SHFILEINFO info, uint tamanho, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    /// <summary>Ícone que o Windows mostra para o arquivo ou pasta (null se não conseguir).</summary>
    public static ImageSource? Obter(string caminho)
    {
        try
        {
            var info = new SHFILEINFO();
            if (SHGetFileInfo(caminho, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100) == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DestroyIcon(info.hIcon); }
        }
        catch { return null; }
    }
}
