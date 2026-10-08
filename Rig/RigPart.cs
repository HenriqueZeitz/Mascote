namespace Mascote.Rig;

/// <summary>Um membro do personagem, como o recorte precisa dele.</summary>
public class RigPart
{
    public string Papel = "braco", Forma = "ret";
    public double X, Y, W, H, PX, PY;   // área do membro e ponto de giro (pixels da imagem)
    public double[]? Pontos;            // Forma "livre": contorno x0,y0,x1,y1... (X,Y,W,H é a caixa em volta dele)
    public bool Preencher;             // preenche o buraco deixado na camada de baixo
    public int Pai = -1;                // índice do membro pai (ex.: olhos dentro da cabeça)
}
