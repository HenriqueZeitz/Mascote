using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mascote.Nucleo;
using Mascote.Personagens;
using Mascote.Rig;
using Microsoft.Win32;

namespace Mascote.Editores;

/// <summary>Editor de Personagens: marca os membros de um PNG e grava a pasta do personagem.</summary>
public partial class JanelaEditorPersonagem : Window
{
    static readonly Dictionary<string, string> Cores = new()
    {
        [Papeis.Cabeca] = "#E53935", [Papeis.Olhos] = "#8E24AA", [Papeis.Boca] = "#D81B60", [Papeis.Braco] = "#1E88E5",
        [Papeis.Pe] = "#43A047", [Papeis.Cauda] = "#FB8C00"
    };
    static readonly Dictionary<string, string> Curtos = new()
    {
        [Papeis.Cabeca] = "Cabeça", [Papeis.Olhos] = "Olhos", [Papeis.Boca] = "Boca", [Papeis.Braco] = "Braço/asa", [Papeis.Pe] = "Pé",
        [Papeis.Cauda] = "Cauda"
    };

    readonly string pastaPrevia = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mascote_previa");
    readonly List<ParteEdicao> partes = [];
    readonly DispatcherTimer timer;
    readonly Random rnd = new();

    ImgData? img;
    int sel = -1;
    (double X0, double Y0, double X1, double Y1)? arrasto;
    List<Point>? laco;         // contorno sendo desenhado (formato livre)
    bool ocupado;              // mexendo nos controles pelo código: ignora os eventos deles
    string pastaCarregada = "";

    // prévia animada
    Personagem? pers;
    TranslateTransform? pulo;
    int t, pisca = 90, piscaSegura;

