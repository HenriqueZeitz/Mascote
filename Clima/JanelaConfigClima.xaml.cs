using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Mascote.Clima;

/// <summary>Diálogo para escolher a cidade (busca pelo nome no Open-Meteo). ShowDialog() devolve true se salvou.</summary>
public partial class JanelaConfigClima : Window
{
    sealed record CidadeAchada(string Nome, double Lat, double Lon);

    readonly ServicoClima clima;

    public JanelaConfigClima(ServicoClima clima)
    {
        InitializeComponent();
        this.clima = clima;
        TxtCidade.Text = clima.Cidade;
        TxtCidade.Focus();
    }

    async void Buscar_Click(object sender, RoutedEventArgs e)
    {
        Lista.Items.Clear(); BtnOk.IsEnabled = false;
        var nome = TxtCidade.Text.Trim();
        if (nome == "") return;
        BtnBuscar.IsEnabled = false;
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(nome)}&count=10&language=pt&format=json";
            using var doc = JsonDocument.Parse(await ServicoClima.Baixar(url));
            if (doc.RootElement.TryGetProperty("results", out var res))
            {
                foreach (var r in res.EnumerateArray())
                {
                    string?[] pedacos = [Texto(r, "name"), Texto(r, "admin1"), Texto(r, "country")];
                    Lista.Items.Add(new ListBoxItem
                    {
                        Content = string.Join(", ", pedacos.Where(p => !string.IsNullOrEmpty(p))),
                        Tag = new CidadeAchada(Texto(r, "name") ?? nome, r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble())
                    });
                }
            }
            Info.Text = Lista.Items.Count > 0 ? "Escolha a cidade certa e clique em Salvar." : "Nenhuma cidade encontrada com esse nome.";
            if (Lista.Items.Count > 0) { Lista.SelectedIndex = 0; BtnOk.IsEnabled = true; BtnOk.IsDefault = true; }
        }
        catch (Exception ex) { Info.Text = $"Não consegui buscar (sem internet ou bloqueado pela rede?): {ex.Message}"; }
        finally { BtnBuscar.IsEnabled = true; }
    }

    static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    void Lista_DuploClique(object sender, MouseButtonEventArgs e)
    {
        if (Lista.SelectedItem != null) Salvar_Click(sender, e);
    }

    void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if ((Lista.SelectedItem as ListBoxItem)?.Tag is not CidadeAchada c) return;
        clima.Configurar(c.Nome, c.Lat, c.Lon);
        DialogResult = true;
    }
}
