using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mascote.Canhao;
using Mascote.Interop;
using Mascote.Mochila;
using Mascote.Personagens;
using Mascote.Roleta;

namespace Mascote.Principal;

// Animação: um quadro a cada 33 ms. Primeiro o estado atual, depois o que acontece em qualquer estado.
public partial class JanelaMascote
{
    void Tick(object? sender, EventArgs e)
    {
        t++;
        var wa = AreaMascote();
        // a cesta só vale para o voo em que ela aconteceu (voando -> guarda-chuva -> pouso -> comemoração)
        if (acertou && estado is not (Estado.Voando or Estado.GuardaChuva or Estado.FechandoGuarda or Estado.Comemorando)) acertou = false;
        double chao = wa.Bottom - Height;

        switch (estado)
        {
            case Estado.Roleta: QuadroRoleta(); break;
            case Estado.Desmaiado: QuadroDesmaiado(); break;
            case Estado.Voando: QuadroVoando(wa, chao); break;
            case Estado.IndoAoRelogio: QuadroIndoAoRelogio(chao); break;
            case Estado.OlhandoRelogio: QuadroOlhandoRelogio(); break;
            case Estado.GuardaChuva: QuadroGuardaChuva(wa, chao); break;
            case Estado.FechandoGuarda: QuadroFechandoGuarda(); break;
            case Estado.Comemorando: QuadroComemorando(); break;
            case Estado.Caindo: QuadroCaindo(chao); break;
            case Estado.Pulando: QuadroPulando(); break;
            case Estado.Andando: QuadroAndando(wa); break;
            case Estado.Parado: QuadroParado(chao); break;
        }

        // Vira para o lado em que anda (conforme o lado para onde o desenho olha)
        Flip.ScaleX = pers.Olhando switch { Olhando.Esquerda => -dir, Olhando.Frente => 1, _ => dir };

        QuadroMochila();

        // Piscar
        if (piscarSegura > 0)
        {
            if (--piscarSegura == 0) piscar = rnd.Next(60, 220);
        }
        else if (--piscar <= 0) piscarSegura = 4;

        // Anima os membros
        var pose = estado switch
        {
            Estado.Andando or Estado.IndoAoRelogio => Pose.Andar,
            Estado.OlhandoRelogio => Pose.Olhar,
            Estado.Pulando or Estado.Voando or Estado.Comemorando => Pose.Pular,
            Estado.Caindo => Pose.Cair,
            Estado.GuardaChuva => Pose.Guarda,
            _ => Pose.Parado
        };
        pers.AplicarPose(pose, t, piscarSegura > 0 || estado == Estado.Desmaiado, AberturaDaBoca());   // desmaiado: olhos fechados
        // se a roleta foi interrompida por outra ação, some com o revólver
        if (arma != null && estado is not (Estado.Roleta or Estado.Desmaiado)) { arma.Remover(); arma = null; }

        // Balão de fala
        if (balao > 0 && --balao == 0) FecharBalao();
        if (--proximaFala <= 0) { FalarFrase(); proximaFala = rnd.Next(3600, 9000); }

        // Alertas recebidos (um de cada vez)
        if (t % 3 == 0 && !alerta && alertas.Recebidas.TryDequeue(out var msg)) MostrarAlerta(msg);

        QuadroCestaEConfete();
        QuadroClima();

        // Mantém acima de tudo (inclusive da barra de tarefas)
        if (t % 30 == 0 && hwnd != IntPtr.Zero && estado != Estado.NoCanhao)   // (mirando: o canhão fica por cima)
        {
            if (cesta != null) cesta.Ordenar(hwnd);   // mascote entre o fundo e a frente da cesta
            else JanelaNativa.KeepOnTop(hwnd);
        }

        // Recarrega se o personagem foi trocado ou salvo de novo no editor
        if (t % 60 == 0 && Carimbo() != carimbo) { try { Carregar(); } catch { } }
    }

    // ---------- Estados do dia a dia ----------

