using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// World map: the Explorer artwork with the dungeon entrances placed at their world coordinates.
sealed class WorldMapView : Control {
    // The artwork is a uniform scaling of the game world: pixel = Origin + world * PxPerUnit (Y axis flipped).
    // Fitted on Hotan and Jangan, then checked against the road data (town plateaus, ruins and cliffs line up).
    internal const double PxPerUnit = 0.184, OriginX = 2248.06, OriginY = 662.82;
    internal const string ResourceName = "explorer-52.jpg";
    static readonly KeyValuePair<int, string>[] Towns = {
        new KeyValuePair<int, string>(20, "Constantinople"), new KeyValuePair<int, string>(25, "Samarkand"),
        new KeyValuePair<int, string>(5, "Hotan"), new KeyValuePair<int, string>(2, "Donwhang"),
        new KeyValuePair<int, string>(1, "Jangan"), new KeyValuePair<int, string>(252, "Nanzhang")
    };

    Bitmap baseMap;
    bool userMoved, dragging, moved;
    double zoom = 0.07, cx = -2400, cy = 0, dragCx, dragCy;
    Point dragStart;
    List<DungeonLocation> sites = new List<DungeonLocation>();

    internal Position Player;
    internal string Selected;
    internal Func<string, bool> Visited;
    internal event Action<string> DungeonPicked;
    internal bool Ready { get { return baseMap != null; } }

    internal static PointF ToImage(double x, double y) {
        return new PointF((float)(OriginX + x * PxPerUnit), (float)(OriginY - y * PxPerUnit));
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
        Invalidate();
    }

    void Fit() {
        if (Width < 50 || Height < 50) return;
        // Artwork plus a margin: about X -12700..7900 and Y -3700..3700.
        zoom = Math.Min((Width - 30) / 20600.0, (Height - 30) / 7400.0);
        cx = -2400;
        cy = 0;
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        if (!userMoved) Fit();
    }

    PointF S(double x, double y) {
        return new PointF((float)((x - cx) * zoom + Width / 2.0), (float)((cy - y) * zoom + Height / 2.0));
    }

    internal void CenterOn(DungeonLocation d) {
        cx = d.X;
        cy = d.Y;
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
                double x0 = (0 - OriginX) / PxPerUnit, x1 = (baseMap.Width - OriginX) / PxPerUnit;
                double y0 = (OriginY - 0) / PxPerUnit, y1 = (OriginY - baseMap.Height) / PxPerUnit;
                var a = S(x0, y0);
                var b = S(x1, y1);
                g.InterpolationMode = dragging ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(baseMap, new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y));
            } else {
                Label(g, "Harita resmi bulunamadı.", big, Brushes.White, 20, 60);
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
                if (selected || zoom > 0.12) Label(g, d.Name, small, Brushes.White, p.X + 10, p.Y + 4);
            }
            if (Player != null && Player.Outside) {
                var p = S(Player.X, Player.Y);
                g.FillEllipse(Brushes.Cyan, p.X - 6, p.Y - 6, 12, 12);
                g.DrawEllipse(Pens.Black, p.X - 6, p.Y - 6, 12, 12);
                Label(g, "Sen", small, Brushes.Cyan, p.X + 8, p.Y - 8);
            }
            using (var back = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                g.FillRectangle(back, 6, 6, 560, 38);
            g.DrawString("Kırmızı: giriş · Yeşil: girildi · Altın halka: seçili · Mavi: şehir · Turkuaz: karakter\nFare tekerleği: yakınlaştır · Sürükle: kaydır · Çift tık: sığdır · Noktaya tıkla: dungeon seç", small, Brushes.White, 10, 8);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e) {
        double wx = cx + (e.X - Width / 2.0) / zoom, wy = cy - (e.Y - Height / 2.0) / zoom;
        zoom = Math.Max(0.015, Math.Min(1.5, zoom * (e.Delta > 0 ? 1.25 : 0.8)));
        cx = wx - (e.X - Width / 2.0) / zoom;
        cy = wy + (e.Y - Height / 2.0) / zoom;
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
        cy = dragCy + dy / zoom;
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
