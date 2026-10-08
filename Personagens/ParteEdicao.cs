using System.Windows;

namespace Mascote.Personagens;

/// <summary>Formato da área de um membro (a "forma" no rig.json).</summary>
public static class Formas
{
    public const string Retangulo = "ret", Elipse = "elipse", Livre = "livre";
}

/// <summary>Um membro enquanto está sendo marcado no editor (área e ponto de giro, em pixels da imagem).</summary>
public sealed class ParteEdicao
{
    public string Papel = Papeis.Braco, Forma = Formas.Retangulo;
    public double X, Y, W, H, PX, PY;   // X,Y,W,H: a área (ou a caixa em volta do contorno livre)
    public List<Point>? Pontos;         // contorno, quando a forma é livre
    public bool Preencher;
    public bool PivoManual;   // o usuário posicionou o ponto de giro: não recalcular

    /// <summary>Passa a ser um contorno livre com estes pontos; a caixa X,Y,W,H é recalculada.</summary>
    public void DefinirContorno(List<Point> pontos)
    {
        Forma = Formas.Livre; Pontos = pontos;
        X = pontos.Min(p => p.X); Y = pontos.Min(p => p.Y);
        W = pontos.Max(p => p.X) - X; H = pontos.Max(p => p.Y) - Y;
    }

    /// <summary>Troca o formato mantendo a área: retângulo/elipse viram contorno, e o contorno vira a caixa em volta dele.</summary>
    public void MudarForma(string forma)
    {
        if (forma == Forma) return;
        if (forma == Formas.Livre)
        {
            var pts = new List<Point>();
            if (Forma == Formas.Elipse)
            {
                for (int i = 0; i < 24; i++)
                {
                    double a = i * Math.PI / 12;
                    pts.Add(new Point(X + W / 2 + Math.Cos(a) * W / 2, Y + H / 2 + Math.Sin(a) * H / 2));
                }
            }
            else pts.AddRange([new(X, Y), new(X + W, Y), new(X + W, Y + H), new(X, Y + H)]);
            Pontos = pts;
        }
        else Pontos = null;
        Forma = forma;
    }
}
