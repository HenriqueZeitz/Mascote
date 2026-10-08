using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mascote.Clima;

/// <summary>Nuvenzinha de chuva em cima da cabeça do mascote (aparece só quando está chovendo na cidade configurada).</summary>
public partial class NuvemChuva : Canvas
{
    readonly List<(Line Linha, double Fase)> gotas = [];
    readonly double queda;
    int clarao;

    /// <summary>Quanto a nuvem ocupa acima da cabeça.</summary>
    public double AlturaOcupada { get; }

    /// <summary>Monta a nuvem em cima de um personagem mw x mh, com a chuva caindo até o topo da cabeça.</summary>
    public NuvemChuva(double mw, double mh)
    {
        InitializeComponent();
        double u = Math.Min(120, Math.Max(50, mw * 0.9)) / 100;
        double vao = Math.Max(18, mh * 0.3);          // espaço entre a nuvem e a cabeça, por onde a chuva cai
        RenderTransform = new ScaleTransform(u, u);
        SetLeft(this, mw / 2 - 50 * u); SetTop(this, mh * 0.04 - vao - 52 * u);
        Visibility = Visibility.Collapsed;
        var rnd = new Random();
        for (int i = 0; i < 7; i++)
        {
            var g = new Line
            {
                X1 = 28 + i * 7.5 + rnd.Next(-2, 3),
                Stroke = new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6)),
                StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            g.X2 = g.X1 - 1.5;
            Gotas.Children.Add(g);
            gotas.Add((g, rnd.NextDouble()));
        }
        queda = vao / u + 2;
        AlturaOcupada = vao + 52 * u - mh * 0.04 + 6;
    }

    /// <summary>Anima as gotas (e o raio, se for trovoada) — chamar a cada quadro enquanto a nuvem estiver visível.</summary>
    public void Atualizar(double t, bool trovoada, Random rnd)
    {
        foreach (var (linha, fase) in gotas)
        {
            double p = (t * 0.045 + fase) % 1;                 // 0 = saindo da nuvem, 1 = chegando na cabeça
            linha.Y1 = 50 + p * queda; linha.Y2 = linha.Y1 + 7;
            linha.Opacity = Math.Min(1, (1 - p) * 3);
        }
        if (clarao > 0)
        {
            if (--clarao == 0) { Raio.Visibility = Visibility.Collapsed; Corpo.Opacity = 1; }
        }
        else if (trovoada && rnd.Next(150) == 0)
        {
            Raio.Visibility = Visibility.Visible; Corpo.Opacity = 0.75; clarao = 4;
        }
    }
}
