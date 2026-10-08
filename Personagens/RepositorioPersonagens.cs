using System.IO;
using Mascote.Nucleo;
using Mascote.Rig;

namespace Mascote.Personagens;

/// <summary>Os personagens gravados em Dados\personagens: qual está em uso, detecção dos membros e gravação.</summary>
public static class RepositorioPersonagens
{
    public static string Pasta(string nome) => Path.Combine(Caminhos.PastaPersonagens, nome);

    static bool Existe(string nome) => File.Exists(Path.Combine(Pasta(nome), "rig.json"));

    public static IEnumerable<string> Listar() =>
        Directory.Exists(Caminhos.PastaPersonagens)
            ? Directory.GetDirectories(Caminhos.PastaPersonagens).Select(d => Path.GetFileName(d)!).Where(Existe).OrderBy(n => n)
            : [];

    /// <summary>O personagem do config.json; se não houver (ou sumiu), o pato padrão; sem ele, o primeiro que existir.</summary>
    public static string? Atual()
    {
        var n = Configuracao.Ler().Personagem;
        if (!string.IsNullOrEmpty(n) && Existe(n)) return n;
        return Existe(PersonagemPadrao.Nome) ? PersonagemPadrao.Nome : Listar().FirstOrDefault();
    }

    public static void DefinirAtual(string nome) => Configuracao.Alterar(c => c.Personagem = nome);

    /// <summary>Detecção automática dos membros de uma imagem.</summary>
    public static (List<ParteEdicao> Partes, string Olhando) Sugerir(ImgData img)
    {
        var lista = RigTools.Detect(img, out string olhando);
        var partes = lista.Select(r => new ParteEdicao
        {
            Papel = r.Papel, Forma = r.Forma, X = r.X, Y = r.Y, W = r.W, H = r.H, PX = r.PX, PY = r.PY, Preencher = r.Preencher
        }).ToList();
        return (partes, olhando);
    }

    /// <summary>Ponto de giro padrão de um membro, conforme o tipo dele e para onde o personagem olha.</summary>
    public static void PivoPadrao(ParteEdicao p, string olhando)
    {
        double x = p.X, y = p.Y, w = p.W, h = p.H;
        switch (p.Papel)
        {
            case Papeis.Cabeca: p.PX = x + w / 2; p.PY = y + h * 0.9; break;
            case Papeis.Olhos: p.PX = x + w / 2; p.PY = y + h / 2; break;
            case Papeis.Pe: p.PX = x + w / 2; p.PY = y; break;
            case Papeis.Braco:
                if (olhando == Olhando.Direita) { p.PX = x + w * 0.8; p.PY = y + h * 0.3; }
                else if (olhando == Olhando.Esquerda) { p.PX = x + w * 0.2; p.PY = y + h * 0.3; }
                else { p.PX = x + w / 2; p.PY = y + h * 0.1; }
                break;
            default:
                if (olhando == Olhando.Direita) { p.PX = x + w; p.PY = y + h / 2; }
                else if (olhando == Olhando.Esquerda) { p.PX = x; p.PY = y + h / 2; }
                else { p.PX = x + w / 2; p.PY = y + h; }
                break;
        }
    }

    /// <summary>Recorta cada membro numa camada e grava a pasta do personagem (original, base, partes e rig.json).</summary>
    public static void Salvar(ImgData img, IReadOnlyList<ParteEdicao> partes, string dir, string nome, double alturaTela, string olhando,
                              IReadOnlyList<string>? frases)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "parte_*.png")) File.Delete(f);
        var rp = new RigPart[partes.Count];
        for (int i = 0; i < partes.Count; i++)
        {
            var p = partes[i]; int pai = -1;
            if (p.Papel == Papeis.Olhos)   // olhos acompanham a cabeça que os contém
            {
                double cx = p.X + p.W / 2, cy = p.Y + p.H / 2;
                for (int j = 0; j < partes.Count; j++)
                {
                    var c = partes[j];
                    if (c.Papel == Papeis.Cabeca && cx >= c.X && cx <= c.X + c.W && cy >= c.Y && cy <= c.Y + c.H) { pai = j; break; }
                }
            }
            rp[i] = new RigPart
            {
                Papel = p.Papel, Forma = p.Forma, X = p.X, Y = p.Y, W = p.W, H = p.H, PX = p.PX, PY = p.PY, Preencher = p.Preencher, Pai = pai,
                Pontos = p.Forma == Formas.Livre ? p.Pontos?.SelectMany(q => new[] { q.X, q.Y }).ToArray() : null
            };
        }
        RigTools.Save(img, Path.Combine(dir, "original.png"));
        RigTools.BuildRig(img, rp, dir);
        var json = new RigJson
        {
            Nome = nome, Largura = img.W, Altura = img.H, AlturaTela = alturaTela, Olhando = olhando,
            Partes = rp.Select((r, i) => new ParteJson
            {
                Papel = r.Papel, Forma = r.Forma, X = r.X, Y = r.Y, W = r.W, H = r.H, Pontos = r.Pontos, PivoX = r.PX, PivoY = r.PY,
                Preencher = r.Preencher, Pai = r.Pai, Arquivo = $"parte_{i}.png"
            }).ToList(),
            Frases = frases is { Count: > 0 } ? frases.ToList() : FrasesPadrao.Lista.ToList()
        };
        // rig.json por último: o mascote recarrega quando ele muda
        Json.Gravar(Path.Combine(dir, "rig.json"), json);
    }
}
