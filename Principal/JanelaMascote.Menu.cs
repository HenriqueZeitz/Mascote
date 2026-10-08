using System.Windows;
using System.Windows.Controls;
using Mascote.Alertas;
using Mascote.Canhao;
using Mascote.Clima;
using Mascote.Editores;
using Mascote.GuardaChuvas;
using Mascote.Interop;
using Mascote.Nucleo;
using Mascote.Personagens;
using Mascote.Roleta;

namespace Mascote.Principal;

// Menu do botão direito
public partial class JanelaMascote
{
    JanelaEditorPersonagem? editorPersonagem;
    JanelaEditorGuardaChuva? editorGuarda;

    void Menu_Aberto(object sender, RoutedEventArgs e)
    {
        MiMochila.Header = qtdMochila > 0 ? $"Mochila ({qtdMochila})..." : "Mochila...";

        MiPersonagens.Items.Clear();
        var atual = RepositorioPersonagens.Atual();
        foreach (var n in RepositorioPersonagens.Listar())
        {
            var it = new MenuItem { Header = n, IsChecked = n == atual };
            it.Click += (_, _) => { RepositorioPersonagens.DefinirAtual(n); Carregar(); };
            MiPersonagens.Items.Add(it);
        }

        MiGuardas.Items.Clear();
        var atualG = Configuracao.Ler().GuardaChuva ?? "";
        bool usandoPadrao = GuardaChuva.PastaAtual() == "";
        foreach (var n in new[] { "" }.Concat(GuardaChuva.Listar()))
        {
            var it = new MenuItem { Header = n != "" ? n : "Padrão (vermelho)", IsChecked = n == "" ? usandoPadrao : n == atualG };
            it.Click += (_, _) => { Configuracao.Alterar(c => c.GuardaChuva = n); Carregar(); };
            MiGuardas.Items.Add(it);
        }
    }

    void Falar_Click(object sender, RoutedEventArgs e) => FalarFrase();

    void Parar_Click(object sender, RoutedEventArgs e)
    {
        pausado = MiParar.IsChecked;
        if (pausado) estado = Estado.Parado;
    }

    void Fechar_Click(object sender, RoutedEventArgs e) => Close();

    void Mochila_Click(object sender, RoutedEventArgs e) => AbrirMochila();

    // ---------- Alertas ----------

    void EnviarAlerta_Click(object sender, RoutedEventArgs e)
    {
        if (alertas.Pasta == "" && new JanelaConfigAlertas(alertas).ShowDialog() != true) return;
        if (alertas.Pasta == "") return;
        var j = new JanelaEnviarAlerta(alertas);
        j.ShowDialog();
        if (j.QtdEnviada != 0)
        {
            Falar(j.QtdEnviada < 0 ? "Alerta enviado para todos!" : $"Alerta enviado para {j.QtdEnviada} pessoa(s)!");
            estado = Estado.Pulando; vy = -7;
        }
    }

    void ConfigAlertas_Click(object sender, RoutedEventArgs e) => new JanelaConfigAlertas(alertas).ShowDialog();

    // ---------- Relógio e clima ----------

    void Hora_Click(object sender, RoutedEventArgs e)
    {
        if (estado == Estado.NoCanhao) return;
        // se estava no ar com o guarda-chuva, guarda e desce direto
        GuardarGuardaChuva(); sombra.Visibility = Visibility.Visible;
        var wa = AreaMascote();
        relogioX = BarraTarefas.PosicaoRelogio(this, wa) ?? wa.Right - 60;
        alvoX = Math.Max(wa.Left, Math.Min(wa.Right - Width, relogioX - Width / 2));
        estado = Estado.IndoAoRelogio;
    }

    static string FalaHora()
    {
        var agora = DateTime.Now; int min = agora.Hour * 60 + agora.Minute;
        var extra = min < 360 ? "Ainda acordado?!" : min < 690 ? "Bom dia!" : min < 810 ? "Hora do almoço!"
                  : min < 1020 ? "Boa tarde!" : min < 1080 ? "Tá quase na hora de ir embora!" : "Boa noite! Bora pra casa?";
        return $"São {agora:HH:mm}! {extra}";
    }

    void Tempo_Click(object sender, RoutedEventArgs e)
    {
        if (!clima.Configurado && new JanelaConfigClima(clima).ShowDialog() != true) return;
        if (clima.Ok) Falar(clima.Fala());
        else { Falar("Deixa eu olhar lá fora..."); falarClima = true; }
    }

    void ConfigClima_Click(object sender, RoutedEventArgs e)
    {
        if (new JanelaConfigClima(clima).ShowDialog() == true) { Falar("Deixa eu olhar lá fora..."); falarClima = true; }
    }

    // ---------- Canhão e roleta ----------

    void Canhao_Click(object sender, RoutedEventArgs e)
    {
        if (estado is Estado.NoCanhao or Estado.Voando) return;
        GuardarGuardaChuva(); FecharBalao();
        estado = Estado.NoCanhao; Mascot.Visibility = Visibility.Hidden;      // entra no canhão
        cesta?.Fechar();
        var area = AreaMascote();   // canhão e cesta no monitor onde ele está
        double x = Left + Width / 2;
        cesta = new Cesta(x, Math.Min(170, Math.Max(80, Mascot.Width * 1.4)), area);
        cesta.Ordenar(hwnd);   // fundo da cesta < mascote < frente da cesta (o canhão abre por cima de tudo)
        new JanelaCanhao(x, AoAtirar, AoCancelarCanhao, area).Mostrar();
    }

    void Roleta_Click(object sender, RoutedEventArgs e)
    {
        if (estado is Estado.NoCanhao or Estado.Voando or Estado.Roleta or Estado.Desmaiado) return;
        if (!roletaConfirmada)   // pergunta uma vez por sessão
        {
            var r = MessageBox.Show("1 bala em 6.\n\nSe cair na bala, o mascote desmaia e a tela do Windows é BLOQUEADA (igual Win+L).\n" +
                                    "Nada é fechado nem perdido: é só desbloquear com a sua senha.\n\nJogar?",
                                    "Roleta russa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            roletaConfirmada = true;
        }
        GuardarGuardaChuva(); FecharBalao();
        Top = AreaMascote().Bottom - Height; Hop.Y = 0;
        arma?.Remover();
        arma = new Arma(Palco, Mascot);
        bala = Bloqueio.Forcar != null ? Bloqueio.Forcar == "bala" : rnd.Next(6) == 0;   // 1 em 6
        dir = 1;                                  // olha para o revólver (que fica à direita)
        estado = Estado.Roleta; ga = 0;
    }

    // ---------- Editores ----------

    // Um editor de cada tipo por vez: se já estiver aberto, só traz para a frente.
    // Quando o editor salva, o mascote percebe sozinho (ver Carimbo) e recarrega.

    void EditorPersonagens_Click(object sender, RoutedEventArgs e)
    {
        if (editorPersonagem != null) { editorPersonagem.Activate(); return; }
        editorPersonagem = new JanelaEditorPersonagem();
        editorPersonagem.Closed += (_, _) => editorPersonagem = null;
        editorPersonagem.Show();
    }

    void EditorGuarda_Click(object sender, RoutedEventArgs e)
    {
        if (editorGuarda != null) { editorGuarda.Activate(); return; }
        editorGuarda = new JanelaEditorGuardaChuva();
        editorGuarda.Closed += (_, _) => editorGuarda = null;
        editorGuarda.Show();
    }
}
