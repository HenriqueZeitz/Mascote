using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Mascote.Canhao;

/// <summary>
/// Canhão: aparece no chão, mira no mouse, segura o botão para carregar a força e solta para atirar o mascote.
/// Esc ou botão direito cancelam.
/// </summary>
public partial class JanelaCanhao : Window
{
    /// <summary>A mesma da queda do mascote (por quadro de 33 ms).</summary>
    public const double Gravidade = 0.9;

    readonly Rect wa;
    readonly double px, py;   // eixo do cano (coordenadas desta janela)
    readonly Action<double, double, double, double> aoAtirar;
    readonly Action aoCancelar;
    readonly DispatcherTimer timer;
    double ang = -45, forca = 0.5;
    bool carregando, atirou;
    int tc, fim;

    /// <param name="xBase">posição X (tela) do canhão</param>
    /// <param name="aoAtirar">(x, y, vx, vy): a boca do cano, na tela, e a velocidade</param>
    /// <param name="area">monitor onde o mascote está</param>
    public JanelaCanhao(double xBase, Action<double, double, double, double> aoAtirar, Action aoCancelar, Rect? area = null)
    {
        InitializeComponent();
        this.aoAtirar = aoAtirar; this.aoCancelar = aoCancelar;
        wa = area ?? SystemParameters.WorkArea;
        Left = wa.Left; Top = wa.Top; Width = wa.Width; Height = wa.Height;

        // a roda encosta no chão
        px = Math.Max(40, Math.Min(wa.Width - 40, xBase - wa.Left)); py = wa.Height - 36;
        Canvas.SetLeft(Carreta, px); Canvas.SetTop(Carreta, py);
        Canvas.SetLeft(Painel, (wa.Width - 300) / 2); Canvas.SetTop(Painel, 20);

        MouseMove += AoMoverMouse;
        MouseLeftButtonDown += (_, _) => { if (atirou) return; carregando = true; tc = 0; CaptureMouse(); };
        MouseLeftButtonUp += (_, _) => Atirar();
        MouseRightButtonUp += (_, _) => Cancelar();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Cancelar(); };

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += Tick;
    }

    public void Mostrar()
    {
        Desenhar();
        Show(); Activate();
        timer.Start();
    }

    double Velocidade => 8 + forca * 30;

    // boca do cano, nas coordenadas desta janela
    Point Boca()
    {
        double a = ang * Math.PI / 180;
        return new Point(px + Math.Cos(a) * 72, py + Math.Sin(a) * 72);
    }

    // linha pontilhada com a trajetória prevista (mesma física do voo)
    void Desenhar()
    {
        Giro.Angle = ang;
        Barra.Width = 274 * forca;
        TxtForca.Text = $"Força: {Math.Round(forca * 100)}%";
        Pontos.Children.Clear();
        var p = Boca(); double a = ang * Math.PI / 180, v = Velocidade;
        double x = p.X, y = p.Y, vx = Math.Cos(a) * v, vy = Math.Sin(a) * v;
        for (int k = 1; k <= 90; k++)
        {
            vy += Gravidade; x += vx; y += vy;
            if (y > wa.Height || x < 0 || x > wa.Width) break;
            if (k % 3 != 0) continue;
            var d = new Ellipse
            {
                Width = 8, Height = 8, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1.5,
                Opacity = Math.Max(0.25, 1 - k / 90.0)
            };
            Canvas.SetLeft(d, x - 4); Canvas.SetTop(d, y - 4);
            Pontos.Children.Add(d);
        }
    }

    void Fechar() { timer.Stop(); Close(); }

    void AoMoverMouse(object sender, MouseEventArgs e)
    {
        if (atirou) return;
        var m = e.GetPosition(Palco);
        double a = Math.Atan2(m.Y - py, m.X - px) * 180 / Math.PI;
        if (a > 0) a = a < 90 ? -5 : -175;   // só aponta para cima
        ang = Math.Max(-175, Math.Min(-5, a));
        Desenhar();
    }

    void Atirar()
    {
        if (!carregando || atirou) return;
        ReleaseMouseCapture(); carregando = false; atirou = true;
        var p = Boca(); double a = ang * Math.PI / 180, v = Velocidade;
        Pontos.Children.Clear(); Painel.Visibility = Visibility.Collapsed; Fumaca.Visibility = Visibility.Visible;
        Background = Brushes.Transparent;   // libera o mouse para o resto da tela
        aoAtirar(p.X + wa.Left, p.Y + wa.Top, Math.Cos(a) * v, Math.Sin(a) * v);
        fim = 25;   // deixa o canhão e a fumaça na tela por ~0,8 s
    }

    void Cancelar()
    {
        if (atirou) return;
        atirou = true; Fechar(); aoCancelar();
    }

    void Tick(object? sender, EventArgs e)
    {
        if (fim > 0)
        {
            // a fumaça se espalhando
            fim--;
            double s = 1 + (25 - fim) * 0.06;
            Fumaca.RenderTransform = new ScaleTransform(s, s, 72, 0);
            Fumaca.Opacity = fim / 25.0;
            if (fim == 0) Fechar();
            return;
        }
        if (carregando)
        {
            tc++;
            forca = (1 - Math.Cos(tc * 0.06)) / 2;   // sobe e desce: solte na hora certa
            Desenhar();
        }
    }
}
