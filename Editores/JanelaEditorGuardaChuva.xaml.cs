using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Mascote.GuardaChuvas;
using Mascote.Nucleo;
using Mascote.Personagens;
using Mascote.Rig;
using Microsoft.Win32;

namespace Mascote.Editores;

/// <summary>Editor de Guarda-chuva: marca onde o personagem segura e de onde balança, e grava a pasta do guarda-chuva.</summary>
public partial class JanelaEditorGuardaChuva : Window
{
    readonly string pastaPrevia = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mascote_guarda_previa");
    readonly DispatcherTimer timer;

    ImgData? img;
    double pegaX, pegaY, topoX, topoY;
    string pasta = "";
    bool ocupado;

    // prévia animada
    Personagem? pers;
    RotateTransform? giro;
    int t;

    public JanelaEditorGuardaChuva()
    {
        ocupado = true;
        InitializeComponent();
        ocupado = false;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += Tick;
        timer.Start();
        Closed += (_, _) => timer.Stop();
    }

    // Sugestão: topo = ponta de cima do desenho, mão = ponta de baixo (o fim do cabo)
    void AcharPontos()
    {
        int W = img!.W, H = img.H; var px = img.Px;
        (double X, double Y)? Ponta(bool deCima)
        {
            for (int k = 0; k < H; k++)
            {
                int y = deCima ? k : H - 1 - k;
                double soma = 0; int n = 0;
                for (int x = 0; x < W; x++) if (px[(y * W + x) * 4 + 3] > 24) { soma += x; n++; }
                if (n > 0) return (soma / n + 0.5, deCima ? y + 2 : y - 2);   // desce/sobe um pouquinho para dentro do desenho
            }
            return null;
        }
        if (Ponta(true) is { } topo) (topoX, topoY) = topo;
        if (Ponta(false) is { } pega) (pegaX, pegaY) = pega;
    }

    void MostrarPontos()
    {
        Overlay.Children.Clear();
        if (img == null) return;
        double esp = Math.Max(1, img.W / 200.0);
        Overlay.Children.Add(new Line
        {
            X1 = topoX, Y1 = topoY, X2 = pegaX, Y2 = pegaY, Stroke = Brushes.Gray, StrokeThickness = esp, StrokeDashArray = [3, 2]
        });
        foreach (var (x, y, cor, nome) in new[] { (pegaX, pegaY, "#E53935", "mão"), (topoX, topoY, "#1E88E5", "topo") })
        {
            double r = esp * 5;
            var c = new Ellipse { Width = r * 2, Height = r * 2, Fill = Brushes.White, Stroke = Imagens.Pincel(cor), StrokeThickness = esp * 2 };
            Canvas.SetLeft(c, x - r); Canvas.SetTop(c, y - r);
            Overlay.Children.Add(c);
            var tb = new TextBlock
            {
                Text = nome, FontSize = Math.Max(7, img.H / 22.0), Foreground = Brushes.White,
                Background = Imagens.Pincel(cor), Padding = new Thickness(3, 0, 3, 0)
            };
            Canvas.SetLeft(tb, x + r * 1.4); Canvas.SetTop(tb, y - tb.FontSize * 0.7);
            Overlay.Children.Add(tb);
        }
    }

    void Gravar(string dir, string nome) =>
        GuardaChuva.Salvar(img!, dir, new GuardaJson { Nome = nome, PegaX = pegaX, PegaY = pegaY, TopoX = topoX, TopoY = topoY, Tamanho = SldTam.Value / 100 });

    // Mostra o personagem atual pendurado no guarda-chuva, balançando
    void AtualizarPrevia()
    {
        Previa.Children.Clear(); pers = null;
        if (img == null) return;
        try
        {
            Gravar(pastaPrevia, "previa");
            var nome = RepositorioPersonagens.Atual();
            if (nome == null) { Status.Text = "Crie um personagem no Editor de Personagens para ver a prévia."; return; }
            var p = Personagem.Carregar(RepositorioPersonagens.Pasta(nome));
            double esc = p.AlturaTela / p.Altura, mw = p.Largura * esc, mh = p.Altura * esc;
            p.Raiz.RenderTransform = new ScaleTransform(esc, esc);
            var m = new Canvas { Width = mw, Height = mh };
            m.Children.Add(p.Raiz);
            var g = GuardaChuva.Carregar(pastaPrevia);
            var pos = g.EncaixarNoPersonagem(mw, mh);
            g.Elemento.Visibility = Visibility.Visible; g.Abre.ScaleX = 1; g.Abre.ScaleY = 1;
            m.Children.Add(g.Elemento);
            giro = new RotateTransform(0, pos.PivoX, pos.PivoY);
            m.RenderTransform = giro;
            // encaixa o conjunto (do topo do guarda-chuva até os pés) na área da prévia
            double alt = mh - pos.Topo;
            double z = Math.Min(2.5, Math.Min(250 / alt, 190 / Math.Max(mw, pos.Largura)));
            var fora = new Canvas { RenderTransform = new ScaleTransform(z, z) };
            Canvas.SetTop(m, -pos.Topo);
            fora.Children.Add(m);
            double larg = Math.Max(300, Previa.ActualWidth);
            Canvas.SetLeft(fora, (larg - mw * z) / 2); Canvas.SetTop(fora, 20);
            Previa.Children.Add(fora);
            pers = p;
        }
        catch (Exception ex) { Status.Text = $"Erro ao gerar a prévia: {Opcoes.MsgErro(ex)}"; }
    }