    void QuadroParado(double chao)
    {
        Hop.Y = 0;
        Flip.ScaleY += (1 + Math.Sin(t * 0.1) * 0.015 - Flip.ScaleY) * 0.2;   // respira
        if (Top < chao) estado = Estado.Caindo;
        else if (!pausado && --restante <= 0)
        {
            estado = Estado.Andando; restante = rnd.Next(100, 450);
            dir = rnd.Next(2) == 0 ? -1 : 1;
        }
    }

    void QuadroAndando(Rect wa)
    {
        double x = Left + dir * 1.3;
        if (x <= wa.Left) { x = wa.Left; dir = 1; }
        else if (x >= wa.Right - Width) { x = wa.Right - Width; dir = -1; }
        Left = x;
        Hop.Y = -Math.Abs(Math.Sin(t * 0.3)) * 3;
        Flip.ScaleY = 1 + Math.Sin(t * 0.6) * 0.03;                          // gingado
        if (--restante <= 0) { estado = Estado.Parado; restante = rnd.Next(60, 250); }
    }

    void QuadroPulando()
    {
        vy += 0.8;
        Hop.Y = Math.Min(0, Hop.Y + vy);
        Flip.ScaleY = 1.08;                                                          // estica no pulo
        if (Hop.Y >= 0) { estado = Estado.Parado; restante = 60; Flip.ScaleY = 0.9; }   // amassa ao cair
    }

    void QuadroCaindo(double chao)
    {
        vy += 0.9;
        Top = Math.Min(chao, Top + vy);
        if (Top >= chao) { estado = Estado.Parado; restante = 40; vy = 0; Flip.ScaleY = 0.9; }
    }

    // ---------- Guarda-chuva ----------

    // desce devagar balançando como folha: inclina para um lado e escorrega para esse lado
    void QuadroGuardaChuva(Rect wa, double chao)
    {
        ga++;
        g.Abre.ScaleX = Math.Min(1, ga / 8.0); g.Abre.ScaleY = Math.Min(1, 0.4 + ga / 12.0);
        double fase = ga * 0.07;
        Balanco.Angle = Math.Sin(fase) * 18;
        double x = Left - Math.Cos(fase) * 2.2;
        Left = Math.Max(wa.Left, Math.Min(wa.Right - Width, x));
        Top = Math.Min(chao, Top + 1.6);
        Hop.Y = 0;
        if (Top >= chao) { estado = Estado.FechandoGuarda; ga = 0; Flip.ScaleY = 0.9; sombra.Visibility = Visibility.Visible; }
    }

    // pousou: endireita e fecha o guarda-chuva
    void QuadroFechandoGuarda()
    {
        ga++;
        Balanco.Angle *= 0.7;
        g.Abre.ScaleX = Math.Max(0, 1 - ga / 8.0);
        if (ga >= 8)
        {
            g.Elemento.Visibility = Visibility.Collapsed; Balanco.Angle = 0; estado = Estado.Parado; restante = 50;
            if (acertou) { estado = Estado.Comemorando; ga = 0; }
        }
    }

    // ---------- Relógio ----------

    // anda (mais rápido que o normal) até ficar ao lado do relógio
    void QuadroIndoAoRelogio(double chao)
    {
        if (Top < chao) Top = Math.Min(chao, Top + 8);
        double dx = alvoX - Left;
        if (Math.Abs(dx) <= 2.5)
        {
            Left = alvoX; Hop.Y = 0;
            dir = relogioX >= Left + Width / 2 ? 1 : -1;   // vira para o relógio
            estado = Estado.OlhandoRelogio; ga = 0;
            Falar("Hmm, deixa eu ver...");
        }
        else
        {
            dir = Math.Sign(dx);
            Left += dir * Math.Min(2.5, Math.Abs(dx));
            Hop.Y = -Math.Abs(Math.Sin(t * 0.4)) * 3;
            Flip.ScaleY = 1 + Math.Sin(t * 0.8) * 0.03;
        }
    }

    // olha para baixo por ~1,5 s e fala a hora
    void QuadroOlhandoRelogio()
    {
        ga++; Hop.Y = 0;
        if (ga == 50) { Falar(FalaHora()); estado = Estado.Parado; restante = 150; }
    }

    // ---------- Canhão ----------

