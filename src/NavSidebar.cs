using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// Left navigation: tab pages grouped under small section headers, each with its own line icon drawn in code.
sealed partial class MemorySelectV3Form {
 sealed class NavHeader { internal string Text; public override string ToString() { return Text; } }
 static readonly string[] WorkTabs = { "Av / Ayarlar", "Explorer Yerleri", "Grim Crypt", "Skiller" };
 ListBox sidebar;

 void BuildNavigation(Panel workspace) {
  sidebar = new ListBox { Dock = DockStyle.Left, Width = 210, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawVariable, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f) };
  var pages = tabs.TabPages.Cast<TabPage>().ToList();
  var work = pages.Where(p => WorkTabs.Contains(p.Text)).ToList();
  var tools = pages.Where(p => !work.Contains(p)).ToList();
  if (work.Count > 0) { sidebar.Items.Add(new NavHeader { Text = "ÇALIŞMA" }); foreach (var p in work) sidebar.Items.Add(p); }
  if (tools.Count > 0) { sidebar.Items.Add(new NavHeader { Text = "ARAÇLAR" }); foreach (var p in tools) sidebar.Items.Add(p); }
  sidebar.MeasureItem += delegate(object sender, MeasureItemEventArgs e) { e.ItemHeight = sidebar.Items[e.Index] is NavHeader ? (e.Index == 0 ? 40 : 54) : 42; };
  sidebar.DrawItem += DrawNavItem;
  int lastPage = -1;
  sidebar.SelectedIndexChanged += delegate {
   if (sidebar.SelectedItem is NavHeader) { sidebar.SelectedIndex = lastPage >= 0 ? lastPage : sidebar.Items.IndexOf(tabs.SelectedTab); return; }
   lastPage = sidebar.SelectedIndex;
   var page = sidebar.SelectedItem as TabPage;
   if (page != null && tabs.SelectedTab != page) tabs.SelectedTab = page;
   sidebar.Invalidate();
  };
  tabs.SelectedIndexChanged += delegate { if (tabs.SelectedTab != null) sidebar.SelectedItem = tabs.SelectedTab; };
  sidebar.MouseMove += delegate(object sender, MouseEventArgs e) {
   int i = sidebar.IndexFromPoint(e.Location);
   sidebar.Cursor = i >= 0 && sidebar.Items[i] is TabPage ? Cursors.Hand : Cursors.Default;
  };
  workspace.Controls.Add(sidebar);
  sidebar.SelectedItem = tabs.SelectedTab ?? (object)work.Concat(tools).FirstOrDefault();
 }

 void DrawNavItem(object sender, DrawItemEventArgs e) {
  if (e.Index < 0) return;
  var g = e.Graphics;
  g.SmoothingMode = SmoothingMode.AntiAlias;
  using (var back = new SolidBrush(sidebar.BackColor)) g.FillRectangle(back, e.Bounds);
  Color fore = sidebar.ForeColor, muted = Blend(fore, sidebar.BackColor, 0.55);
  var header = sidebar.Items[e.Index] as NavHeader;
  if (header != null) {
   var hb = new Rectangle(e.Bounds.X + 22, e.Bounds.Bottom - 24, e.Bounds.Width - 26, 20);
   using (var hf = new Font("Segoe UI", 8.25f, FontStyle.Bold)) TextRenderer.DrawText(g, header.Text, hf, hb, muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
   return;
  }
  bool sel = (e.State & DrawItemState.Selected) != 0;
  if (sel) {
   var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 3, e.Bounds.Width - 20, e.Bounds.Height - 6);
   using (var path = RoundedRect(r, 7)) using (var fill = new SolidBrush(Color.FromArgb(46, accentColor))) g.FillPath(fill, path);
   using (var bar = new SolidBrush(accentColor)) g.FillRectangle(bar, e.Bounds.X + 10, e.Bounds.Y + 11, 3, e.Bounds.Height - 22);
  }
  var page = (TabPage)sidebar.Items[e.Index];
  Color ink = sel ? accentColor : Blend(fore, sidebar.BackColor, 0.75);
  var box = new RectangleF(e.Bounds.X + 26, e.Bounds.Y + (e.Bounds.Height - 18) / 2f, 18, 18);
  using (var pen = new Pen(ink, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
  using (var dot = new SolidBrush(ink)) DrawNavIcon(g, page.Text, box, pen, dot);
  var tb = new Rectangle(e.Bounds.X + 54, e.Bounds.Y, e.Bounds.Width - 60, e.Bounds.Height);
  using (var f = new Font(sidebar.Font, sel ? FontStyle.Bold : FontStyle.Regular))
   TextRenderer.DrawText(g, page.Text, f, tb, sel ? fore : Blend(fore, sidebar.BackColor, 0.85), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
 }

 static Color Blend(Color a, Color b, double t) {
  return Color.FromArgb((int)(a.R * t + b.R * (1 - t)), (int)(a.G * t + b.G * (1 - t)), (int)(a.B * t + b.B * (1 - t)));
 }

 static GraphicsPath RoundedRect(Rectangle r, int radius) {
  var p = new GraphicsPath(); int d = radius * 2;
  p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
  p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
  p.CloseFigure(); return p;
 }

 // Simple line icons in an 18x18 box; colour comes from the theme so they work on dark and light palettes.
 internal static void DrawNavIcon(Graphics g, string title, RectangleF b, Pen pen, Brush dot) {
  float x = b.X, y = b.Y, w = b.Width, h = b.Height, cx = x + w / 2, cy = y + h / 2;
  string t = title.ToLowerInvariant();
  if (t.StartsWith("av")) { // crosshair: hunting target
   g.DrawEllipse(pen, x + 2, y + 2, w - 4, h - 4); g.FillEllipse(dot, cx - 2, cy - 2, 4, 4);
   g.DrawLine(pen, cx, y, cx, y + 4); g.DrawLine(pen, cx, y + h - 4, cx, y + h);
   g.DrawLine(pen, x, cy, x + 4, cy); g.DrawLine(pen, x + w - 4, cy, x + w, cy);
  } else if (t.StartsWith("explorer")) { // compass
   g.DrawEllipse(pen, x + 1, y + 1, w - 2, h - 2);
   g.FillPolygon(dot, new[] { new PointF(cx, y + 3.5f), new PointF(cx + 2.6f, cy), new PointF(cx - 2.6f, cy) });
   g.DrawPolygon(pen, new[] { new PointF(cx, y + h - 3.5f), new PointF(cx + 2.6f, cy), new PointF(cx - 2.6f, cy) });
  } else if (t.StartsWith("grim")) { // crypt gate: arch with a skull-eyed door
   using (var p = new GraphicsPath()) { p.AddLine(x + 2, y + h - 1, x + 2, y + 7); p.AddArc(x + 2, y + 1, w - 4, 12, 180, 180); p.AddLine(x + w - 2, y + 7, x + w - 2, y + h - 1); g.DrawPath(pen, p); }
   g.DrawLine(pen, x, y + h - 1, x + w, y + h - 1);
   g.FillEllipse(dot, cx - 3.4f, y + 7, 2.4f, 2.4f); g.FillEllipse(dot, cx + 1f, y + 7, 2.4f, 2.4f);
   g.DrawLine(pen, cx - 2.5f, y + 12.5f, cx + 2.5f, y + 12.5f);
  } else if (t.StartsWith("güncelleme") || t.StartsWith("guncelleme")) { // download into tray
   g.DrawLine(pen, cx, y + 1, cx, y + 11); g.DrawLine(pen, cx - 4, y + 7, cx, y + 11); g.DrawLine(pen, cx + 4, y + 7, cx, y + 11);
   g.DrawLines(pen, new[] { new PointF(x + 1, y + 12), new PointF(x + 1, y + h - 1), new PointF(x + w - 1, y + h - 1), new PointF(x + w - 1, y + 12) });
  } else if (t.StartsWith("çift") || t.StartsWith("cift")) { // two characters
   g.DrawEllipse(pen, x + 1, y + 4, 6, 6); g.DrawArc(pen, x - 1, y + 12, 10, 10, 200, 140);
   g.DrawEllipse(pen, x + 9, y + 1, 7, 7); g.DrawArc(pen, x + 7, y + 10, 12, 12, 200, 140);
  } else if (t.Contains("npc")) { // character with speech bubble
   g.DrawEllipse(pen, x + 2, y + 4, 7, 7); g.DrawArc(pen, x - 0.5f, y + 13, 12, 12, 200, 140);
   g.DrawEllipse(pen, x + 10, y, 7, 6); g.DrawLine(pen, x + 11.5f, y + 5.5f, x + 10.5f, y + 8);
  } else if (t.StartsWith("skill")) { // four-point star: skills and buffs
   var star = new PointF[8];
   for (int i = 0; i < 8; i++) { double a = Math.PI / 4 * i - Math.PI / 2; float rr = i % 2 == 0 ? w / 2 - 0.5f : 3f; star[i] = new PointF(cx + (float)(Math.Cos(a) * rr), cy + (float)(Math.Sin(a) * rr)); }
   g.DrawPolygon(pen, star); g.FillEllipse(dot, cx - 1.5f, cy - 1.5f, 3, 3);
  } else if (t.StartsWith("log")) { // list lines
   for (int i = 0; i < 3; i++) { float ly = y + 4 + i * 5; g.FillEllipse(dot, x + 1, ly - 1.2f, 2.4f, 2.4f); g.DrawLine(pen, x + 6, ly, x + w - 1, ly); }
  } else g.FillEllipse(dot, cx - 3, cy - 3, 6, 6);
 }
}
