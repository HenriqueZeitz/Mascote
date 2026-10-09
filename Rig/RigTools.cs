using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mascote.Rig;

/// <summary>Carrega a imagem, detecta os membros e recorta cada membro numa camada PNG.</summary>
public static class RigTools
{
    const int TRANSP = -1, LINHA = -2, NOVO = int.MinValue;

    static bool Op(ImgData d, int i) { return d.Px[i * 4 + 3] > 24; }
    static double Lum(ImgData d, int i) { return 0.114 * d.Px[i * 4] + 0.587 * d.Px[i * 4 + 1] + 0.299 * d.Px[i * 4 + 2]; }
    static int Diff(ImgData d, int a, int b) {
      return Math.Abs(d.Px[a * 4] - d.Px[b * 4]) + Math.Abs(d.Px[a * 4 + 1] - d.Px[b * 4 + 1]) + Math.Abs(d.Px[a * 4 + 2] - d.Px[b * 4 + 2]);
    }

    // ---------- Entrada e saída ----------

    static byte[] LoadRaw(string path, out int w, out int h) {
      var bi = new BitmapImage();
      bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
      bi.UriSource = new Uri(Path.GetFullPath(path)); bi.EndInit();
      BitmapSource src = new FormatConvertedBitmap(bi, PixelFormats.Bgra32, null, 0);
      w = src.PixelWidth; h = src.PixelHeight;
      var px = new byte[w * h * 4]; src.CopyPixels(px, w * 4, 0);
      return px;
    }

    public static ImgData Load(string path, int maxH) {
      int w, h; var px = LoadRaw(path, out w, out h);
      return Recortar(px, w, h, maxH);
    }

