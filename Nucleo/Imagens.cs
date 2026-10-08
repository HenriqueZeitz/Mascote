using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mascote.Nucleo;

public static class Imagens
{
    /// <summary>Carrega um arquivo de imagem sem deixá-lo preso (os editores regravam os mesmos arquivos).</summary>
    public static BitmapImage Carregar(string arquivo)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bi.UriSource = new Uri(arquivo);
        bi.EndInit();
        return bi;
    }

    public static Image NovaImagem(string arquivo, double largura, double altura)
    {
        var im = new Image { Source = Carregar(arquivo), Width = largura, Height = altura, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
        return im;
    }

    public static SolidColorBrush Pincel(string hex, byte alfa = 255)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(Color.FromArgb(alfa, c.R, c.G, c.B));
    }
}
