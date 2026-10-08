using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mascote.Nucleo;
using Mascote.Rig;

namespace Mascote.GuardaChuvas;

/// <summary>O guarda.json de um guarda-chuva.</summary>
public sealed class GuardaJson
{
    [JsonPropertyName("nome")] public string Nome { get; set; } = "";
    [JsonPropertyName("largura")] public double Largura { get; set; }
    [JsonPropertyName("altura")] public double Altura { get; set; }
    [JsonPropertyName("pegaX")] public double PegaX { get; set; }
    [JsonPropertyName("pegaY")] public double PegaY { get; set; }
    [JsonPropertyName("topoX")] public double TopoX { get; set; }
    [JsonPropertyName("topoY")] public double TopoY { get; set; }
    [JsonPropertyName("tamanho")] public double Tamanho { get; set; }
}

/// <summary>Onde o guarda-chuva ficou no personagem: ponto de balanço e topo, em coordenadas do personagem.</summary>
public readonly record struct EncaixeGuarda(double PivoX, double PivoY, double Topo, double Largura);

/// <summary>
/// Guarda-chuva montado em WPF.
/// PegaX/PegaY = onde o personagem segura | TopoX/TopoY = ponto de onde ele balança | Tamanho = largura em relação ao personagem
/// </summary>
public sealed class GuardaChuva
{
    public required Canvas Elemento { get; init; }
    public double W { get; init; }
    public double PegaX { get; init; }
    public double PegaY { get; init; }
    public double TopoX { get; init; }
    public double TopoY { get; init; }
    public double Tamanho { get; init; }
    public ScaleTransform Abre { get; private set; } = null!;   // abre/fecha pelo meio da copa
    public ScaleTransform Tam { get; private set; } = null!;    // tamanho proporcional ao personagem

    public static IEnumerable<string> Listar() =>
        Directory.Exists(Caminhos.PastaGuardaChuvas)
            ? Directory.GetDirectories(Caminhos.PastaGuardaChuvas).Where(d => File.Exists(Path.Combine(d, "guarda.json")))
                       .Select(d => Path.GetFileName(d)!).OrderBy(n => n)
            : [];

    /// <summary>Pasta do guarda-chuva escolhido no config.json ("" = o padrão desenhado).</summary>
    public static string PastaAtual()
    {
        var n = Configuracao.Ler().GuardaChuva;
        if (!string.IsNullOrEmpty(n))
        {
            var p = Path.Combine(Caminhos.PastaGuardaChuvas, n);
            if (File.Exists(Path.Combine(p, "guarda.json"))) return p;
        }
        return "";
    }

    /// <summary>Monta o guarda-chuva de uma pasta (imagem.png + guarda.json) ou o padrão.</summary>
    public static GuardaChuva Carregar(string pasta = "")
    {
        GuardaChuva? d = null;
        if (pasta != "" && File.Exists(Path.Combine(pasta, "guarda.json")))
        {
            try
            {
                var j = Json.Ler<GuardaJson>(Path.Combine(pasta, "guarda.json"))!;
                var cv = new Canvas { Width = j.Largura, Height = j.Altura };
                cv.Children.Add(Imagens.NovaImagem(Path.Combine(pasta, "imagem.png"), j.Largura, j.Altura));
                d = new GuardaChuva
                {
                    Elemento = cv, W = j.Largura, PegaX = j.PegaX, PegaY = j.PegaY, TopoX = j.TopoX, TopoY = j.TopoY, Tamanho = j.Tamanho
                };
            }
            catch { d = null; }
        }
        d ??= new GuardaChuva { Elemento = new GuardaChuvaPadrao(), W = 100, PegaX = 50, PegaY = 88, TopoX = 50, TopoY = 5, Tamanho = 1.15 };

        d.Elemento.IsHitTestVisible = false;
        d.Elemento.Visibility = Visibility.Collapsed;
        d.Abre = new ScaleTransform(0, 0, d.TopoX, d.TopoY + (d.PegaY - d.TopoY) * 0.35);
        d.Tam = new ScaleTransform(1, 1);
        var g = new TransformGroup();
        g.Children.Add(d.Abre); g.Children.Add(d.Tam);
        d.Elemento.RenderTransform = g;
        return d;
    }

    /// <summary>Encaixa o guarda-chuva num personagem de mw x mh (cabo encostando no topo da cabeça).</summary>
    public EncaixeGuarda EncaixarNoPersonagem(double mw, double mh)
    {
        double larg = Math.Min(220, Math.Max(48, mw * Tamanho));
        double u = larg / W;
        Tam.ScaleX = u; Tam.ScaleY = u;
        double left = mw / 2 - PegaX * u, top = mh * 0.08 - PegaY * u;
        Canvas.SetLeft(Elemento, left); Canvas.SetTop(Elemento, top);
        return new EncaixeGuarda(left + TopoX * u, top + TopoY * u, top, larg);
    }

    public static void Salvar(ImgData img, string dir, GuardaJson dados)
    {
        Directory.CreateDirectory(dir);
        RigTools.Save(img, Path.Combine(dir, "imagem.png"));
        dados.Largura = img.W; dados.Altura = img.H;
        Json.Gravar(Path.Combine(dir, "guarda.json"), dados);
    }
}
