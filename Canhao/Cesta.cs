using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Mascote.Interop;

namespace Mascote.Canhao;

public partial class CestaFundo : Canvas
{
    public CestaFundo() => InitializeComponent();
}

public partial class CestaFrente : Canvas
{
    public CestaFrente() => InitializeComponent();
}

/// <summary>
/// Cesta de basquete: aparece junto com o canhão, do lado oposto da tela. Conta cesta quando o centro do mascote
/// atravessa o aro de cima para baixo; se ele bater na tabela, rebate.
/// São duas janelas, para o mascote passar POR DENTRO do aro:
///   fundo  = tabela, suporte e a metade de trás do aro   (fica atrás do mascote)
///   frente = metade da frente do aro e a rede             (fica na frente do mascote)
/// </summary>
public sealed class Cesta
{
    readonly Window fundo, frente;
    readonly IntPtr hwndFundo, hwndFrente;
    readonly ScaleTransform rede;

    /// <summary>Tabela à direita do aro (canhão na esquerda da tela).</summary>
    public bool Direita { get; }
    public double AroL { get; }
    public double AroR { get; }
    public double AroY { get; }
    public double TabL { get; }
    public double TabR { get; }
    public double TabTopo { get; }
    public double TabBase { get; }
    public bool Acertou;
    /// <summary>Quadros de balanço da rede que ainda faltam.</summary>
    public int Balanco;
    /// <summary>Contagem para sumir (-1 = ainda em jogo).</summary>
    public int Fim = -1;

    /// <param name="xCanhao">X (tela) do canhão</param>
    /// <param name="larguraAro">largura do aro na tela (proporcional ao personagem)</param>
    public Cesta(double xCanhao, double larguraAro, Rect? area = null)
    {
        var wa = area ?? SystemParameters.WorkArea;
        double k = larguraAro / 78;
        var rnd = new Random();
        Direita = (xCanhao - wa.Left) < (wa.Width / 2);        // canhão na esquerda -> cesta na direita
        double alvoX = wa.Left + wa.Width * (Direita ? 0.55 + rnd.NextDouble() * 0.33 : 0.12 + rnd.NextDouble() * 0.33);
        double alvoY = wa.Top + wa.Height * (0.3 + rnd.NextDouble() * 0.3);
        double aroL = Direita ? 40 : 22, aroR = Direita ? 118 : 100, tabL = Direita ? 122 : 5;
        double left = alvoX - (aroL + aroR) / 2 * k, top = alvoY - 71 * k;

        var desenhoFrente = new CestaFrente();
        rede = desenhoFrente.Rede;
        fundo = NovaJanela(new CestaFundo(), k, !Direita);
        frente = NovaJanela(desenhoFrente, k, !Direita);
        foreach (var j in new[] { fundo, frente }) { j.Left = left; j.Top = top; j.Show(); }
        hwndFundo = new WindowInteropHelper(fundo).Handle;
        hwndFrente = new WindowInteropHelper(frente).Handle;

        AroL = left + aroL * k; AroR = left + aroR * k; AroY = top + 71 * k;
        TabL = left + tabL * k; TabR = left + (tabL + 13) * k; TabTopo = top + 8 * k; TabBase = top + 102 * k;
    }

    static Window NovaJanela(Canvas desenho, double k, bool espelhar)
    {
        var g = new TransformGroup();
        if (espelhar) g.Children.Add(new ScaleTransform(-1, 1, 70, 0));   // tabela à esquerda
        g.Children.Add(new ScaleTransform(k, k));
        desenho.RenderTransform = g;
        var raiz = new Canvas();
        raiz.Children.Add(desenho);
        return new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true,
            ShowInTaskbar = false, ShowActivated = false, ResizeMode = ResizeMode.NoResize, IsHitTestVisible = false,
            Content = raiz, Width = 140 * k, Height = 150 * k
        };
    }

    /// <summary>Empilha as janelas: fundo da cesta &lt; mascote &lt; frente da cesta (cada chamada põe a janela no topo).</summary>
    public void Ordenar(IntPtr hwndMascote)
    {
        JanelaNativa.KeepOnTop(hwndFundo);
        if (hwndMascote != IntPtr.Zero) JanelaNativa.KeepOnTop(hwndMascote);
        JanelaNativa.KeepOnTop(hwndFrente);
    }

    /// <summary>
    /// Chamar a cada quadro: balança a rede depois da cesta e fecha as janelas quando a contagem acabar.
    /// Devolve false quando a cesta foi fechada.
    /// </summary>
    public bool Atualizar()
    {
        if (Balanco > 0)
        {
            Balanco--;
            double f = Math.Sin(Balanco * 0.5) * Balanco / 30;
            rede.ScaleY = 1 + 0.3 * f; rede.ScaleX = 1 - 0.1 * f;
        }
        if (Fim > 0)
        {
            Fim--;
            if (Fim < 20) { fundo.Opacity = Fim / 20.0; frente.Opacity = Fim / 20.0; }   // some devagar
            if (Fim == 0) { Fechar(); return false; }
        }
        return true;
    }

    public void Fechar()
    {
        foreach (var j in new[] { fundo, frente }) { try { j.Close(); } catch { } }
    }
}
