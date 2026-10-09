namespace Mascote.Nucleo;

/// <summary>
/// Quanto tempo o mascote fica mexendo a boca para uma fala, e quanto a boca está aberta em cada instante.
/// Regra: uma palavra de 8 letras leva, em média, 1 segundo — ou seja, 1/8 de segundo por letra (ou número).
/// </summary>
public static class TempoDeFala
{
    public const double LetrasPorSegundo = 8;
    const double Minimo = 0.4;   // "Ai!", "x_x": ainda dá uma mexidinha

    const string Vogais = "aeiouáéíóúâêôãõàüy";

    public static int Letras(string texto) => texto.Count(char.IsLetterOrDigit);

    /// <summary>Segundos falando: letras / 8 (mínimo de 0,4 s).</summary>
    public static double Segundos(string texto) => Math.Max(Minimo, Letras(texto) / LetrasPorSegundo);

    /// <summary>
    /// Quanto a boca deve estar aberta (0 = fechada, 1 = toda aberta) aos <paramref name="s"/> segundos da fala.
    /// O tempo total é repartido pelo texto: letras andam no ritmo da regra acima, e espaços e pontuação viram
    /// pausas curtas com a boca fechada (vírgula e ponto um pouco mais longas). Vogais abrem mais que consoantes.
    /// </summary>
    public static double Abertura(string texto, double s)
    {
        double total = Segundos(texto);
        if (s < 0 || s >= total || texto.Length == 0) return 0;
        double peso = 0;
        foreach (var c in texto) peso += Peso(c);
        if (peso <= 0) return 0;
        // sem nenhuma letra (só pontuação, ex.: "..."): abre e fecha devagar durante o tempo mínimo
        if (Letras(texto) == 0) return Math.Abs(Math.Sin(s / total * Math.PI * 2)) * 0.5;

        double alvo = s / total * peso, acumulado = 0;
        foreach (var c in texto)
        {
            double p = Peso(c);
            if (acumulado + p > alvo)
            {
                if (!char.IsLetterOrDigit(c)) return 0;               // pausa: boca fechada
                double dentro = (alvo - acumulado) / p;               // 0..1 dentro desta letra
                double forma = Math.Sin(dentro * Math.PI);            // abre e fecha a cada letra
                return (Vogais.Contains(char.ToLowerInvariant(c)) ? 1.0 : 0.45) * (0.35 + 0.65 * forma);
            }
            acumulado += p;
        }
        return 0;
    }

    // Peso de cada caractere no tempo da fala: letra = 1; espaço = meia letra; pontuação = pausa maior
    static double Peso(char c) =>
        char.IsLetterOrDigit(c) ? 1 :
        char.IsWhiteSpace(c) ? 0.5 :
        c is '.' or ',' or '!' or '?' or ';' or ':' or '…' ? 1.5 : 0.3;
}
