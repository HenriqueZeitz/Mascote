namespace Mascote.Rig;

/// <summary>Região de cor do desenho (delimitada pelos contornos escuros).</summary>
public class Comp
{
    public int Area, X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1;
    public double Cx, Cy, R, G, B;
    public double Lum { get { return 0.299 * R + 0.587 * G + 0.114 * B; } }
    public double Sat { get { double mx = Math.Max(R, Math.Max(G, B)), mn = Math.Min(R, Math.Min(G, B)); return mx <= 0 ? 0 : (mx - mn) / mx; } }
    public double Wd { get { return X1 - X0 + 1; } }
    public double Ht { get { return Y1 - Y0 + 1; } }
}
