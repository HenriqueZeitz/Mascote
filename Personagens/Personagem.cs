using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using Mascote.Nucleo;

namespace Mascote.Personagens;

/// <summary>Um membro montado: a camada dele e as transformações que a animação mexe.</summary>
public sealed class NoMembro
{
    public required string Papel;
    public required Canvas Canvas;
    public required ScaleTransform Escala;
    public required RotateTransform Giro;
    public required TranslateTransform Mov;
    public required ParteJson P;
    public int Pai;
    public int Fase;
}

/// <summary>Personagem montado em WPF a partir de uma pasta (rig.json + camadas PNG).</summary>
public sealed class Personagem
{
    public required Canvas Raiz { get; init; }
    public required IReadOnlyList<NoMembro> Nos { get; init; }
    public double Largura { get; init; }
    public double Altura { get; init; }
    public double AlturaTela { get; init; }
    public string Olhando { get; init; } = Personagens.Olhando.Direita;
    public IReadOnlyList<string> Frases { get; init; } = FrasesPadrao.Lista;

    public static Personagem Carregar(string dir)
    {
        var rig = Json.Ler<RigJson>(Path.Combine(dir, "rig.json")) ?? throw new InvalidDataException("rig.json vazio.");
        double W = rig.Largura, H = rig.Altura;
        var nos = new List<NoMembro>();
        foreach (var p in rig.Partes)
        {
            var cv = new Canvas { Width = W, Height = H };
            cv.Children.Add(Imagens.NovaImagem(Path.Combine(dir, p.Arquivo), W, H));
            var sc = new ScaleTransform(1, 1, p.PivoX, p.PivoY);
            var ro = new RotateTransform(0, p.PivoX, p.PivoY);
            var tr = new TranslateTransform();
            var g = new TransformGroup();
            g.Children.Add(sc); g.Children.Add(ro); g.Children.Add(tr);
            cv.RenderTransform = g;
            nos.Add(new NoMembro { Papel = p.Papel, Canvas = cv, Escala = sc, Giro = ro, Mov = tr, P = p, Pai = p.Pai });
        }
        // membros do mesmo tipo se alternam (pé esquerdo/direito, asa de cá/de lá)
        foreach (var grupo in nos.GroupBy(n => n.Papel))
        {
            int k = 0;
            foreach (var n in grupo.OrderBy(n => n.P.PivoX)) n.Fase = k++ % 2;
        }
        var raiz = new Canvas { Width = W, Height = H };
        string[] atras = [Papeis.Pe, Papeis.Cauda];
        foreach (var n in nos) if (n.Pai < 0 && atras.Contains(n.Papel)) raiz.Children.Add(n.Canvas);
        raiz.Children.Add(Imagens.NovaImagem(Path.Combine(dir, rig.Base), W, H));
        foreach (var n in nos) if (n.Pai < 0 && !atras.Contains(n.Papel)) raiz.Children.Add(n.Canvas);
        foreach (var n in nos) if (n.Pai >= 0 && n.Pai < nos.Count) nos[n.Pai].Canvas.Children.Add(n.Canvas);

        var frases = rig.Frases?.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
        return new Personagem
        {
            Raiz = raiz, Nos = nos, Largura = W, Altura = H, Olhando = rig.Olhando, AlturaTela = rig.AlturaTela,
            Frases = frases is { Count: > 0 } ? frases : FrasesPadrao.Lista
        };
    }

    /// <summary>Anima cada membro conforme o tipo. Chamar a cada quadro (t = contador de quadros).</summary>
    public void AplicarPose(Pose pose, double t, bool piscando)
    {
        int lado = Olhando == Personagens.Olhando.Esquerda ? -1 : 1;
        bool frente = Olhando == Personagens.Olhando.Frente;
        bool olhar = pose == Pose.Olhar;        // 'olhar' = parado, mas com a cabeça inclinada para baixo
        if (olhar) pose = Pose.Parado;
        foreach (var n in Nos)
        {
            double f = n.Fase * Math.PI;
            switch (n.Papel)
            {
                case Papeis.Cabeca:
                    n.Giro.Angle = pose switch
                    {
                        Pose.Andar => Math.Sin(t * 0.3) * 4,
                        Pose.Parado => olhar ? (frente ? 8 : 14 * lado) + Math.Sin(t * 0.3) * 1.5 : Math.Sin(t * 0.04) * 3,
                        Pose.Guarda => Math.Sin(t * 0.05) * 6,   // olha em volta enquanto desce
                        _ => -5 * lado
                    };
                    break;
                case Papeis.Olhos:
                    n.Escala.ScaleY = piscando ? 0.1 : 1;
                    break;
                case Papeis.Braco:
                    int sg = n.Fase == 1 && frente ? -1 : 1;   // de frente, os braços se espelham
                    double a = pose switch
                    {
                        Pose.Andar => Math.Sin(t * 0.6 + f) * 12,
                        Pose.Parado => Math.Sin(t * 0.06 + f) * 3,
                        Pose.Guarda => 25 + Math.Sin(t * 0.1 + f) * 4,        // erguido, segurando o guarda-chuva
                        _ => Math.Abs(Math.Sin(t * 1.2)) * 35                 // bate no ar
                    };
                    n.Giro.Angle = a * lado * sg;
                    break;
                case Papeis.Pe:
                    if (pose == Pose.Andar)
                    {
                        double v = Math.Sin(t * 0.3 + f);
                        n.Mov.Y = -Math.Max(0, v) * (n.P.H * 0.35 + 1);
                        n.Giro.Angle = v * 6 * lado;
                    }
                    else if (pose == Pose.Guarda) { n.Mov.Y = 0; n.Giro.Angle = Math.Sin(t * 0.12 + f) * 12; }   // perninhas soltas balançando
                    else if (pose is Pose.Pular or Pose.Cair) { n.Mov.Y = 0; n.Giro.Angle = n.Fase == 0 ? -10 : 10; }
                    else { n.Mov.Y = 0; n.Giro.Angle = 0; }
                    break;
                case Papeis.Cauda:
                    n.Giro.Angle = lado * pose switch
                    {
                        Pose.Andar => Math.Sin(t * 0.6) * 12,
                        Pose.Parado => Math.Sin(t * 0.15) * 7,
                        Pose.Guarda => Math.Sin(t * 0.1) * 10,
                        _ => -15
                    };
                    break;
            }
        }
    }
}
