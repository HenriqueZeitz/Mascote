using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mascote.Roleta;

/// <summary>
/// Roleta russa (de brincadeira): o revólver de desenho animado que aparece ao lado do mascote, com 1 bala em 6.
/// As partes animadas (Tambor, Bandeira, EscalaBandeira) vêm do XAML.
/// </summary>
public partial class Arma : Canvas
{
    readonly Canvas palco;

    /// <summary>Escala de 0 a 1 com que o revólver aparece e some.</summary>
    public ScaleTransform Aparece { get; } = new(0, 0, 42, 26);
    /// <summary>Tremor do suspense.</summary>
    public TranslateTransform Treme { get; } = new();

    /// <summary>Monta o revólver ao lado do mascote (no palco da janela), mirando a cabeça dele.</summary>
    public Arma(Canvas palco, FrameworkElement mascot)
    {
        InitializeComponent();
        this.palco = palco;
        double ml = GetLeft(mascot), mt = GetTop(mascot);
        double x = ml + mascot.Width * 0.92;
        double u = Math.Min(1, Math.Max(0.4, (palco.Width - x - 3) / 84));   // cabe no que sobra à direita do mascote
        x = Math.Min(x, palco.Width - 84 * u - 3);                           // se ainda não couber, encosta mais no mascote
        var grupo = new TransformGroup();
        grupo.Children.Add(Aparece); grupo.Children.Add(new ScaleTransform(u, u)); grupo.Children.Add(Treme);
        RenderTransform = grupo;
        SetLeft(this, x); SetTop(this, mt + mascot.Height * 0.12);
        SetZIndex(this, 8);
        palco.Children.Add(this);
    }

    public void Remover() => palco.Children.Remove(this);

    public void MostrarBandeira() => Bandeira.Visibility = Visibility.Visible;
}