    public JanelaEditorPersonagem()
    {
        ocupado = true;
        InitializeComponent();
        Opcoes.Adicionar(CmbPapel, Papeis.Nomes);
        Opcoes.Adicionar(CmbForma, [new(Formas.Retangulo, "Retângulo"), new(Formas.Elipse, "Elipse"), new(Formas.Livre, "Contorno livre (laço)")]);
        Opcoes.Adicionar(CmbOlhando, [new(Olhando.Direita, "Direita"), new(Olhando.Esquerda, "Esquerda"), new(Olhando.Frente, "Frente")]);
        Opcoes.Adicionar(CmbEstado, [new("andar", "Prévia: andando"), new("parado", "Prévia: parado"), new("pular", "Prévia: pulando"),
                                     new("falar", "Prévia: falando (as frases abaixo)")]);
        CmbForma.SelectedIndex = 0; CmbOlhando.SelectedIndex = 0; CmbEstado.SelectedIndex = 0;
        ocupado = false;

        KeyDown += (_, e) => { if (e.Key == Key.Delete && e.OriginalSource is not TextBox) Remover(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += Tick;
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    string OlhandoEscolhido => Opcoes.Selecionada(CmbOlhando);

    // Frases digitadas (uma por linha); em branco = frases padrão
    List<string> Frases() => TxtFrases.Text.Split('\n').Select(f => f.Trim()).Where(f => f != "").ToList();

    void MostrarFrases(IEnumerable<string>? frases) =>
        TxtFrases.Text = string.Join(Environment.NewLine, frases?.Any() == true ? frases : FrasesPadrao.Lista);

    // Desenha as áreas dos membros por cima da imagem
    void MostrarPartes()
    {
        Overlay.Children.Clear();
        if (img == null) return;
        double esp = Math.Max(1, img.W / 220.0);
        for (int i = 0; i < partes.Count; i++)
        {
            var p = partes[i]; var cor = Cores[p.Papel];
            Shape sh;
            if (p.Forma == Formas.Livre && p.Pontos != null) sh = new Polygon { Points = new PointCollection(p.Pontos), StrokeLineJoin = PenLineJoin.Round };
            else
            {
                sh = p.Forma == Formas.Elipse ? new Ellipse() : new Rectangle();
                sh.Width = Math.Max(1, p.W); sh.Height = Math.Max(1, p.H);
                Canvas.SetLeft(sh, p.X); Canvas.SetTop(sh, p.Y);
            }
            sh.Stroke = Imagens.Pincel(cor);
            if (i == sel) { sh.StrokeThickness = esp * 2; sh.Fill = Imagens.Pincel(cor, 45); }
            else { sh.StrokeThickness = esp; sh.StrokeDashArray = [3, 2]; }
            Overlay.Children.Add(sh);

            if (i == sel)   // o nome só aparece no membro selecionado, para não ficar em cima do desenho
            {
                var tb = new TextBlock
                {
                    Text = $"{i + 1} {Curtos[p.Papel]}", FontSize = Math.Max(6, img.H / 24.0),
                    Foreground = Brushes.White, Background = Imagens.Pincel(cor), Padding = new Thickness(2, 0, 2, 0)
                };
                Canvas.SetLeft(tb, Math.Max(0, p.X)); Canvas.SetTop(tb, Math.Max(0, p.Y - tb.FontSize * 1.4));
                Overlay.Children.Add(tb);
            }

            double r = esp * 3.5;
            var pv = new Ellipse { Width = r * 2, Height = r * 2, Fill = Brushes.White, Stroke = Imagens.Pincel(cor), StrokeThickness = esp * 1.3 };
            Canvas.SetLeft(pv, p.PX - r); Canvas.SetTop(pv, p.PY - r);
            Overlay.Children.Add(pv);
        }
        if (laco != null)
        {
            Overlay.Children.Add(new Polyline
            {
                Points = new PointCollection(laco), Stroke = Brushes.Black, StrokeThickness = esp, StrokeDashArray = [4, 2], StrokeLineJoin = PenLineJoin.Round
            });
        }
        else if (arrasto is { } d)
        {
            var rc = new Rectangle
            {
                Width = Math.Abs(d.X1 - d.X0), Height = Math.Abs(d.Y1 - d.Y0),
                Stroke = Brushes.Black, StrokeThickness = esp, StrokeDashArray = [4, 2]
            };
            Canvas.SetLeft(rc, Math.Min(d.X0, d.X1)); Canvas.SetTop(rc, Math.Min(d.Y0, d.Y1));
            Overlay.Children.Add(rc);
        }
    }

    void AtualizarLista()
    {
        ocupado = true;
        LstPartes.Items.Clear();
        for (int i = 0; i < partes.Count; i++) LstPartes.Items.Add($"{i + 1}. {Papeis.Nome(partes[i].Papel)}");
        LstPartes.SelectedIndex = sel;
        PainelParte.IsEnabled = sel >= 0;
        if (sel >= 0)
        {
            var p = partes[sel];
            Opcoes.Selecionar(CmbPapel, p.Papel); Opcoes.Selecionar(CmbForma, p.Forma); ChkPreencher.IsChecked = p.Preencher;
        }
        ocupado = false;
        MostrarPartes();
    }

    // Gera o personagem numa pasta temporária e mostra animado
    void AtualizarPrevia()
    {
        Previa.Children.Clear(); pers = null;
        if (img == null) return;
        try
        {
            RepositorioPersonagens.Salvar(img, partes, pastaPrevia, "previa", 72, OlhandoEscolhido, null);
            var p = Personagem.Carregar(pastaPrevia);
            double esc = Math.Min(140 / p.Altura, 270 / p.Largura);
            p.Raiz.RenderTransform = new ScaleTransform(esc, esc);
            double larg = Math.Max(280, Previa.ActualWidth);
            var sombra = new Ellipse { Width = p.Largura * esc * 0.6, Height = 8, Fill = new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0)) };
            Canvas.SetLeft(sombra, (larg - sombra.Width) / 2); Canvas.SetTop(sombra, 166);
            pulo = new TranslateTransform();
            var hold = new Canvas { RenderTransform = pulo };
            hold.Children.Add(p.Raiz);
            Canvas.SetLeft(hold, (larg - p.Largura * esc) / 2); Canvas.SetTop(hold, 170 - p.Altura * esc);
            Previa.Children.Add(sombra); Previa.Children.Add(hold);
            pers = p;
        }
        catch (Exception ex) { Status.Text = $"Erro ao gerar a prévia: {ex.Message}"; }
    }

    void DefinirImagem(ImgData nova)
    {
        img = nova; partes.Clear(); sel = -1;
        Img.Source = RigTools.ToBitmap(nova); Img.Width = nova.W; Img.Height = nova.H;
        Stage.Width = nova.W; Stage.Height = nova.H;
    }

    void Detectar()
    {
        var (achadas, olhando) = RepositorioPersonagens.Sugerir(img!);
        partes.Clear(); partes.AddRange(achadas);
        ocupado = true; Opcoes.Selecionar(CmbOlhando, olhando); ocupado = false;
        sel = -1; AtualizarLista(); AtualizarPrevia();
        Status.Text = partes.Count > 0
            ? $"Encontrei {partes.Count} membro(s): {string.Join(", ", partes.Select(p => Curtos[p.Papel]))}. Confira na prévia, ajuste o que precisar e clique em Salvar."
            : "Não consegui identificar membros automaticamente. Clique em '+ Novo membro' e arraste no desenho para marcar cada um.";
    }

    // ---------- Botões ----------

    void Abrir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Imagens PNG (*.png)|*.png|Todas as imagens|*.png;*.gif;*.bmp;*.jpg;*.jpeg" };
        if (dlg.ShowDialog(this) != true) return;
        ImgData nova;
        try { nova = RigTools.Load(dlg.FileName, 256); }
        catch (Exception ex) { MessageBox.Show(this, Opcoes.MsgErro(ex), "Não deu para abrir a imagem"); return; }
        DefinirImagem(nova);
        TxtNome.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        MostrarFrases(null);   // personagem novo começa com as frases padrão
        pastaCarregada = "";
        Detectar();
    }