    // Para imagens sem transparência (fundo branco, ou o "xadrez" de transparência falso desenhado na imagem):
    // pega as cores mais comuns da borda e apaga tudo dessas cores que estiver ligado à borda.
    public static ImgData LoadSemFundo(string path, int maxH) {
      int w, h; var px = LoadRaw(path, out w, out h);
      var cont = new Dictionary<int, int>(); int total = 0;
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
        if (x != 0 && y != 0 && x != w - 1 && y != h - 1) continue;
        int i = (y * w + x) * 4; int k = ((px[i] >> 4) << 8) | ((px[i + 1] >> 4) << 4) | (px[i + 2] >> 4);
        int c; cont.TryGetValue(k, out c); cont[k] = c + 1; total++;
      }
      var cores = cont.Where(kv => kv.Value >= total * 0.08).OrderByDescending(kv => kv.Value).Take(3)
                      .Select(kv => new[] { ((kv.Key >> 8) & 15) * 16 + 8, ((kv.Key >> 4) & 15) * 16 + 8, (kv.Key & 15) * 16 + 8 }).ToList();
      if (cores.Count == 0) throw new Exception("Não consegui identificar a cor do fundo dessa imagem.");
      Func<int, bool> ehFundo = i => {
        foreach (var c in cores) if (Math.Abs(px[i * 4] - c[0]) + Math.Abs(px[i * 4 + 1] - c[1]) + Math.Abs(px[i * 4 + 2] - c[2]) <= 60) return true;
        return false;
      };
      var vis = new bool[w * h]; var pilha = new Stack<int>();
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
        if (x != 0 && y != 0 && x != w - 1 && y != h - 1) continue;
        int i = y * w + x; if (!vis[i] && ehFundo(i)) { vis[i] = true; pilha.Push(i); }
      }
      while (pilha.Count > 0) {
        int j = pilha.Pop(), x = j % w, y = j / w; px[j * 4 + 3] = 0;
        for (int k = 0; k < 4; k++) {
          int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          int m = ny * w + nx; if (!vis[m] && ehFundo(m)) { vis[m] = true; pilha.Push(m); }
        }
      }
      return Recortar(px, w, h, maxH);
    }

    static ImgData Recortar(byte[] px, int w, int h, int maxH) {
      int x0 = w, y0 = h, x1 = -1, y1 = -1, transp = 0;
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
        if (px[(y * w + x) * 4 + 3] > 24) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        else transp++;
      }
      if (x1 < 0) throw new Exception("A imagem está totalmente transparente.");
      if (transp < w * h / 100) throw new Exception("A imagem não tem fundo transparente. Use um PNG com o personagem recortado.");
      const int pad = 2;   // recorta no contorno do desenho, com uma pequena margem
      var d = new ImgData { W = x1 - x0 + 1 + 2 * pad, H = y1 - y0 + 1 + 2 * pad };
      d.Px = new byte[d.W * d.H * 4];
      for (int y = y0; y <= y1; y++) Buffer.BlockCopy(px, (y * w + x0) * 4, d.Px, ((y - y0 + pad) * d.W + pad) * 4, (x1 - x0 + 1) * 4);
      if (d.H > maxH) d = Resize(d, maxH);
      return d;
    }

    static ImgData Resize(ImgData d, int nh) {
      int nw = Math.Max(1, (int)Math.Round(d.W * (double)nh / d.H));
      var dv = new DrawingVisual();
      RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
      using (var dc = dv.RenderOpen()) dc.DrawImage(ToBitmap(d), new Rect(0, 0, nw, nh));
      var rtb = new RenderTargetBitmap(nw, nh, 96, 96, PixelFormats.Pbgra32); rtb.Render(dv);
      var r = new ImgData { W = nw, H = nh, Px = new byte[nw * nh * 4] };
      new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0).CopyPixels(r.Px, nw * 4, 0);
      return r;
    }

    public static BitmapSource ToBitmap(ImgData d) {
      var b = BitmapSource.Create(d.W, d.H, 96, 96, PixelFormats.Bgra32, null, d.Px, d.W * 4); b.Freeze(); return b;
    }

    public static void Save(ImgData d, string path) {
      var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(ToBitmap(d)));
      using (var fs = File.Create(path)) enc.Save(fs);
    }

    // ---------- Análise: separa o desenho em regiões de cor, delimitadas pelos contornos escuros ----------

    public static int[] Label(ImgData d, out List<Comp> comps) {
      int W = d.W, H = d.H, n = W * H;
      var lab = new int[n]; comps = new List<Comp>();
      for (int i = 0; i < n; i++) lab[i] = !Op(d, i) ? TRANSP : (Lum(d, i) < 95 ? LINHA : NOVO);
      var q = new int[n];
      for (int s = 0; s < n; s++) {
        if (lab[s] != NOVO) continue;
        int id = comps.Count; var c = new Comp(); comps.Add(c);
        int qh = 0, qt = 0; q[qt++] = s; lab[s] = id;
        double sx = 0, sy = 0, sr = 0, sg = 0, sb = 0;
        while (qh < qt) {
          int j = q[qh++], x = j % W, y = j / W;
          c.Area++; sx += x; sy += y; sb += d.Px[j * 4]; sg += d.Px[j * 4 + 1]; sr += d.Px[j * 4 + 2];
          if (x < c.X0) c.X0 = x; if (x > c.X1) c.X1 = x; if (y < c.Y0) c.Y0 = y; if (y > c.Y1) c.Y1 = y;
          for (int k = 0; k < 4; k++) {
            int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            int m = ny * W + nx;
            if (lab[m] == NOVO && Diff(d, j, m) <= 48) { lab[m] = id; q[qt++] = m; }
          }
        }
        c.Cx = sx / c.Area; c.Cy = sy / c.Area; c.R = sr / c.Area; c.G = sg / c.Area; c.B = sb / c.Area;
      }
      return lab;
    }

    static bool Inside(RigPart p, int x, int y) {
      double px = x + 0.5, py = y + 0.5;
      if (p.Forma == "elipse") {
        double rx = p.W / 2, ry = p.H / 2; if (rx <= 0 || ry <= 0) return false;
        double dx = (px - (p.X + rx)) / rx, dy = (py - (p.Y + ry)) / ry;
        return dx * dx + dy * dy <= 1;
      }
      if (p.Forma == "livre" && p.Pontos != null && p.Pontos.Length >= 6) {
        // contorno livre: conta quantas arestas uma linha horizontal a partir do ponto cruza (ímpar = dentro)
        var q = p.Pontos; bool dentro = false;
        for (int i = 0, j = q.Length - 2; i < q.Length; j = i, i += 2) {
          double xi = q[i], yi = q[i + 1], xj = q[j], yj = q[j + 1];
          if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) dentro = !dentro;
        }
        return dentro;
      }
      return px >= p.X && px < p.X + p.W && py >= p.Y && py < p.Y + p.H;
    }

    // Pixels que pertencem ao membro: regiões de cor que estão majoritariamente dentro da área marcada,
    // e os contornos ficam com a região mais próxima (assim o contorno entre corpo e membro é dividido ao meio).
    static bool[] Mask(ImgData d, int[] lab, List<Comp> comps, RigPart p, bool[] claimed) {
      int W = d.W, H = d.H, n = W * H;
      var inS = new bool[n]; var cnt = new int[comps.Count]; var m = new bool[n];
      int xa = Math.Max(0, (int)Math.Floor(p.X)), xb = Math.Min(W - 1, (int)Math.Ceiling(p.X + p.W));
      int ya = Math.Max(0, (int)Math.Floor(p.Y)), yb = Math.Min(H - 1, (int)Math.Ceiling(p.Y + p.H));
      for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) {
        int i = y * W + x;
        if (lab[i] != TRANSP && !claimed[i] && Inside(p, x, y)) { inS[i] = true; if (lab[i] >= 0) cnt[lab[i]]++; }
      }
      var own = new sbyte[n]; var q = new int[n]; int qh = 0, qt = 0;
      for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) {
        int i = y * W + x;
        if (inS[i] && lab[i] >= 0) { own[i] = (sbyte)(cnt[lab[i]] * 2 >= comps[lab[i]].Area ? 1 : 2); q[qt++] = i; }
      }
      while (qh < qt) {
        int j = q[qh++], x = j % W, y = j / W;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
          int nx = x + dx, ny = y + dy;
          if (nx < xa || ny < ya || nx > xb || ny > yb) continue;
          int mm = ny * W + nx;
          if (inS[mm] && lab[mm] == LINHA && own[mm] == 0) { own[mm] = own[j]; q[qt++] = mm; }
        }
      }
      int incluidos = 0;
      for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) { int i = y * W + x; if (inS[i] && own[i] == 1) incluidos++; }
      for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) {
        int i = y * W + x;   // contorno solto só entra se o membro for feito só de contorno
        m[i] = inS[i] && (own[i] == 1 || (own[i] == 0 && lab[i] == LINHA && incluidos == 0));
      }
      if (p.Preencher) {
        // membro desenhado por cima do corpo: leva o contorno inteiro (senão sobra meio contorno no corpo)
        int lim = (int)Math.Ceiling(Math.Max(2, Math.Round(H * 0.03)) * 1.5) + 1;
        var dd = new int[n]; qh = 0; qt = 0;
        for (int i = 0; i < n; i++) if (m[i]) q[qt++] = i;
        while (qh < qt) {
          int j = q[qh++], x = j % W, y = j / W;
          if (dd[j] >= lim) continue;
          for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int nx = x + dx, ny = y + dy;
            if (nx < xa || ny < ya || nx > xb || ny > yb) continue;
            int mm = ny * W + nx;
            if (inS[mm] && !m[mm] && lab[mm] == LINHA) { m[mm] = true; dd[mm] = dd[j] + 1; q[qt++] = mm; }
          }
        }
      }
      return m;
    }

    // Preenche o buraco deixado por um membro, espalhando a cor da vizinhança para dentro
    static void Fill(byte[] L, bool[] hole, ImgData d) {
      int W = d.W, H = d.H, n = W * H;
      var feito = new bool[n]; var todo = new List<int>();
      for (int i = 0; i < n; i++) {
        feito[i] = L[i * 4 + 3] > 24;
        if (hole[i] && Op(d, i) && !feito[i]) todo.Add(i);
      }
      var preenchidos = new List<int>(todo);
      // primeiro espalha só cores claras (ignora contornos escuros); se travar, aceita qualquer cor
      for (int fase = 0; fase < 2 && todo.Count > 0; fase++) {
        while (todo.Count > 0) {
          var prox = new List<int>(); var novos = new List<int[]>();
          foreach (int i in todo) {
            int x = i % W, y = i / W, c = 0; double b = 0, g = 0, r = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
              int nx = x + dx, ny = y + dy;
              if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
              int m = ny * W + nx;
              if (!feito[m]) continue;
              if (fase == 0 && 0.114 * L[m * 4] + 0.587 * L[m * 4 + 1] + 0.299 * L[m * 4 + 2] < 95) continue;
              b += L[m * 4]; g += L[m * 4 + 1]; r += L[m * 4 + 2]; c++;
            }
            if (c > 0) novos.Add(new[] { i, (int)(b / c), (int)(g / c), (int)(r / c) }); else prox.Add(i);
          }
          if (novos.Count == 0) break;
          foreach (var v in novos) { int i = v[0]; L[i * 4] = (byte)v[1]; L[i * 4 + 1] = (byte)v[2]; L[i * 4 + 2] = (byte)v[3]; L[i * 4 + 3] = 255; feito[i] = true; }
          todo = prox;
        }
      }
      // suaviza o preenchimento (tira as marcas em "X" do espalhamento)
      for (int it = 0; it < 12; it++) {
        var cores = new int[preenchidos.Count * 3];
        for (int k = 0; k < preenchidos.Count; k++) {
          int i = preenchidos[k], x = i % W, y = i / W, c = 0; double b = 0, g = 0, r = 0;
          for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            int m = ny * W + nx;
            if (!feito[m] || 0.114 * L[m * 4] + 0.587 * L[m * 4 + 1] + 0.299 * L[m * 4 + 2] < 95) continue;
            b += L[m * 4]; g += L[m * 4 + 1]; r += L[m * 4 + 2]; c++;
          }
          if (c == 0) { cores[k * 3] = L[i * 4]; cores[k * 3 + 1] = L[i * 4 + 1]; cores[k * 3 + 2] = L[i * 4 + 2]; }
          else { cores[k * 3] = (int)(b / c); cores[k * 3 + 1] = (int)(g / c); cores[k * 3 + 2] = (int)(r / c); }
        }
        for (int k = 0; k < preenchidos.Count; k++) {
          int i = preenchidos[k]; if (!feito[i]) continue;
          L[i * 4] = (byte)cores[k * 3]; L[i * 4 + 1] = (byte)cores[k * 3 + 1]; L[i * 4 + 2] = (byte)cores[k * 3 + 2];
        }
      }
    }

    static int Prio(string p) {
      switch (p) { case "olhos": case "boca": return 0; case "braco": return 1; case "pe": return 2; case "cauda": return 3; case "cabeca": return 4; default: return 5; }
    }

    // Recorta cada membro numa camada (parte_N.png) e o que sobra vira o corpo (base.png)
    public static void BuildRig(ImgData d, RigPart[] parts, string dir) {
      List<Comp> comps; var lab = Label(d, out comps);
      int n = d.W * d.H; var claimed = new bool[n];
      var masks = new bool[parts.Length][]; var layers = new byte[parts.Length][];
      var ordem = Enumerable.Range(0, parts.Length).OrderBy(k => Prio(parts[k].Papel)).ThenBy(k => parts[k].W * parts[k].H).ToArray();
      foreach (int k in ordem) {
        var m = Mask(d, lab, comps, parts[k], claimed); var L = new byte[n * 4];
        for (int i = 0; i < n; i++) if (m[i]) { claimed[i] = true; Buffer.BlockCopy(d.Px, i * 4, L, i * 4, 4); }
        masks[k] = m; layers[k] = L;
      }
      var bas = new byte[n * 4];
      for (int i = 0; i < n; i++) if (!claimed[i]) Buffer.BlockCopy(d.Px, i * 4, bas, i * 4, 4);
      for (int k = 0; k < parts.Length; k++)
        if (parts[k].Preencher) Fill(parts[k].Pai >= 0 && parts[k].Pai < parts.Length ? layers[parts[k].Pai] : bas, masks[k], d);
      Save(new ImgData { W = d.W, H = d.H, Px = bas }, Path.Combine(dir, "base.png"));
      for (int k = 0; k < parts.Length; k++) Save(new ImgData { W = d.W, H = d.H, Px = layers[k] }, Path.Combine(dir, "parte_" + k + ".png"));
    }

    // ---------- Detecção automática dos membros ----------

    static bool Inter(Comp o, double x0, double y0, double x1, double y1) { return o.X1 >= x0 && o.X0 <= x1 && o.Y1 >= y0 && o.Y0 <= y1; }

    static List<int[]> Faixa(int[] lab, int W, int H, int ya, int yb, double minW) {
      var res = new List<int[]>(); if (ya < 0) ya = 0;
      var vis = new bool[W * H]; var pilha = new Stack<int>();
      for (int y = ya; y <= yb; y++) for (int x = 0; x < W; x++) {
        int s = y * W + x; if (vis[s] || lab[s] == TRANSP) continue;
        int x0 = x, x1 = x, y0 = y, y1 = y; vis[s] = true; pilha.Push(s);
        while (pilha.Count > 0) {
          int j = pilha.Pop(), jx = j % W, jy = j / W;
          if (jx < x0) x0 = jx; if (jx > x1) x1 = jx; if (jy < y0) y0 = jy; if (jy > y1) y1 = jy;
          for (int k = 0; k < 4; k++) {
            int nx = jx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = jy + (k == 2 ? 1 : k == 3 ? -1 : 0);
            if (nx < 0 || nx >= W || ny < ya || ny > yb) continue;
            int m = ny * W + nx; if (!vis[m] && lab[m] != TRANSP) { vis[m] = true; pilha.Push(m); }
          }
        }
        if (x1 - x0 + 1 >= minW) res.Add(new[] { x0, y0, x1, y1 });
      }
      return res;
    }

    public static List<RigPart> Detect(ImgData d, out string olhando) {
      olhando = "frente";
      List<Comp> comps; var lab = Label(d, out comps);
      int W = d.W, H = d.H, n = W * H;
      var res = new List<RigPart>();
      int opaco = 0, oy0 = H, oy1 = -1;
      for (int i = 0; i < n; i++) if (lab[i] != TRANSP) { opaco++; int y = i / W; if (y < oy0) oy0 = y; if (y > oy1) oy1 = y; }
      if (opaco == 0 || comps.Count == 0) return res;
      double alt = oy1 - oy0 + 1, lin = Math.Max(2, Math.Round(alt * 0.03));

      // distância de cada pixel até o fundo transparente (para saber quais regiões ficam na borda da silhueta)
      var dist = new int[n]; var q = new int[n * 2]; int qh = 0, qt = 0;
      for (int i = 0; i < n; i++) {
        int x = i % W, y = i / W;
        if (lab[i] == TRANSP) { dist[i] = 0; q[qt++] = i; }
        else if (x == 0 || y == 0 || x == W - 1 || y == H - 1) { dist[i] = 1; q[qt++] = i; }
        else dist[i] = int.MaxValue;
      }
      while (qh < qt) {
        int j = q[qh++], x = j % W, y = j / W;
        for (int k = 0; k < 4; k++) {
          int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
          if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
          int m = ny * W + nx; if (dist[m] > dist[j] + 1) { dist[m] = dist[j] + 1; if (qt < q.Length) q[qt++] = m; }
        }
      }
      var borda = new bool[comps.Count];
      for (int i = 0; i < n; i++) if (lab[i] >= 0 && dist[i] <= lin * 1.5 + 1) borda[lab[i]] = true;

      // corpo: maior região na parte de baixo
      double minA = Math.Max(4, opaco * 0.003);
      int corpo = -1;
      for (int c = 0; c < comps.Count; c++)
        if (comps[c].Area >= minA && comps[c].Cy >= oy0 + 0.4 * alt && (corpo < 0 || comps[c].Area > comps[corpo].Area)) corpo = c;
      if (corpo < 0) for (int c = 0; c < comps.Count; c++) if (corpo < 0 || comps[c].Area > comps[corpo].Area) corpo = c;
      var B = comps[corpo];
      var usados = new HashSet<int> { corpo };

      // cabeça: maior região acima do corpo, mais o que estiver grudado nela (bico, nariz, cabelo...)
      int cab = -1;
      for (int c = 0; c < comps.Count; c++)
        if (c != corpo && comps[c].Area >= 0.04 * opaco && comps[c].Cy < B.Cy && comps[c].Cy <= oy0 + 0.55 * alt && (cab < 0 || comps[c].Area > comps[cab].Area)) cab = c;
      RigPart? pCab = null;
      if (cab >= 0) {
        var C = comps[cab]; usados.Add(cab);
        double x0 = C.X0, y0 = C.Y0, x1 = C.X1, y1 = C.Y1;
        for (int c = 0; c < comps.Count; c++) {
          if (usados.Contains(c)) continue; var o = comps[c];
          if (o.Area < C.Area && o.Cy < C.Y1 - 0.2 * C.Ht && Inter(o, C.X0 - lin, C.Y0 - lin, C.X1 + lin, C.Y1 + lin)) {
            usados.Add(c); x0 = Math.Min(x0, o.X0); y0 = Math.Min(y0, o.Y0); x1 = Math.Max(x1, o.X1); y1 = Math.Max(y1, o.Y1);
          }
        }
        if (y0 - oy0 < 0.15 * alt) y0 = oy0;   // inclui topete/orelhas/antenas acima da cabeça
        double dx = C.Cx - B.Cx;
        olhando = dx > 0.12 * B.Wd ? "direita" : dx < -0.12 * B.Wd ? "esquerda" : "frente";
        pCab = new RigPart { Papel = "cabeca", X = x0 - lin, Y = y0 - lin, W = x1 - x0 + 1 + 2 * lin, H = y1 - y0 + 1 + 2 * lin, PX = C.Cx, PY = C.Y1 };
        res.Add(pCab);

        // olhos: regiões brancas pequenas dentro da cabeça
        double ex0 = 1e9, ey0 = 1e9, ex1 = -1, ey1 = -1;
        for (int c = 0; c < comps.Count; c++) {
          var o = comps[c]; double cx = (o.X0 + o.X1) / 2.0, cy = (o.Y0 + o.Y1) / 2.0;
          if (c != cab && o.Area >= 2 && o.Area < 0.2 * C.Area && o.Lum > 200 && o.Sat < 0.1 &&
              cx >= pCab.X && cx <= pCab.X + pCab.W && cy >= pCab.Y && cy <= pCab.Y + pCab.H) {
            ex0 = Math.Min(ex0, o.X0); ey0 = Math.Min(ey0, o.Y0); ex1 = Math.Max(ex1, o.X1); ey1 = Math.Max(ey1, o.Y1);
          }
        }
        if (ex1 >= 0) {
          double mg = lin + 1;
          res.Add(new RigPart { Papel = "olhos", X = ex0 - mg, Y = ey0 - mg, W = ex1 - ex0 + 1 + 2 * mg, H = ey1 - ey0 + 1 + 2 * mg,
                                PX = (ex0 + ex1 + 1) / 2.0, PY = (ey0 + ey1 + 1) / 2.0, Preencher = true });
        }
      }

      // pés: sobe uma faixa a partir do chão enquanto houver pedaços separados
      var pes = new List<RigPart>();
      List<int[]>? caixas = null; int prev = 0;
      double minW = Math.Max(2, W * 0.04);
      for (int k = 1; k <= alt * 0.3; k++) {
        var cx = Faixa(lab, W, H, oy1 - k + 1, oy1, minW);
        if (cx.Count >= 2 && cx.Count >= prev) { caixas = cx; prev = cx.Count; }
        else if (prev >= 2 && cx.Count < prev) break;
      }
      if (caixas != null) {
        var dosPes = new HashSet<int>();
        foreach (var bx in caixas.OrderByDescending(b => b[2] - b[0]).Take(4)) {
          double x0 = bx[0], y0 = bx[1], x1 = bx[2], y1 = bx[3];
          for (int y = bx[1]; y <= bx[3]; y++) for (int x = bx[0]; x <= bx[2]; x++) {
            int l = lab[y * W + x];
            if (l >= 0 && l != corpo && !usados.Contains(l) && comps[l].Area < 0.5 * B.Area && !dosPes.Contains(l)) {
              dosPes.Add(l); var o = comps[l];
              x0 = Math.Min(x0, o.X0); y0 = Math.Min(y0, o.Y0); x1 = Math.Max(x1, o.X1); y1 = Math.Max(y1, o.Y1);
            }
          }
          double mg = Math.Ceiling(lin * 0.6);
          pes.Add(new RigPart { Papel = "pe", X = x0 - mg, Y = y0 - mg, W = x1 - x0 + 1 + 2 * mg, H = y1 - y0 + 1 + 2 * mg, PX = (x0 + x1 + 1) / 2.0, PY = y0 });
        }
        foreach (var l in dosPes) usados.Add(l);
        res.AddRange(pes);
      }

      // braços/asas desenhados por cima do corpo: regiões fechadas dentro do corpo
      var dentro = new List<int>();
      for (int c = 0; c < comps.Count; c++) {
        var o = comps[c]; if (usados.Contains(c) || borda[c]) continue;
        if (o.Area < 0.03 * B.Area || o.Area > 0.45 * B.Area) continue;
        double ix = Math.Max(0, Math.Min(o.X1, B.X1 + lin) - Math.Max(o.X0, B.X0 - lin) + 1);
        double iy = Math.Max(0, Math.Min(o.Y1, B.Y1 + lin) - Math.Max(o.Y0, B.Y0 - lin) + 1);
        if (ix * iy >= 0.85 * o.Wd * o.Ht) dentro.Add(c);
      }
      foreach (int c in dentro.OrderByDescending(c => comps[c].Area).Take(2)) {
        var o = comps[c]; usados.Add(c);
        double px = olhando == "direita" ? o.X0 + 0.8 * o.Wd : olhando == "esquerda" ? o.X0 + 0.2 * o.Wd : o.Cx;
        double py = olhando == "frente" ? o.Y0 + 0.1 * o.Ht : o.Y0 + 0.3 * o.Ht;
        res.Add(new RigPart { Papel = "braco", X = o.X0 - lin, Y = o.Y0 - lin, W = o.Wd + 2 * lin, H = o.Ht + 2 * lin, PX = px, PY = py, Preencher = true });
      }

      // braços (de frente) ou cauda (de lado): regiões que saem para os lados do corpo
      double topo = pCab != null ? pCab.Y + pCab.H * 0.6 : oy0;
      double fundo = pes.Count > 0 ? pes.Min(p => p.Y) : oy1;
      for (int c = 0; c < comps.Count; c++) {
        var o = comps[c];
        if (usados.Contains(c) || !borda[c] || o.Area < 0.01 * opaco || o.Area > 0.6 * B.Area) continue;
        if (o.Cy < topo || o.Cy > fundo) continue;
        if (!Inter(o, B.X0 - 2 * lin, B.Y0 - 2 * lin, B.X1 + 2 * lin, B.Y1 + 2 * lin)) continue;
        bool esq = (B.X0 - o.X0) > 0.25 * o.Wd && o.Cx < B.Cx;
        bool dir = (o.X1 - B.X1) > 0.25 * o.Wd && o.Cx > B.Cx;
        if (!esq && !dir) continue;
        string papel;
        if (olhando == "frente") papel = "braco";
        else if ((olhando == "direita" && esq) || (olhando == "esquerda" && dir)) papel = "cauda";
        else continue;
        usados.Add(c);
        res.Add(new RigPart { Papel = papel, X = o.X0 - lin, Y = o.Y0 - lin, W = o.Wd + 2 * lin, H = o.Ht + 2 * lin,
                              PX = esq ? o.X1 : o.X0, PY = papel == "cauda" ? o.Cy : o.Y0 + 0.15 * o.Ht });
      }
      return res;
    }
}
