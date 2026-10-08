using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Mascote.Alertas;

/// <summary>Configuração: pasta da rede e nome que aparece para os outros. ShowDialog() devolve true se salvou.</summary>
public partial class JanelaConfigAlertas : Window
{
    readonly ServicoAlertas alertas;

    public JanelaConfigAlertas(ServicoAlertas alertas)
    {
        InitializeComponent();
        this.alertas = alertas;
        TxtPasta.Text = alertas.Pasta; TxtNome.Text = alertas.Nome;
        if (alertas.Erro != "") Info.Text = $"Erro ao acessar a pasta: {alertas.Erro}";
    }

    void Procurar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Escolha a pasta compartilhada para os alertas do mascote" };
        if (TxtPasta.Text != "") dlg.InitialDirectory = TxtPasta.Text;
        if (dlg.ShowDialog(this) == true) TxtPasta.Text = dlg.FolderName;
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        string pasta = TxtPasta.Text.Trim().TrimEnd('\\'), nome = TxtNome.Text.Trim();
        if (nome == "") { Info.Text = "Informe seu nome."; return; }
        if (pasta != "")
        {
            try   // confere se dá para escrever lá
            {
                Directory.CreateDirectory(Path.Combine(pasta, "mensagens"));
                Directory.CreateDirectory(Path.Combine(pasta, "online"));
                var teste = Path.Combine(pasta, "online", $"teste_{Environment.ProcessId}.tmp");
                File.WriteAllText(teste, "ok"); File.Delete(teste);
            }
            catch (Exception ex) { Info.Text = $"Não consegui gravar nessa pasta: {ex.Message}"; return; }
        }
        alertas.Configurar(pasta, nome);
        DialogResult = true;
    }
}
