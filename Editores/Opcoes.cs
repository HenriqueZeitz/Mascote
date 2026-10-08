using System.Windows.Controls;

namespace Mascote.Editores;

/// <summary>Listas de opções (ComboBox) dos editores: o texto aparece na tela e a chave fica na Tag.</summary>
static class Opcoes
{
    public static void Adicionar(ComboBox cmb, IEnumerable<KeyValuePair<string, string>> pares)
    {
        foreach (var (chave, texto) in pares) cmb.Items.Add(new ComboBoxItem { Content = texto, Tag = chave });
    }

    public static void Selecionar(ComboBox cmb, string chave)
    {
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            if ((string)((ComboBoxItem)cmb.Items[i]).Tag == chave) { cmb.SelectedIndex = i; return; }
        }
    }

    public static string Selecionada(ComboBox cmb) => (string)((ComboBoxItem)cmb.SelectedItem).Tag;

    /// <summary>Nome de pasta válido a partir do nome digitado.</summary>
    public static string NomeDePasta(string nome) => string.Concat(nome.Where(c => !"\\/:*?\"<>|".Contains(c))).Trim();

    public static string MsgErro(Exception ex) => ex.InnerException?.Message ?? ex.Message;
}