    void DefinirImagem(ImgData nova)
    {
        img = nova;
        Img.Source = RigTools.ToBitmap(nova); Img.Width = nova.W; Img.Height = nova.H;
        Stage.Width = nova.W; Stage.Height = nova.H;
    }

    // ---------- Botões ----------

    void Abrir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Imagens (*.png;*.gif;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.gif;*.jpg;*.jpeg;*.bmp;*.webp|Todos os arquivos|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        ImgData nova;
        try { nova = RigTools.Load(dlg.FileName, 256); }
        catch (Exception ex)
        {
            var msg = Opcoes.MsgErro(ex);
            if (!msg.Contains("fundo transparente")) { MessageBox.Show(this, msg, "Não deu para abrir a imagem"); return; }
            var r = MessageBox.Show(this, "Essa imagem não tem fundo transparente (muitas imagens 'PNG' da internet vêm com o xadrez desenhado ou fundo branco).\n\n" +
                                          "Quer que eu tente apagar o fundo automaticamente?", "Abrir imagem", MessageBoxButton.YesNo);
            if (r != MessageBoxResult.Yes) return;
            try { nova = RigTools.LoadSemFundo(dlg.FileName, 256); }
            catch (Exception ex2) { MessageBox.Show(this, Opcoes.MsgErro(ex2), "Não deu para apagar o fundo"); return; }
        }
        DefinirImagem(nova); AcharPontos();
        TxtNome.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName); pasta = "";
        ocupado = true; SldTam.Value = 115; ocupado = false;
        MostrarPontos(); AtualizarPrevia();
        Status.Text = "Marquei a mão (vermelho) e o topo (azul) automaticamente. Confira na prévia e ajuste clicando no desenho, se precisar.";
    }

    void Editar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Guarda-chuva (guarda.json)|guarda.json", InitialDirectory = Caminhos.PastaGuardaChuvas };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(dlg.FileName)!;
            var j = Json.Ler<GuardaJson>(dlg.FileName)!;
            DefinirImagem(RigTools.Load(System.IO.Path.Combine(dir, "imagem.png"), 256));
            pegaX = j.PegaX; pegaY = j.PegaY; topoX = j.TopoX; topoY = j.TopoY;
            ocupado = true; SldTam.Value = j.Tamanho * 100; ocupado = false;
            TxtNome.Text = j.Nome; pasta = dir;
            MostrarPontos(); AtualizarPrevia();
            Status.Text = $"Editando '{j.Nome}'.";
        }
        catch (Exception ex) { MessageBox.Show(this, Opcoes.MsgErro(ex), "Não deu para abrir o guarda-chuva"); }
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (img == null) { Status.Text = "Abra uma imagem primeiro."; return; }
        var nome = TxtNome.Text.Trim();
        var nomePasta = Opcoes.NomeDePasta(nome);
        if (nomePasta == "") { MessageBox.Show(this, "Dê um nome ao guarda-chuva.", "Salvar"); return; }
        var dir = System.IO.Path.Combine(Caminhos.PastaGuardaChuvas, nomePasta);
        if (File.Exists(System.IO.Path.Combine(dir, "guarda.json")) && pasta != dir &&
            MessageBox.Show(this, $"Já existe um guarda-chuva chamado '{nomePasta}'. Substituir?", "Salvar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { Gravar(dir, nome); }
        catch (Exception ex) { MessageBox.Show(this, Opcoes.MsgErro(ex), "Erro ao salvar"); return; }
        pasta = dir;
        // se for o guarda-chuva em uso, o mascote percebe sozinho e recarrega
        bool emUso = string.Equals(GuardaChuva.PastaAtual(), dir, StringComparison.OrdinalIgnoreCase);
        Status.Text = $"Guarda-chuva '{nomePasta}' salvo. " +
                      (emUso ? "O mascote já foi atualizado." : "Para usá-lo, clique com o botão direito no mascote e escolha Trocar guarda-chuva.");
    }

    // ---------- Ajustes ----------

    void SldTam_Mudou(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblTam == null) return;   // ainda montando a janela
        LblTam.Text = $"{(int)SldTam.Value}%";
        if (!ocupado) AtualizarPrevia();
    }

    void Stage_Clique(object sender, MouseButtonEventArgs e)
    {
        if (img == null) return;
        var pt = e.GetPosition(Stage);
        if (RbMao.IsChecked == true) { pegaX = pt.X; pegaY = pt.Y; } else { topoX = pt.X; topoY = pt.Y; }
        MostrarPontos(); AtualizarPrevia();
    }

    // ---------- Animação da prévia ----------

    void Tick(object? sender, EventArgs e)
    {
        if (pers == null || giro == null) return;
        t++;
        giro.Angle = Math.Sin(t * 0.07) * 18;
        pers.AplicarPose(Pose.Guarda, t, false);
    }
}
