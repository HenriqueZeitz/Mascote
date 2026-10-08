using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mascote.Personagens;

namespace Mascote.Mochila;

public partial class MochilaDesenho : Canvas
{
    public MochilaDesenho() => InitializeComponent();
}

/// <summary>A mochila nas costas do personagem: tampa que abre, contador de itens e o "pulinho" depois de guardar.</summary>
public sealed class MochilaVisual : Canvas
{
    readonly MochilaDesenho desenho = new();
    readonly ScaleTransform pulo;
    readonly double tamContador;   // número sempre legível

    /// <summary>Tampa aberta (enquanto um arquivo é arrastado por cima).</summary>
    public bool Aberta;
    /// <summary>Quadros de "pulinho" que ainda faltam.</summary>
    public int Anim;

    /// <summary>Monta a mochila nas costas de um personagem mw x mh que olha para <paramref name="olhando"/>.</summary>
    public MochilaVisual(double mw, double mh, string olhando)
    {
        double u = Math.Min(60, Math.Max(24, mw * 0.42)) / 60;
        double bw = 60 * u;
        double x = olhando switch   // nas costas
        {
            Olhando.Esquerda => mw * 0.88 - bw * 0.45,
            Olhando.Frente => mw * 0.82 - bw * 0.5,
            _ => mw * 0.12 - bw * 0.55
        };
        desenho.RenderTransform = new ScaleTransform(u, u);
        Children.Add(desenho);
        pulo = new ScaleTransform(1, 1, 30 * u, 62 * u);   // cresce/pula a partir de baixo
        RenderTransform = pulo;
        SetLeft(this, x); SetTop(this, mh * 0.3);
        Visibility = Visibility.Collapsed;
        tamContador = Math.Max(1, 0.85 / u);
    }

    /// <summary>Contador e visibilidade: aparece quando tem algo dentro, ou enquanto um arquivo é arrastado por cima.</summary>
    public void MostrarQuantidade(int qtd)
    {
        desenho.Qtd.Text = qtd.ToString();
        desenho.Contador.Visibility = qtd > 0 ? Visibility.Visible : Visibility.Hidden;   // sem "0" com a mochila vazia
        Visibility = qtd > 0 || Aberta ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Anima a tampa e o "pulinho" depois de guardar. Chamar a cada quadro.</summary>
    public void Atualizar(double flipX)
    {
        double alvo = Aberta ? -55 : 0;
        desenho.GiroTampa.Angle += (alvo - desenho.GiroTampa.Angle) * 0.35;
        if (Anim > 0)
        {
            Anim--;
            double f = 1 + Math.Sin(Anim * 0.6) * Anim / 40;
            pulo.ScaleX = f; pulo.ScaleY = f;
        }
        else if (Aberta) { pulo.ScaleX = 1.2; pulo.ScaleY = 1.2; }
        else { pulo.ScaleX = 1; pulo.ScaleY = 1; }
        desenho.EscalaContador.ScaleX = flipX * tamContador;   // desfaz o espelhamento do mascote para o número não ficar ao contrário
        desenho.EscalaContador.ScaleY = tamContador;
    }
}
