using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// World map: the game's own terrain map with the dungeon entrances placed at their world coordinates.
sealed class WorldMapView : Control {
    // The map is stitched from the game's own minimap tiles (zoom 6, 4x4 regions per tile), so world
    // coordinates map to pixels exactly: pixel = coordinate / 4 + offset, with Y flipped (north is up).
    internal const double AX = 0.25, AY = 0.0, AC = 3408.0, BX = 0.0, BY = -0.25, BC = 1020.0;    internal const string ResourceName = "world-map.jpg";
    static readonly KeyValuePair<int, string>[] Towns = {
        new KeyValuePair<int, string>(20, "Constantinople"), new KeyValuePair<int, string>(25, "Samarkand"),
        new KeyValuePair<int, string>(5, "Hotan"), new KeyValuePair<int, string>(2, "Donwhang"),
        new KeyValuePair<int, string>(1, "Jangan"), new KeyValuePair<int, string>(252, "Nanzhang")
    };

    Bitmap baseMap;
    bool userMoved, dragging, moved;
    // View state in artwork pixels: zoom = screen pixels per artwork pixel, (cx, cy) = artwork pixel at the view centre.
    double zoom = 0.4, cx = 1833, cy = 667, dragCx, dragCy;
    Point dragStart;
    List<DungeonLocation> sites = new List<DungeonLocation>();

    internal Position Player;
    internal string Selected;
    internal Func<string, bool> Visited;
    internal event Action<string> DungeonPicked;
    internal bool Ready { get { return baseMap != null; } }

    internal static PointF ToImage(double x, double y) {
        return new PointF((float)(AX * x + AY * y + AC), (float)(BX * x + BY * y + BC));
    }

    internal IEnumerable<DungeonLocation> Sites {
        set { sites = value.GroupBy(d => d.Code).Select(g => g.First()).OrderBy(d => d.Code).ToList(); Invalidate(); }
    }

    internal WorldMapView() {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        VisibleChanged += delegate { if (Visible) LoadBase(); };
    }