    // saiu do canhão: balística, rodopiando, quicando nas bordas da tela
    void QuadroVoando(Rect wa, double chao)
    {
        ga++;
        vy += JanelaCanhao.Gravidade;
        double x = Left + vx, y = Top + vy;
        double meia = Mascot.Width / 2, cx = x + Width / 2;
        if (cx < wa.Left + meia) { x = wa.Left + meia - Width / 2; vx = Math.Abs(vx) * 0.7; dir = 1; }
        else if (cx > wa.Right - meia) { x = wa.Right - meia - Width / 2; vx = -Math.Abs(vx) * 0.7; dir = -1; }
        double folgaTopo = Height - Mascot.Height - 3;
        if (y + folgaTopo < wa.Top) { y = wa.Top - folgaTopo; vy = Math.Abs(vy) * 0.5; }   // bate no teto
        Left = x; Top = Math.Min(chao, y);
        Balanco.Angle += vx * 1.6 + dir * 2;
        Hop.Y = 0;

        // cesta de basquete: rebate na tabela; conta cesta se o centro dele atravessar o aro descendo
        double mcx = Left + Width / 2, mcy = Top + Height - Mascot.Height / 2 - 3;
        var c = cesta;
        if (c != null && !c.Acertou)
        {
            if (mcy > c.TabTopo && mcy < c.TabBase)
            {
                if (c.Direita && vx > 0 && mcx + meia * 0.4 >= c.TabL && prevCx + meia * 0.4 < c.TabL)
                {
                    vx = -Math.Abs(vx) * 0.6; Left = c.TabL - meia * 0.4 - Width / 2; dir = -1;
                }
                else if (!c.Direita && vx < 0 && mcx - meia * 0.4 <= c.TabR && prevCx - meia * 0.4 > c.TabR)
                {
                    vx = Math.Abs(vx) * 0.6; Left = c.TabR + meia * 0.4 - Width / 2; dir = 1;
                }
            }
            if (vy > 0 && prevCy < c.AroY && mcy >= c.AroY && mcx > c.AroL - 6 && mcx < c.AroR + 6)
            {
                // CESTA! passa pela rede (freia) e a rede balança
                c.Acertou = true; c.Balanco = 30; acertou = true; placar++;
                semGuarda = 30;   // cai pela rede por ~1 s antes de poder abrir o guarda-chuva
                vx *= 0.2; vy = Math.Min(vy, 3);
                SystemSounds.Asterisk.Play();
                Falar("CESTAAA!!!");
            }
        }
        prevCx = mcx; prevCy = mcy;
        // acima do aro, com a cesta ainda valendo, não abre o guarda-chuva (senão nunca chega lá)
        bool miraCesta = c != null && !c.Acertou && mcy < c.AroY + 30;
        if (semGuarda > 0) semGuarda--;

        if (!miraCesta && semGuarda <= 0 && vy > 3 && (chao - Top) > 220)
        {
            // caindo de muito alto: para de girar e abre o guarda-chuva
            RestaurarGiro();
            estado = Estado.GuardaChuva; ga = 0; g.Elemento.Visibility = Visibility.Visible; g.Abre.ScaleX = 0; g.Abre.ScaleY = 0;
            if (!acertou) Falar("Ufa!", "Guarda-chuva, me salva!", "Ainda bem que eu trouxe isso!");
        }
        else if (Top >= chao)
        {
            if (vy > 9) { vy = -vy * 0.35; vx *= 0.6; Flip.ScaleY = 0.8; }   // quica no chão
            else
            {
                RestaurarGiro();
                Flip.ScaleY = 0.8;
                sombra.Visibility = Visibility.Visible;
                if (acertou) { estado = Estado.Comemorando; ga = 0; }
                else
                {
                    estado = Estado.Parado; restante = 70;
                    if (cesta != null) Falar("Errei... de novo!", "Quase!", "A cesta fugiu!", "Essa foi por pouco!");
                    else Falar("De novo! De novo!", "Que viagem!", "Tô tonto...", "Aterrissagem perfeita!");
                }
            }
        }
    }

