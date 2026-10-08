namespace Mascote.Nucleo;

/// <summary>
/// Carga inicial de frases de um personagem novo. Cada personagem guarda as suas no rig.json ("frases");
/// os que ainda não têm frases próprias usam estas.
/// </summary>
public static class FrasesPadrao
{
    public static readonly IReadOnlyList<string> Lista =
    [
        "Quack!",
        "Qual foi?",
        "Oi! Tô de olho em você :)",
        "Já bebeu água hoje?",
        "Hora de um cafezinho?",
        "Salvou o arquivo?",
        "Vai pela a sombra!",
        "Que postura é essa, pae?!",
        "Vai falhar igual ontem ou vai fazer dar certo?",
        "Será que já mijaram na tampa da privada...",
        "Tô só passeando por aqui."
    ];
}
