using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using Mascote.Nucleo;

namespace Mascote.Alertas;

/// <summary>
/// Alertas entre mascotes, através de uma pasta compartilhada na rede:
///   &lt;pasta&gt;\online\     um arquivo por pessoa com o mascote aberto (atualizado a cada 20 s)
///   &lt;pasta&gt;\mensagens\  um arquivo por alerta enviado (apagado depois de 1 dia)
/// O acesso à rede roda numa thread separada, para o mascote não travar se a rede estiver lenta.
/// </summary>
public sealed class ServicoAlertas
{
    public string Id { get; } = $"{Environment.UserName}@{Environment.MachineName}";

    volatile string pasta = "";
    volatile string nome = Environment.UserName;
    volatile string erro = "";
    volatile IReadOnlyList<Presenca> online = [];
    volatile bool parar;
    readonly ConcurrentQueue<MensagemAlerta> enviar = new();
    readonly AutoResetEvent sinal = new(false);   // acorda a thread (mensagem para enviar ou arquivo novo)

    public string Pasta => pasta;
    public string Nome => nome;
    public string Erro => erro;
    public IReadOnlyList<Presenca> Online => online;
    public ConcurrentQueue<MensagemAlerta> Recebidas { get; } = new();

    string IdSeguro => Regex.Replace(Id, @"[^\w\-@.]", "_");

    public void Iniciar()
    {
        var cfg = Configuracao.Ler();
        if (!string.IsNullOrEmpty(cfg.PastaAlertas)) pasta = cfg.PastaAlertas;
        if (!string.IsNullOrEmpty(cfg.NomeAlertas)) nome = cfg.NomeAlertas;
        new Thread(Laco) { IsBackground = true, Name = "Alertas" }.Start();
    }

    public void Parar()
    {
        parar = true; sinal.Set();
        if (pasta != "")   // sai da lista de online na hora
        {
            try { File.Delete(Path.Combine(pasta, "online", $"{IdSeguro}.json")); } catch { }
        }
    }

    /// <summary>Grava a pasta e o nome no config.json e passa a usá-los.</summary>
    public void Configurar(string novaPasta, string novoNome)
    {
        Configuracao.Alterar(c => { c.PastaAlertas = novaPasta; c.NomeAlertas = novoNome; });
        nome = novoNome; pasta = novaPasta; erro = "";
        sinal.Set();
    }

    /// <summary>Coloca um alerta na fila e acorda a thread para gravar na hora.</summary>
    public void Enviar(string texto, List<string> para)
    {
        enviar.Enqueue(new MensagemAlerta { De = Id, Nome = nome, Texto = texto, Para = para, Quando = DateTime.Now.ToString("HH:mm") });
        sinal.Set();
    }

    void Laco()
    {
        var vistos = new HashSet<string>();
        string? pastaAtual = null;
        DateTime proxOnline = DateTime.MinValue, proxLimpeza = DateTime.MinValue;
        var seguro = IdSeguro;
        var vigia = new VigiaPasta();
        while (!parar)
        {
            try
            {
                var p = pasta;
                if (p == "") { sinal.WaitOne(1000); continue; }
                string dirMsg = Path.Combine(p, "mensagens"), dirOn = Path.Combine(p, "online");
                if (p != pastaAtual)
                {
                    Directory.CreateDirectory(dirMsg); Directory.CreateDirectory(dirOn);
                    // o que já estava na pasta antes de abrir o mascote não é mostrado
                    vistos.Clear();
                    foreach (var f in Directory.GetFiles(dirMsg, "*.json")) vistos.Add(Path.GetFileName(f));
                    pastaAtual = p; proxOnline = DateTime.MinValue;
                    vigia.Vigiar(dirMsg, sinal);
                }
                else if (vigia.Falhou) vigia.Vigiar(dirMsg, sinal);   // tenta de novo (ex.: a rede voltou)

                // envia (grava com outro nome e renomeia, para ninguém ler o arquivo pela metade)
                while (enviar.TryDequeue(out var m))
                {
                    var arq = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{seguro}.json";
                    var tmp = Path.Combine(dirMsg, arq + ".tmp");
                    Json.Gravar(tmp, m, compacto: true);
                    File.Move(tmp, Path.Combine(dirMsg, arq), true);
                    vistos.Add(arq);
                }

                // recebe
                foreach (var f in Directory.GetFiles(dirMsg, "*.json"))
                {
                    if (!vistos.Add(Path.GetFileName(f))) continue;
                    MensagemAlerta? msg;
                    try { msg = Json.Ler<MensagemAlerta>(f); } catch { continue; }
                    if (msg == null || msg.De == Id) continue;
                    if (msg.Para is { Count: > 0 } && !msg.Para.Contains(Id)) continue;   // lista vazia = todos
                    Recebidas.Enqueue(msg);
                }

                // presença: quem atualizou o arquivo nos últimos 75 s (pelo relógio do servidor) está online
                if (DateTime.Now >= proxOnline)
                {
                    var meu = Path.Combine(dirOn, $"{seguro}.json");
                    Json.Gravar(meu, new Presenca(Id, nome), compacto: true);
                    var agora = File.GetLastWriteTimeUtc(meu);
                    var lista = new List<Presenca>();
                    foreach (var f in Directory.GetFiles(dirOn, "*.json"))
                    {
                        if (Math.Abs((agora - File.GetLastWriteTimeUtc(f)).TotalSeconds) >= 75) continue;
                        try
                        {
                            var o = Json.Ler<Presenca>(f);
                            if (o != null && o.Id != Id) lista.Add(o);
                        }
                        catch { }
                    }
                    online = lista.OrderBy(o => o.Nome).ToList();
                    proxOnline = DateTime.Now.AddSeconds(20);
                }

                // faxina de hora em hora
                if (DateTime.Now >= proxLimpeza)
                {
                    var agora = DateTime.UtcNow;
                    Limpar(dirMsg, agora, 1); Limpar(dirOn, agora, 7);
                    proxLimpeza = DateTime.Now.AddHours(1);
                }
                erro = "";
            }
            catch (Exception ex)
            {
                erro = ex.Message; pastaAtual = null;
            }
            // dorme até chegar arquivo novo / ter algo para enviar; a cada 2 s confere a pasta mesmo assim
            // (reserva para servidores que não avisam mudanças)
            sinal.WaitOne(2000);
        }
        vigia.Parar();
    }

    static void Limpar(string dir, DateTime agora, double dias)
    {
        foreach (var f in Directory.GetFiles(dir))
        {
            try { if ((agora - File.GetLastWriteTimeUtc(f)).TotalDays > dias) File.Delete(f); } catch { }
        }
    }
}