    // fez cesta: pula sem parar, vira de um lado para o outro, e solta confete
    void QuadroComemorando()
    {
        ga++;
        if (ga == 1)
        {
            SoltarConfete();
            Falar($"{Sortear(["Três pontos!!!", "Cesta de primeira!", "Sou o rei da quadra!", "Swish!!!", "Chupa, Jordan!"])}  Placar: {placar}");
        }
        Hop.Y = -Math.Abs(Math.Sin(ga * 0.22)) * 22;
        Flip.ScaleY = 1 + Math.Sin(ga * 0.44) * 0.06;
        if (ga % 29 == 0) dir = -dir;
        if (ga >= 115) { Hop.Y = 0; Flip.ScaleY = 1; estado = Estado.Parado; restante = 60; acertou = false; }
    }

    void QuadroCestaEConfete()
    {
        // Cesta: some uns 3 s depois que ele pousa (e balança a rede quando acerta)
        if (cesta != null)
        {
            if (cesta.Fim < 0 && estado is not (Estado.NoCanhao or Estado.Voando or Estado.GuardaChuva or Estado.FechandoGuarda)) cesta.Fim = 90;
            if (!cesta.Atualizar()) cesta = null;
        }
        // Confetes caindo
        if (confetes != null)
        {
            foreach (var f in confetes)
            {
                f.VY += 0.35; f.VX *= 0.98; f.X += f.VX; f.Y += f.VY; f.Giro.Angle += f.VR;
                Canvas.SetLeft(f.R, f.X); Canvas.SetTop(f.R, f.Y);
            }
            if (!confetes.Any(f => f.Y < Height)) { CamadaConfete.Children.Clear(); confetes = null; }
        }
    }

    // ---------- Roleta russa ----------

    // o revólver aparece, o tambor gira e vai parando, suspense... clique ou BANG
    void QuadroRoleta()
    {
        ga++; Hop.Y = 0;
        var a = arma!;
        if (ga <= 8) { a.Aparece.ScaleX = ga / 8.0; a.Aparece.ScaleY = ga / 8.0; }
        if (ga == 2) Falar("Roleta russa... seja o que Deus quiser!");
        if (ga >= 10 && ga < 75) a.Tambor.Angle += 45 * (1 - (ga - 10) / 65.0);            // gira e desacelera
        if (ga == 75) { a.Tambor.Angle = Math.Round(a.Tambor.Angle / 60) * 60; Falar("..."); }   // encaixa numa câmara
        if (ga >= 75 && ga < 110)
        {
            // suspense: arma e mascote tremendo
            a.Treme.X = (rnd.NextDouble() - 0.5) * 3; a.Treme.Y = (rnd.NextDouble() - 0.5) * 3;
            Hop.Y = (rnd.NextDouble() - 0.5) * 2;
        }
        if (ga == 110)
        {
            a.Treme.X = 0; a.Treme.Y = 0; Hop.Y = 0;
            if (bala)
            {
                a.MostrarBandeira();
                SystemSounds.Hand.Play();
                Falar("BANG!");
                sobrevivencias = 0;
                estado = Estado.Desmaiado; ga = 0;
                return;
            }
            Falar("*clique*");
            sobrevivencias++;
        }
        if (ga == 135)
        {
            var ufa = Sortear(["Ufa! Hoje não!", "Sobrevivi!!!", "Ainda tô aqui!", "Meu coração...", "Essa foi por pouco!"]);
            Falar($"{ufa}  ({sobrevivencias} seguida{(sobrevivencias != 1 ? "s" : "")})");
            Flip.ScaleY = 0.9;
        }
        if (ga >= 150) { a.Aparece.ScaleX = Math.Max(0, 1 - (ga - 150) / 8.0); a.Aparece.ScaleY = a.Aparece.ScaleX; }
        if (ga >= 158) { a.Remover(); arma = null; estado = Estado.Parado; restante = 60; }
    }

