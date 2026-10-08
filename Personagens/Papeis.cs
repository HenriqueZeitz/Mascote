namespace Mascote.Personagens;

/// <summary>Tipos de membro (o "papel" no rig.json) e para onde o desenho olha.</summary>
public static class Papeis
{
    public const string Cabeca = "cabeca", Olhos = "olhos", Braco = "braco", Pe = "pe", Cauda = "cauda";

    public static readonly IReadOnlyList<KeyValuePair<string, string>> Nomes =
    [
        new(Cabeca, "Cabeça"), new(Olhos, "Olhos (piscam)"), new(Braco, "Braço / asa"), new(Pe, "Pé / perna"), new(Cauda, "Cauda / orelha")
    ];

    public static string Nome(string papel) => Nomes.FirstOrDefault(p => p.Key == papel).Value ?? papel;
}

public static class Olhando
{
    public const string Direita = "direita", Esquerda = "esquerda", Frente = "frente";
}

/// <summary>Pose usada para animar os membros.</summary>
public enum Pose { Parado, Andar, Olhar, Pular, Cair, Guarda }