    void Editar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Personagem (rig.json)|rig.json", InitialDirectory = Caminhos.PastaPersonagens };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(dlg.FileName)!;
            var rig = Json.Ler<RigJson>(dlg.FileName)!;
            DefinirImagem(RigTools.Load(System.IO.Path.Combine(dir, rig.Original), 256));
            foreach (var p in rig.Partes)
            {
                partes.Add(new ParteEdicao
                {
                    Papel = p.Papel, Forma = p.Forma, X = p.X, Y = p.Y, W = p.W, H = p.H, PX = p.PivoX, PY = p.PivoY,
                    Pontos = Contorno(p.Pontos), Preencher = p.Preencher, PivoManual = true
                });
            }
            ocupado = true; Opcoes.Selecionar(CmbOlhando, rig.Olhando); ocupado = false;
            TxtNome.Text = rig.Nome; TxtAltura.Text = ((int)rig.AlturaTela).ToString();
            MostrarFrases(rig.Frases);
            pastaCarregada = dir;
            AtualizarLista(); AtualizarPrevia();
            Status.Text = $"Editando '{rig.Nome}'.";
        }
        catch (Exception ex) { MessageBox.Show(this, Opcoes.MsgErro(ex), "Não deu para abrir o personagem"); }
    }

    static List<Point>? Contorno(double[]? xy)
    {
        if (xy == null || xy.Length < 6) return null;
        var pts = new List<Point>();
        for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Point(xy[i], xy[i + 1]));
        return pts;
    }

    void Detectar_Click(object sender, RoutedEventArgs e)
    {
        if (img == null) return;
        if (partes.Count > 0 && MessageBox.Show(this, "Isso substitui os membros atuais pela detecção automática. Continuar?", "Detectar membros",
                                                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Detectar();
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (img == null) { Status.Text = "Abra uma imagem primeiro."; return; }
        var nome = TxtNome.Text.Trim();
        var pasta = Opcoes.NomeDePasta(nome);
        if (pasta == "") { MessageBox.Show(this, "Dê um nome ao personagem.", "Salvar"); return; }
        if (!int.TryParse(TxtAltura.Text, out int alt) || alt < 24 || alt > 400)
        {
            MessageBox.Show(this, "A altura na tela deve ser um número entre 24 e 400.", "Salvar"); return;
        }
        var dir = RepositorioPersonagens.Pasta(pasta);
        if (File.Exists(System.IO.Path.Combine(dir, "rig.json")) && pastaCarregada != dir &&
            MessageBox.Show(this, $"Já existe um personagem chamado '{pasta}'. Substituir?", "Salvar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { RepositorioPersonagens.Salvar(img, partes, dir, nome, alt, OlhandoEscolhido, Frases()); }
        catch (Exception ex) { MessageBox.Show(this, Opcoes.MsgErro(ex), "Erro ao salvar"); return; }
        pastaCarregada = dir;
        // se for o personagem em uso, o mascote percebe sozinho e recarrega
        bool emUso = string.Equals(RepositorioPersonagens.Atual(), pasta, StringComparison.OrdinalIgnoreCase);
        Status.Text = $"Personagem '{pasta}' salvo. " +
                      (emUso ? "O mascote já foi atualizado." : "Para usá-lo, clique com o botão direito no mascote e escolha Trocar personagem.");
    }

    void Nova_Click(object sender, RoutedEventArgs e)
    {
        sel = -1; AtualizarLista();
        Status.Text = "Arraste no desenho para marcar a área do novo membro.";
    }

    void Remover_Click(object sender, RoutedEventArgs e) => Remover();

    void Remover()
    {
        if (sel < 0) return;
        partes.RemoveAt(sel); sel = -1; AtualizarLista(); AtualizarPrevia();
    }

    // ---------- Propriedades do membro selecionado ----------

    void LstPartes_Mudou(object sender, SelectionChangedEventArgs e)
    {
        if (ocupado) return;
        sel = LstPartes.SelectedIndex; AtualizarLista();
    }

    void CmbPapel_Mudou(object sender, SelectionChangedEventArgs e)
    {
        if (ocupado || sel < 0) return;
        var p = partes[sel]; p.Papel = Opcoes.Selecionada(CmbPapel);
        if (Papeis.VaiNaCabeca(p.Papel)) p.Preencher = true;   // olhos e boca são desenhados por cima da cabeça
        if (!p.PivoManual) RepositorioPersonagens.PivoPadrao(p, OlhandoEscolhido);
        AtualizarLista(); AtualizarPrevia();
    }

    void CmbForma_Mudou(object sender, SelectionChangedEventArgs e)
    {
        if (ocupado || sel < 0) return;   // sem seleção, só escolhe como o próximo membro será marcado
        partes[sel].MudarForma(Opcoes.Selecionada(CmbForma)); MostrarPartes(); AtualizarPrevia();
    }

    void ChkPreencher_Click(object sender, RoutedEventArgs e)
    {
        if (sel < 0) return;
        partes[sel].Preencher = ChkPreencher.IsChecked == true; AtualizarPrevia();
    }

    void CmbOlhando_Mudou(object sender, SelectionChangedEventArgs e)
    {
        if (ocupado) return;
        foreach (var p in partes) if (!p.PivoManual) RepositorioPersonagens.PivoPadrao(p, OlhandoEscolhido);
        MostrarPartes(); AtualizarPrevia();
    }

    // ---------- Mouse no desenho ----------

    void Stage_BotaoDesce(object sender, MouseButtonEventArgs e)
    {
        if (img == null) return;
        var pt = e.GetPosition(Stage);
        arrasto = (pt.X, pt.Y, pt.X, pt.Y);
        laco = Opcoes.Selecionada(CmbForma) == Formas.Livre ? [pt] : null;
        Stage.CaptureMouse();
    }

    void Stage_MouseMove(object sender, MouseEventArgs e)
    {
        if (arrasto is not { } d) return;
        var pt = e.GetPosition(Stage);
        arrasto = (d.X0, d.Y0, pt.X, pt.Y);
        if (laco != null && img != null)
        {
            // um ponto novo só quando o mouse andou um pouco (contorno leve, sem pontos amontoados)
            var u = laco[^1]; double passo = Math.Max(1, img.W / 150.0);
            if (Math.Abs(pt.X - u.X) + Math.Abs(pt.Y - u.Y) >= passo)
                laco.Add(new Point(Math.Clamp(pt.X, 0, img.W), Math.Clamp(pt.Y, 0, img.H)));
        }
        MostrarPartes();
    }

    void Stage_BotaoSobe(object sender, MouseButtonEventArgs e)
    {
        if (arrasto is not { } d || img == null) return;
        Stage.ReleaseMouseCapture();
        var contorno = laco;
        arrasto = null; laco = null;
        double x0 = Math.Max(0, Math.Min(d.X0, d.X1)), y0 = Math.Max(0, Math.Min(d.Y0, d.Y1));
        double x1 = Math.Min(img.W, Math.Max(d.X0, d.X1)), y1 = Math.Min(img.H, Math.Max(d.Y0, d.Y1));
        if (contorno != null)   // no laço, o que vale é a caixa em volta de todo o contorno
        {
            x0 = contorno.Min(q => q.X); y0 = contorno.Min(q => q.Y); x1 = contorno.Max(q => q.X); y1 = contorno.Max(q => q.Y);
        }
        if (x1 - x0 < 3 && y1 - y0 < 3)
        {
            // clique simples: seleciona o menor membro que contém o ponto
            int hit = -1; double menor = double.MaxValue;
            for (int i = 0; i < partes.Count; i++)
            {
                var c = partes[i];
                if (d.X0 >= c.X && d.X0 <= c.X + c.W && d.Y0 >= c.Y && d.Y0 <= c.Y + c.H && c.W * c.H < menor) { hit = i; menor = c.W * c.H; }
            }
            sel = hit; AtualizarLista();
            return;
        }
        if (contorno is { Count: < 3 }) { MostrarPartes(); return; }   // rabisco curto demais para ser um contorno
        if (sel < 0)
        {
            partes.Add(new ParteEdicao { Forma = Opcoes.Selecionada(CmbForma) });
            sel = partes.Count - 1;
            Status.Text = "Novo membro criado. Escolha o tipo dele ao lado e, se precisar, ajuste o ponto de giro com o botão direito.";
        }
        var p = partes[sel];
        if (contorno != null) p.DefinirContorno(contorno);
        else
        {
            p.MudarForma(Opcoes.Selecionada(CmbForma));
            p.X = x0; p.Y = y0; p.W = x1 - x0; p.H = y1 - y0;
        }
        if (!p.PivoManual) RepositorioPersonagens.PivoPadrao(p, OlhandoEscolhido);
        AtualizarLista(); AtualizarPrevia();
    }

    void Stage_BotaoDireito(object sender, MouseButtonEventArgs e)
    {
        if (sel < 0) { Status.Text = "Selecione um membro antes de posicionar o ponto de giro."; return; }
        var pt = e.GetPosition(Stage); var p = partes[sel];
        p.PX = pt.X; p.PY = pt.Y; p.PivoManual = true;
        MostrarPartes(); AtualizarPrevia();
    }

    // ---------- Animação da prévia ----------

    void Tick(object? sender, EventArgs e)
    {
        if (pers == null || pulo == null) return;
        t++;
        var estado = Opcoes.Selecionada(CmbEstado);
        var pose = estado switch { "andar" => Pose.Andar, "pular" => Pose.Pular, _ => Pose.Parado };
        double y = 0;
        if (estado == "andar") y = -Math.Abs(Math.Sin(t * 0.3)) * 3;
        else if (estado == "pular")
        {
            int c = t % 50;
            if (c < 30) y = -Math.Sin(Math.PI * c / 30) * 30; else pose = Pose.Parado;
        }
        pulo.Y = y;
        if (piscaSegura > 0) { if (--piscaSegura == 0) pisca = rnd.Next(40, 120); }
        else if (--pisca <= 0) piscaSegura = 4;
        pers.AplicarPose(pose, t, piscaSegura > 0, estado == "falar" ? AberturaPrevia() : 0);
    }

    // "Prévia: falando": diz as frases do personagem uma atrás da outra, com meio segundo de pausa entre elas
    int fraseDaPrevia;
    double inicioDaFrase;
    double AberturaPrevia()
    {
        var frases = Frases();
        if (frases.Count == 0) return 0;
        double agora = t * 0.033;
        var frase = frases[fraseDaPrevia % frases.Count];
        double s = agora - inicioDaFrase;
        if (s > TempoDeFala.Segundos(frase) + 0.5) { fraseDaPrevia++; inicioDaFrase = agora; s = 0; }
        return TempoDeFala.Abertura(frase, s);
    }
}
