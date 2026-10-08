using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mascote.Alertas;

/// <summary>Janela de envio de alerta.</summary>
public partial class JanelaEnviarAlerta : Window
{
    readonly ServicoAlertas alertas;
    readonly CheckBox todos;

    /// <summary>Quantas pessoas devem receber (-1 = todos, 0 = cancelado).</summary>
    public int QtdEnviada { get; private set; }

    public JanelaEnviarAlerta(ServicoAlertas alertas)
    {
        InitializeComponent();
        this.alertas = alertas;
        todos = new CheckBox { Content = "Todos", IsChecked = true, FontWeight = FontWeights.SemiBold };
        Lista.Children.Add(todos);
        var pessoas = alertas.Online;
        foreach (var o in pessoas)
        {
            var cb = new CheckBox { Content = o.Nome, Tag = o.Id, Margin = new Thickness(0, 4, 0, 0), ToolTip = o.Id };
            cb.Checked += (_, _) => todos.IsChecked = false;
            Lista.Children.Add(cb);
        }
        todos.Checked += (_, _) => { foreach (var c in Lista.Children.OfType<CheckBox>()) if (c != todos) c.IsChecked = false; };
        Info.Text = pessoas.Count > 0 ? $"{pessoas.Count} pessoa(s) com o mascote aberto agora."
                                      : "Ninguém mais apareceu online ainda (a lista atualiza a cada 20 segundos).";
        if (alertas.Erro != "")
        {
            Info.Text = $"Atenção: erro ao acessar a pasta da rede: {alertas.Erro}"; Info.Foreground = Brushes.Firebrick;
        }
        Txt.Focus();
    }

    void Enviar_Click(object sender, RoutedEventArgs e)
    {
        var texto = Txt.Text.Trim();
        if (texto == "") { Info.Text = "Escreva a mensagem."; return; }
        var para = Lista.Children.OfType<CheckBox>().Where(c => c != todos && c.IsChecked == true).Select(c => (string)c.Tag).ToList();
        if (todos.IsChecked != true && para.Count == 0) { Info.Text = "Escolha \"Todos\" ou pelo menos uma pessoa."; return; }
        if (todos.IsChecked == true) para = [];
        alertas.Enviar(texto, para);
        QtdEnviada = para.Count > 0 ? para.Count : -1;
        DialogResult = true;
    }
}
