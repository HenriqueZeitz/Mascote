using System.IO;
using System.Windows;
using System.Windows.Threading;
using Mascote.Nucleo;
using Mascote.Personagens;
using Mascote.Principal;
using Mascote.Roleta;

namespace Mascote;

/// <summary>Mascote de mesa - fica andando logo acima da barra de tarefas.</summary>
public partial class App : Application
{
    Mutex? mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += AoDarErro;

        // opções de teste da roleta
        foreach (var a in e.Args)
        {
            if (a == "--roleta-simular") Bloqueio.Simular = true;
            else if (a.StartsWith("--roleta-forcar=")) Bloqueio.Forcar = a["--roleta-forcar=".Length..];
        }

        // Só uma instância por vez
        mutex = new Mutex(false, @"Global\MascoteDeMesa");
        bool livre;
        try { livre = mutex.WaitOne(0); } catch (AbandonedMutexException) { livre = true; }
        if (!livre) { mutex = null; Shutdown(); return; }

        // primeira execução: cria a pasta Dados e instala o pato de exemplo
        Caminhos.CriarPastas();
        PersonagemPadrao.InstalarSeFaltar();

        var janela = new JanelaMascote();
        if (!janela.Iniciar())
        {
            MessageBox.Show($"Nenhum personagem encontrado em:\n{Caminhos.PastaPersonagens}\n\nCrie um com o Editor de Personagens.", "Mascote");
            Shutdown();
            return;
        }
        MainWindow = janela;
        janela.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { mutex?.ReleaseMutex(); } catch { }
        base.OnExit(e);
    }

    // Um erro num quadro da animação não derruba o mascote: anota em Dados\erros.log e segue
    static void AoDarErro(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try { File.AppendAllText(Caminhos.ArqErros, $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}  {e.Exception}\n\n"); } catch { }
    }
}
