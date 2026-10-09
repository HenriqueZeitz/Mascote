using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mascote.Alertas;
using Mascote.Canhao;
using Mascote.Clima;
using Mascote.GuardaChuvas;
using Mascote.Interop;
using Mascote.Mochila;
using Mascote.Nucleo;
using Mascote.Personagens;
using Mascote.Roleta;

namespace Mascote.Principal;

/// <summary>O que o mascote está fazendo (um estado por vez; ver JanelaMascote.Estados.cs).</summary>
public enum Estado
{
    Parado, Andando, Pulando, Caindo,
    GuardaChuva, FechandoGuarda,          // descendo de guarda-chuva / pousou e está fechando
    IndoAoRelogio, OlhandoRelogio,
    NoCanhao, Voando, Comemorando,
    Roleta, Desmaiado
}

/// <summary>
/// Janela do mascote. Esta parte cuida da montagem do personagem, das falas e do mouse;
/// o menu está em JanelaMascote.Menu.cs e a animação (quadro a quadro) em JanelaMascote.Estados.cs.
/// </summary>
public partial class JanelaMascote : Window
{
    sealed class Confete
    {
        public required Rectangle R;
        public required RotateTransform Giro;
        public double X, Y, VX, VY, VR;
    }

    readonly Random rnd = new();
    readonly DispatcherTimer timer;
    readonly ServicoAlertas alertas = new();
    readonly ServicoClima clima = new();
    readonly Brush corBorda;

    // personagem e o que vai junto dele
    Personagem pers = null!;
    GuardaChuva g = null!;
    NuvemChuva nuvem = null!;
    MochilaVisual mochila = null!;
    Ellipse sombra = null!;
    string carimbo = "";
    double balCx, balCy;   // centro do balanço no guarda-chuva

    // estado da animação
    Estado estado = Estado.Caindo;
    int dir = -1;          // -1 = esquerda, 1 = direita
    int t;                 // contador de quadros
    int restante = 200;    // quadros que faltam no estado atual (andando/parado)
    int ga;                // contador de quadros dos estados com roteiro (guarda-chuva, roleta, comemoração...)
    double vx, vy;
    bool pausado;
    int piscar = 90, piscarSegura;
    IntPtr hwnd;

    // falas
    int balao;             // quadros até o balão sumir
    int proximaFala;
    bool alerta;           // o balão está mostrando um alerta recebido
    string falaTexto = ""; // o que ele está dizendo agora (para mexer a boca no ritmo do texto)
    readonly System.Diagnostics.Stopwatch falaRelogio = new();
    double boca;           // abertura atual da boca (0..1), suavizada quadro a quadro

    // canhão e cesta
    Cesta? cesta;
    bool acertou;
    int placar, semGuarda;
    double prevCx, prevCy;
    List<Confete>? confetes;

    // relógio, clima, mochila, roleta
    double relogioX, alvoX;
    bool falarClima;
    bool? chovia;
    string[]? paraGuardar;
    int qtdMochila;
    Arma? arma;
    bool bala, roletaConfirmada;
    int sobrevivencias, levantando;
    double deslize;
    DateTime bloqueouEm = DateTime.MinValue;