    internal void LoadBase() {
        if (baseMap != null) return;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)) {
            if (stream == null) return;
            using (var image = Image.FromStream(stream)) baseMap = new Bitmap(image);
        }
        if (!userMoved) Fit();
        Invalidate();
    }

    void Fit() {
        if (Width < 50 || Height < 50) return;
        double w = baseMap != null ? baseMap.Width : 3667, h = baseMap != null ? baseMap.Height : 1334;
        zoom = Math.Min((Width - 20) / w, (Height - 20) / h);
        cx = w / 2;
        cy = h / 2;
    }
    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        if (!userMoved) Fit();
    }

    PointF S(double x, double y) {
        var p = ToImage(x, y);
        return new PointF((float)((p.X - cx) * zoom + Width / 2.0), (float)((p.Y - cy) * zoom + Height / 2.0));
    }

    internal void CenterOn(DungeonLocation d) {
        var p = ToImage(d.X, d.Y);
        cx = p.X;
        cy = p.Y;
        userMoved = true;
        Invalidate();
    }
    // Scrolls to the entrance only when it is not already comfortably on screen.
    internal void Reveal(DungeonLocation d) {
        var p = S(d.X, d.Y);
        if (p.X < 40 || p.Y < 40 || p.X > Width - 40 || p.Y > Height - 40) CenterOn(d);
    }

    DungeonLocation HitTest(Point point) {
        DungeonLocation best = null;
        double nearest = 15 * 15;
        foreach (var d in sites) {
            var p = S(d.X, d.Y);
            double distance = (p.X - point.X) * (p.X - point.X) + (p.Y - point.Y) * (p.Y - point.Y);
            if (distance <= nearest) { nearest = distance; best = d; }
        }
        return best;
    }

    // White text with a dark outline stays readable on both the artwork and the dark background.
    static void Label(Graphics g, string text, Font font, Brush brush, float x, float y) {
        foreach (var o in new[] { new PointF(-1, 0), new PointF(1, 0), new PointF(0, -1), new PointF(0, 1) })
            g.DrawString(text, font, Brushes.Black, x + o.X, y + o.Y);
        g.DrawString(text, font, brush, x, y);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var small = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (var big = new Font("Segoe UI", 10.5f, FontStyle.Bold)) {
            if (baseMap != null) {
                g.InterpolationMode = dragging ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float left = (float)((0 - cx) * zoom + Width / 2.0), top = (float)((0 - cy) * zoom + Height / 2.0);
                g.DrawImage(baseMap, new RectangleF(left, top, (float)(baseMap.Width * zoom), (float)(baseMap.Height * zoom)));            } else {
                Label(g, "Harita resmi bulunamadı.", big, Brushes.White, 20, 20);
            }
            foreach (var town in Towns) {
                var gate = ExpeditionRules.Network.Gates.FirstOrDefault(x => x.Id == town.Key);
                if (gate == null) continue;
                var p = S(gate.X, gate.Y);
                var diamond = new[] { new PointF(p.X, p.Y - 8), new PointF(p.X + 8, p.Y), new PointF(p.X, p.Y + 8), new PointF(p.X - 8, p.Y) };
                g.FillPolygon(Brushes.DodgerBlue, diamond);
                g.DrawPolygon(Pens.White, diamond);
                Label(g, town.Value, small, Brushes.White, p.X + 10, p.Y - 8);
            }
            foreach (var d in sites) {
                var p = S(d.X, d.Y);
                bool selected = d.Code == Selected, visited = Visited != null && Visited(d.Code);
                if (selected) using (var ring = new Pen(Color.Gold, 3)) g.DrawEllipse(ring, p.X - 13, p.Y - 13, 26, 26);
                g.FillEllipse(visited ? Brushes.SeaGreen : Brushes.OrangeRed, p.X - 7, p.Y - 7, 14, 14);
                g.DrawEllipse(Pens.White, p.X - 7, p.Y - 7, 14, 14);
                Label(g, d.Code, selected ? big : small, selected ? Brushes.Gold : Brushes.White, p.X + 10, p.Y - (selected ? 22 : 18));
                if (selected || zoom > 0.55) Label(g, d.Name, small, Brushes.White, p.X + 10, p.Y + 4);
            }
            if (Player != null && Player.Outside) {
                var p = S(Player.X, Player.Y);
                g.FillEllipse(Brushes.Cyan, p.X - 6, p.Y - 6, 12, 12);
                g.DrawEllipse(Pens.Black, p.X - 6, p.Y - 6, 12, 12);
                Label(g, "Sen", small, Brushes.Cyan, p.X + 8, p.Y - 8);
            }
            const string legend = "Kırmızı: giriş · Yeşil: girildi · Altın halka: seçili · Mavi: şehir · Turkuaz: karakter\nFare tekerleği: yakınlaştır · Sürükle: kaydır · Çift tık: sığdır · Noktaya tıkla: dungeon seç";
            var legendSize = g.MeasureString(legend, small);
            float legendTop = Height - legendSize.Height - 14;
            using (var back = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
                g.FillRectangle(back, 6, legendTop - 4, legendSize.Width + 8, legendSize.Height + 8);
            g.DrawString(legend, small, Brushes.White, 10, legendTop);        }
    }

    protected override void OnMouseWheel(MouseEventArgs e) {
        double wx = cx + (e.X - Width / 2.0) / zoom, wy = cy + (e.Y - Height / 2.0) / zoom;
        zoom = Math.Max(0.1, Math.Min(4.0, zoom * (e.Delta > 0 ? 1.25 : 0.8)));
        cx = wx - (e.X - Width / 2.0) / zoom;
        cy = wy - (e.Y - Height / 2.0) / zoom;
        userMoved = true;
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        dragging = true;
        moved = false;
        dragStart = e.Location;
        dragCx = cx;
        dragCy = cy;
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (!dragging) { Cursor = HitTest(e.Location) != null ? Cursors.Hand : Cursors.Default; return; }
        int dx = e.X - dragStart.X, dy = e.Y - dragStart.Y;
        if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3) moved = true;
        if (!moved) return;
        cx = dragCx - dx / zoom;
        cy = dragCy - dy / zoom;
        userMoved = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        bool click = dragging && !moved;
        dragging = false;
        if (!click) { Invalidate(); return; }
        var hit = HitTest(e.Location);
        if (hit != null && DungeonPicked != null) DungeonPicked(hit.Code);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e) {
        base.OnMouseDoubleClick(e);
        userMoved = false;
        Fit();
        Invalidate();
    }

    protected override void Dispose(bool disposing) {
        if (disposing && baseMap != null) { baseMap.Dispose(); baseMap = null; }
        base.Dispose(disposing);
    }
}

sealed partial class MemorySelectV3Form {
    readonly WorldMapView worldMap = new WorldMapView();
    TabPage worldTab;
    bool pickingFromMap;

    void BuildWorldMap() {
        worldTab = new TabPage("Dünya haritası");
        worldMap.Dock = DockStyle.Fill;
        worldTab.Controls.Add(worldMap);
        grimViews.TabPages.Add(worldTab);
        worldMap.Sites = V3Data.Dungeons;
        worldMap.Visited = code => visitedDungeons.Any(id => id.StartsWith(code + "|", StringComparison.Ordinal));
        worldMap.DungeonPicked += PickDungeonFromMap;
        grimViews.SelectedIndexChanged += delegate { if (grimViews.SelectedTab == worldTab) SyncWorldMap(); };
    }

    void SyncWorldMap() {
        if (worldTab == null) return;
        var d = SelectedDungeon();
        worldMap.Selected = d == null ? null : d.Code;
        if (d != null && !pickingFromMap) worldMap.Reveal(d);
        worldMap.Invalidate();
    }

    // Clicking a marker selects the grid row without moving the map under the cursor.
    void PickDungeonFromMap(string code) {
        foreach (DataGridViewRow row in dungeonGrid.Rows) {
            var location = row.Tag as DungeonLocation;
            if (location == null || location.Code != code) continue;
            pickingFromMap = true;
            try { dungeonGrid.CurrentCell = row.Cells[1]; } finally { pickingFromMap = false; }
            return;
        }
    }
}