    // BANG: a bandeirinha sai do cano, ele cai de lado, a tela bloqueia; quando desbloquear, ele levanta
    void QuadroDesmaiado()
    {
        ga++; Hop.Y = 0;
        if (arma != null && ga <= 6) arma.EscalaBandeira.ScaleX = ga / 6.0;
        // cai de lado, para a esquerda, girando no canto do pé (assim fica deitado em cima do chão).
        // A janela cresce para a esquerda e o conteúdo é empurrado na mesma medida: nada sai do lugar na tela,
        // mas sobra espaço para ele deitar sem ser cortado.
        if (ga == 1)   // já no BANG, para a bandeirinha também caber
        {
            Balanco.CenterX = -Mascot.Width / 2; Balanco.CenterY = 0;
            deslize = Math.Max(0, Mascot.Height - Canvas.GetLeft(Mascot) + 4);
            Left -= deslize; Width += deslize; Palco.Width += deslize;
            Palco.RenderTransform = new TranslateTransform(deslize, 0);
        }
        if (ga >= 8 && ga <= 20) Balanco.Angle = -82 * (ga - 8) / 12.0;
        if (ga == 24) Falar("x_x");
        if (ga == 45) { Bloqueio.Bloquear(); bloqueouEm = DateTime.Now; }
        if (ga > 45 && ga % 15 == 0 && levantando == 0)
        {
            // espera a tela ser desbloqueada (dá uns segundos para a tela de bloqueio aparecer)
            if ((DateTime.Now - bloqueouEm).TotalSeconds > 3 && !Bloqueio.TelaBloqueada()) levantando = 1;
        }
        if (levantando != 0)
        {
            levantando++;
            Balanco.Angle = -82 * Math.Max(0, 1 - levantando / 12.0);
            if (levantando == 3) { arma?.Remover(); arma = null; }
            if (levantando >= 14)
            {
                // janela volta ao tamanho normal (sem mexer nada na tela)
                Palco.RenderTransform = null; Palco.Width -= deslize; Width -= deslize; Left += deslize; deslize = 0;
                levantando = 0; RestaurarGiro(); Flip.ScaleY = 0.9;
                estado = Estado.Parado; restante = 90;
                Falar("Ai... o que aconteceu?", "Eu vi uma luz...", "Ninguém viu isso, tá?", "Acho que preciso de um café...");
            }
        }
    }

    // ---------- Mochila e clima ----------

    // Guarda o que foi solto em cima dele, anima a tampa e confere o conteúdo a cada 2 s
    void QuadroMochila()
    {
        if (paraGuardar != null)
        {
            var itens = paraGuardar; paraGuardar = null;
            var nomes = ServicoMochila.Adicionar(itens);
            mochila.Anim = 30; AtualizarMochila();
            Falar(nomes.Count == 1 ? $"Guardei '{nomes[0]}' na mochila!" : nomes.Count > 0 ? $"Guardei {nomes.Count} coisas na mochila!" : "Ops, não consegui guardar...");
        }
        if (mochila.Aberta && estado == Estado.Andando) { estado = Estado.Parado; restante = 60; }   // espera parado enquanto arrastam o arquivo
        if (t % 60 == 0) AtualizarMochila();
        mochila.Atualizar(Flip.ScaleX);
    }

    // Consulta em segundo plano; comenta quando começa/para de chover
    void QuadroClima()
    {
        if (t % 30 == 0 && clima.Atualizar())
        {
            if (falarClima) { Falar(clima.Fala()); falarClima = false; }
            else if (clima.Ok && chovia != null && clima.Chovendo != chovia) Falar(clima.Chovendo ? "Xi, começou a chover!" : "Oba, parou de chover!");
            if (clima.Ok) chovia = clima.Chovendo;
        }
        // nuvem em cima da cabeça enquanto chove (some quando está no ar com o guarda-chuva ou no canhão)
        bool nuvemVisivel = clima.Chovendo && estado is not (Estado.GuardaChuva or Estado.FechandoGuarda or Estado.Voando or Estado.NoCanhao);
        if (nuvemVisivel != (nuvem.Visibility == Visibility.Visible))
        {
            nuvem.Visibility = nuvemVisivel ? Visibility.Visible : Visibility.Collapsed;
            // o balão de fala sobe para não ficar atrás da nuvem
            Canvas.SetBottom(Balao, Mascot.Height + 4 + (nuvemVisivel ? nuvem.AlturaOcupada : 0));
        }
        if (nuvemVisivel) nuvem.Atualizar(t, clima.Trovoada, rnd);
    }
}