    public JanelaMascote()
    {
        InitializeComponent();
        corBorda = Balao.BorderBrush;
        proximaFala = rnd.Next(1800, 5000);
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += Tick;

        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            JanelaNativa.ToolWindow(hwnd);   // some do Alt+Tab
        };
        Loaded += (_, _) =>
        {
            alertas.Iniciar(); clima.Iniciar();
            timer.Start(); Falar("Cheguei :)");
        };
        Closed += (_, _) => { timer.Stop(); alertas.Parar(); cesta?.Fechar(); };
    }

    /// <summary>Monta o personagem e põe a janela no canto do monitor principal. false = não há nenhum personagem.</summary>
    public bool Iniciar()
    {
        if (!Carregar()) return false;
        var wa = SystemParameters.WorkArea;   // começa no monitor principal
        Left = wa.Right - Width - 40;
        Top = wa.Bottom - Height;
        return true;
    }

    // Área útil do monitor onde o mascote está (o ponto embaixo dos pés dele) — assim ele anda/cai em qualquer monitor
    Rect AreaMascote() => Monitores.AreaUtil(Left + Width / 2, Top + Height - 5, this);

    // Muda quando o personagem ou o guarda-chuva é trocado, ou salvo de novo num editor
    static string Carimbo()
    {
        var n = RepositorioPersonagens.Atual();
        if (n == null) return "";
        var c = $"{n}|{File.GetLastWriteTimeUtc(System.IO.Path.Combine(RepositorioPersonagens.Pasta(n), "rig.json")).Ticks}";
        var pg = GuardaChuva.PastaAtual();
        if (pg != "") c += $"|{pg}|{File.GetLastWriteTimeUtc(System.IO.Path.Combine(pg, "guarda.json")).Ticks}";
        return c;
    }

    // Monta o personagem escolhido e ajusta a janela ao tamanho dele
    bool Carregar()
    {
        var nome = RepositorioPersonagens.Atual();
        if (nome == null) return false;
        var p = Personagem.Carregar(RepositorioPersonagens.Pasta(nome));
        double esc = p.AlturaTela / p.Altura;
        double mw = p.Largura * esc, mh = p.Altura * esc;

        Mascot.Children.Clear();
        sombra = new Ellipse { Width = mw * 0.6, Height = Math.Max(4, mh * 0.08), Fill = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)) };
        Canvas.SetLeft(sombra, (mw - sombra.Width) / 2);
        Canvas.SetTop(sombra, mh - sombra.Height / 2 - 1);
        Mascot.Children.Add(sombra);
        p.Raiz.RenderTransform = new ScaleTransform(esc, esc);
        Mascot.Children.Add(p.Raiz);
        Mascot.Width = mw; Mascot.Height = mh;

        // guarda-chuva acima da cabeça, com o cabo encostando no topo do personagem
        g = GuardaChuva.Carregar(GuardaChuva.PastaAtual());
        var pos = g.EncaixarNoPersonagem(mw, mh);
        Mascot.Children.Insert(1, g.Elemento);   // atrás do personagem (logo depois da sombra)
        // balança pendurado no topo da copa (centro relativo à origem 0.5,1 do mascote)
        Balanco.CenterX = pos.PivoX - mw / 2; Balanco.CenterY = pos.PivoY - mh;
        balCx = Balanco.CenterX; balCy = Balanco.CenterY;

        // nuvem de chuva (aparece só quando está chovendo na cidade configurada)
        nuvem = new NuvemChuva(mw, mh);
        Mascot.Children.Add(nuvem);

        // mochila nas costas (atrás do personagem); clicar nela abre a janela da mochila
        mochila = new MochilaVisual(mw, mh, p.Olhando);
        Mascot.Children.Insert(1, mochila);
        mochila.MouseLeftButtonDown += (_, e) => { e.Handled = true; AbrirMochila(); };
        AtualizarMochila();
        double folga = (mh - pos.PivoY) * Math.Sin(20 * Math.PI / 180) + Math.Abs(pos.PivoX - mw / 2);   // quanto ele vai para os lados

        // espaço para o balanço, o guarda-chuva e os balões
        Width = Math.Max(Math.Max(170, mw + 2 * folga + 10), pos.Largura + folga + 10);
        Height = mh + Math.Max(130, 20 - pos.Topo);
        Palco.Width = Width; Palco.Height = Height;
        Canvas.SetLeft(Mascot, (Width - mw) / 2);
        Canvas.SetTop(Mascot, Height - mh - 3);
        Balao.Width = Width - 10;
        Canvas.SetBottom(Balao, mh + 4);

        pers = p; carimbo = Carimbo(); estado = Estado.Caindo; vy = 0;
        return true;
    }

    // ---------- Falas ----------

    static T Sortear<T>(IReadOnlyList<T> lista) => lista[Random.Shared.Next(lista.Count)];

    void Falar(string txt)
    {
        if (alerta || estado == Estado.NoCanhao) return;   // não atropela um alerta, nem fala de dentro do canhão
        TextoBalao.Text = txt;
        Balao.Background = Brushes.White; Balao.BorderBrush = corBorda; Balao.BorderThickness = new Thickness(1.5);
        Balao.Visibility = Visibility.Visible;
        balao = Math.Max(130, QuadrosDeFala(txt) + 45);   // o balão fica pelo menos enquanto ele fala (+ 1,5 s para ler)
        ComecarAFalar(txt);
    }

    // Começa a mexer a boca: dura TempoDeFala.Segundos(texto) — 1 s para cada 8 letras
    void ComecarAFalar(string txt) { falaTexto = txt; falaRelogio.Restart(); }

    static int QuadrosDeFala(string txt) => (int)Math.Ceiling(TempoDeFala.Segundos(txt) * 1000 / 33);

    // Abertura da boca neste quadro (suavizada para não "piscar"); fechada fora da fala, desmaiado ou dentro do canhão
    double AberturaDaBoca()
    {
        double alvo = 0;
        if (falaTexto != "" && estado is not (Estado.Desmaiado or Estado.NoCanhao))
        {
            double s = falaRelogio.Elapsed.TotalSeconds;
            if (s >= TempoDeFala.Segundos(falaTexto)) falaTexto = "";
            else alvo = TempoDeFala.Abertura(falaTexto, s);
        }
        boca += (alvo - boca) * 0.55;
        return boca < 0.01 ? 0 : boca;
    }

    void Falar(params string[] opcoes) => Falar(Sortear(opcoes));

    // Uma das frases do personagem atual
    void FalarFrase() => Falar(Sortear(pers.Frases));

    // Alerta recebido de outra pessoa: pula, toca um som e mostra o balão destacado por ~12 s (clique para fechar)
    void MostrarAlerta(MensagemAlerta msg)
    {
        TextoBalao.Inlines.Clear();
        var quando = string.IsNullOrEmpty(msg.Quando) ? "" : $" ({msg.Quando})";
        TextoBalao.Inlines.Add(new Run($"{msg.Nome}{quando}:") { FontWeight = FontWeights.Bold, Foreground = Brushes.Firebrick });
        TextoBalao.Inlines.Add(new LineBreak());
        TextoBalao.Inlines.Add(new Run(msg.Texto));
        Balao.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xD6));
        Balao.BorderBrush = Brushes.Firebrick; Balao.BorderThickness = new Thickness(2.5);
        Balao.Visibility = Visibility.Visible;
        alerta = true; balao = Math.Max(360, QuadrosDeFala(msg.Texto) + 60);
        ComecarAFalar(msg.Texto);   // ele "lê" o alerta em voz alta
        estado = Estado.Pulando; vy = -9;
        SystemSounds.Exclamation.Play();
    }

    void FecharBalao()
    {
        Balao.Visibility = Visibility.Collapsed; balao = 0; alerta = false;
        falaTexto = "";   // fechou o balão: para de falar
    }

    void Balao_Clique(object sender, MouseButtonEventArgs e) => FecharBalao();

    // O giro do mascote é usado de dois jeitos: balançando no guarda-chuva (centro no topo da copa)
    // e rodopiando quando sai do canhão (centro no meio do corpo). Isto volta para o do guarda-chuva.
    void RestaurarGiro()
    {
        Balanco.Angle = 0; Balanco.CenterX = balCx; Balanco.CenterY = balCy;
    }

    void GuardarGuardaChuva()
    {
        g.Elemento.Visibility = Visibility.Collapsed; RestaurarGiro();
    }

    // ---------- Mouse: arrastar / duplo clique ----------

    void Mascot_Clique(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            if (estado is Estado.GuardaChuva or Estado.FechandoGuarda or Estado.IndoAoRelogio or Estado.OlhandoRelogio
                       or Estado.Voando or Estado.NoCanhao or Estado.Roleta or Estado.Desmaiado) return;
            estado = Estado.Pulando; vy = -7; acertou = false;   // interrompeu a comemoração: não comemora de novo depois
            FalarFrase();
        }
        else
        {
            if (estado is Estado.NoCanhao or Estado.Roleta or Estado.Desmaiado) return;   // na roleta não dá para arrastar
            FecharBalao();
            try { DragMove(); } catch { }
            RestaurarGiro(); vy = 0; ga = 0;
            acertou = false;   // pegou com a mão: a cesta (se houve) já era — o próximo pouso não é comemoração
            // solto lá de cima: abre o guarda-chuva; de pertinho: só cai
            double altura = (AreaMascote().Bottom - Height) - Top;
            if (altura > 80)
            {
                estado = Estado.GuardaChuva; g.Elemento.Visibility = Visibility.Visible; sombra.Visibility = Visibility.Hidden;
                g.Abre.ScaleX = 0; g.Abre.ScaleY = 0;
            }
            else
            {
                // pertinho do chão: só cai — e fecha o guarda-chuva se ele estava aberto (pego no meio da descida)
                estado = Estado.Caindo; g.Elemento.Visibility = Visibility.Collapsed; sombra.Visibility = Visibility.Visible;
            }
        }
    }

    // ---------- Mochila ----------

    void AtualizarMochila()
    {
        qtdMochila = ServicoMochila.Itens().Length;
        mochila.MostrarQuantidade(qtdMochila);
    }

    void AbrirMochila()
    {
        new JanelaMochila().ShowDialog();
        AtualizarMochila();
    }

    // Arrastar arquivos até o mascote: ele para, abre a mochila e guarda uma cópia
    static bool TemArquivos(DragEventArgs e) => e.Data.GetDataPresent(DataFormats.FileDrop);

    void Mascot_DragEnter(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!TemArquivos(e)) { e.Effects = DragDropEffects.None; return; }
        e.Effects = DragDropEffects.Copy;
        if (!mochila.Aberta)
        {
            mochila.Aberta = true; AtualizarMochila();
            if (estado == Estado.Andando) estado = Estado.Parado;
            Falar("Pode mandar!");
        }
    }

    void Mascot_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TemArquivos(e) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
    }

    void Mascot_DragLeave(object sender, DragEventArgs e)
    {
        var p = e.GetPosition(Mascot);   // passar de uma parte do desenho para outra também dispara "saiu": ignora
        if (p.X >= 0 && p.Y >= 0 && p.X <= Mascot.Width && p.Y <= Mascot.Height) return;
        mochila.Aberta = false; AtualizarMochila();
    }

    void Mascot_Drop(object sender, DragEventArgs e)
    {
        mochila.Aberta = false; e.Handled = true;
        if (!TemArquivos(e)) { AtualizarMochila(); return; }
        // o que já está na mochila não entra de novo; a cópia em si é feita no próximo quadro (libera o Explorer na hora)
        paraGuardar = ServicoMochila.DeFora((string[])e.Data.GetData(DataFormats.FileDrop));
    }

    // ---------- Canhão ----------

    // O mascote "entra" no canhão (some), e é atirado da boca do cano com a velocidade escolhida
    void AoAtirar(double x, double y, double velX, double velY)
    {
        Mascot.Visibility = Visibility.Visible;
        Left = x - Width / 2;                     // centro do personagem na boca do cano
        Top = y - (Height - Mascot.Height / 2 - 3);
        vx = velX; vy = velY; ga = 0;
        dir = velX >= 0 ? 1 : -1;
        Balanco.Angle = 0; Balanco.CenterX = 0; Balanco.CenterY = -Mascot.Height / 2;   // rodopia em volta do meio do corpo
        sombra.Visibility = Visibility.Hidden;
        estado = Estado.Voando; acertou = false;
        cesta?.Ordenar(hwnd);   // para ele passar por dentro do aro
        prevCx = x; prevCy = y;
        Falar("Weeeee!", "Lá vou eu!", "Uhuuuul!", "Socorrooo!");
    }

    void AoCancelarCanhao()
    {
        Mascot.Visibility = Visibility.Visible; estado = Estado.Parado; restante = 40;
        cesta?.Fechar(); cesta = null;
    }

    // Confetes da comemoração (pedacinhos coloridos que voam e caem dentro da janela do mascote)
    void SoltarConfete()
    {
        CamadaConfete.Children.Clear(); confetes = [];
        string[] cores = ["#E53935", "#FDD835", "#43A047", "#1E88E5", "#8E24AA", "#FB8C00"];
        for (int i = 0; i < 30; i++)
        {
            var giro = new RotateTransform(rnd.Next(360), 2, 4);
            var r = new Rectangle { Width = 4 + rnd.Next(3), Height = 7 + rnd.Next(4), Fill = Imagens.Pincel(Sortear(cores)), RenderTransform = giro };
            CamadaConfete.Children.Add(r);
            confetes.Add(new Confete
            {
                R = r, Giro = giro, X = Width / 2, Y = Height - Mascot.Height,
                VX = (rnd.NextDouble() - 0.5) * 9, VY = -5 - rnd.NextDouble() * 8, VR = (rnd.NextDouble() - 0.5) * 30
            });
        }
    }
}
