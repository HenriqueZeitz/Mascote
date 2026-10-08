using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mascote.Interop;
using Mascote.Nucleo;
using Microsoft.Win32;

namespace Mascote.Mochila;

/// <summary>Janela da mochila: ver o que está guardado, abrir, tirar ou jogar fora (vai para a Lixeira).</summary>
public partial class JanelaMochila : Window
{
    Point? inicioArrasto;

    public JanelaMochila()
    {
        InitializeComponent();
        Preencher();
    }

    void Preencher()
    {
        Lista.Items.Clear(); double total = 0;
        foreach (var it in ServicoMochila.Itens())
        {
            double tam = ServicoMochila.Tamanho(it);
            total += tam;
            var linha = new DockPanel();
            var img = new Image { Width = 24, Height = 24, Margin = new Thickness(0, 0, 8, 0), Source = IconeShell.Obter(it.FullName) };
            DockPanel.SetDock(img, Dock.Left); linha.Children.Add(img);
            var info = new TextBlock
            {
                Text = $"{ServicoMochila.FormatarTamanho(tam)}   ·   {it.LastWriteTime:dd/MM HH:mm}", Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
            };
            DockPanel.SetDock(info, Dock.Right); linha.Children.Add(info);
            linha.Children.Add(new TextBlock { Text = it.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            Lista.Items.Add(new ListBoxItem { Content = linha, Tag = it.FullName, Padding = new Thickness(4) });
        }
        Resumo.Text = Lista.Items.Count > 0
            ? $"{Lista.Items.Count} item(ns) na mochila  ·  {ServicoMochila.FormatarTamanho(total)}  ·  {Caminhos.PastaMochila}"
            : "A mochila está vazia. Arraste arquivos até o mascote para guardar.";
    }

    string[] Selecionados() => Lista.SelectedItems.Cast<ListBoxItem>().Select(i => (string)i.Tag).ToArray();

    static void AbrirNoWindows(string arquivo, string argumentos = "")
    {
        try { Process.Start(new ProcessStartInfo(arquivo, argumentos) { UseShellExecute = true }); } catch { }
    }

    void Abrir_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in Selecionados()) AbrirNoWindows(p);
    }

    void Mostrar_Click(object sender, RoutedEventArgs e)
    {
        var p = Selecionados().FirstOrDefault();
        if (p != null) AbrirNoWindows("explorer.exe", $"/select,\"{p}\"");
        else AbrirNoWindows("explorer.exe", $"\"{Caminhos.PastaMochila}\"");
    }

    void Tirar_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selecionados(); if (sel.Length == 0) return;
        var dlg = new OpenFolderDialog
        {
            Title = "Para onde tirar os itens da mochila?", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var p in sel) { try { ServicoMochila.Mover(p, dlg.FolderName); } catch { } }
        Preencher();
    }

    void Lixo_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selecionados(); if (sel.Length == 0) return;
        var txt = sel.Length == 1 ? $"Jogar '{Path.GetFileName(sel[0])}' na Lixeira?" : $"Jogar {sel.Length} itens na Lixeira?";
        if (MessageBox.Show(this, txt, "Mochila", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var p in sel) { try { ServicoMochila.MandarParaLixeira(p); } catch { } }
        Preencher();
    }

    // arrastar para fora (o Explorer decide: mesma unidade = move, outra unidade = copia)
    void Lista_BotaoDesce(object sender, MouseButtonEventArgs e) => inicioArrasto = e.GetPosition(Lista);

    void Lista_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || inicioArrasto is not { } ini) return;
        var p = e.GetPosition(Lista);
        if (Math.Abs(p.X - ini.X) + Math.Abs(p.Y - ini.Y) < 8) return;
        var sel = Selecionados(); inicioArrasto = null;
        if (sel.Length == 0) return;
        var dados = new DataObject(DataFormats.FileDrop, sel);
        DragDrop.DoDragDrop(Lista, dados, DragDropEffects.Copy | DragDropEffects.Move);
        Preencher();
    }

    // também dá para arrastar arquivos para dentro desta janela
    void Lista_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var orig = ServicoMochila.DeFora((string[])e.Data.GetData(DataFormats.FileDrop));
        if (orig.Length > 0) { ServicoMochila.Adicionar(orig); Preencher(); }
    }
}
