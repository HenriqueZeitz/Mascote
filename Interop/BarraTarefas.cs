using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;

namespace Mascote.Interop;

public static class BarraTarefas
{
    /// <summary>
    /// Posição (em X, unidades da tela do WPF) do relógio da barra de tarefas do monitor <paramref name="area"/>,
    /// achada pela automação de interface do Windows. Null se não der para descobrir.
    /// </summary>
    public static double? PosicaoRelogio(Window janela, Rect area)
    {
        try
        {
            var m = PresentationSource.FromVisual(janela)!.CompositionTarget.TransformFromDevice;   // pixels -> unidades WPF (DPI)
            var raiz = AutomationElement.RootElement;
            // barra principal e barras dos outros monitores; fica com a que está no mesmo monitor do mascote
            AutomationElement? barra = null;
            foreach (var classe in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            {
                var cond = new PropertyCondition(AutomationElement.ClassNameProperty, classe);
                foreach (AutomationElement b in raiz.FindAll(TreeScope.Children, cond))
                {
                    var rb = b.Current.BoundingRectangle;
                    var cxb = m.Transform(new Point(rb.X + rb.Width / 2, 0)).X;
                    if (cxb >= area.Left && cxb <= area.Right) { barra = b; break; }
                }
                if (barra != null) break;
            }
            if (barra == null) return null;

            Rect? r = null;
            foreach (AutomationElement e in barra.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
            {
                var c = e.Current;
                // Windows 10: TrayClockWClass | Windows 11: botão cujo nome tem a hora ("Relógio 09:26 ...")
                if (c.BoundingRectangle.Width > 0 && (c.ClassName == "TrayClockWClass" || Regex.IsMatch(c.Name ?? "", @"\d{1,2}:\d{2}")))
                {
                    r = c.BoundingRectangle; break;
                }
            }
            var cx = r is { } rr ? rr.X + rr.Width / 2 : barra.Current.BoundingRectangle.Right - 60;
            return m.Transform(new Point(cx, 0)).X;
        }
        catch { return null; }
    }
}
