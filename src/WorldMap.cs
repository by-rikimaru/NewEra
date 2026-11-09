using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

// World map drawn from the walkable road data, so every dungeon entrance sits at its exact world coordinate.
sealed class WorldMapView : Control {
    internal const double MinX = -13500, MaxX = 8500, MinY = -4200, MaxY = 4000, BaseScale = 0.15;
    static readonly KeyValuePair<int, string>[] Towns = {
        new KeyValuePair<int, string>(20, "Constantinople"), new KeyValuePair<int, string>(25, "Samarkand"),
        new KeyValuePair<int, string>(5, "Hotan"), new KeyValuePair<int, string>(2, "Donwhang"),
        new KeyValuePair<int, string>(1, "Jangan"), new KeyValuePair<int, string>(252, "Nanzhang")
    };

    Bitmap baseMap;
    bool rendering, userMoved, dragging, moved;
    string renderError;
    double zoom = 0.04, cx = -2500, cy = -100, dragCx, dragCy;
    Point dragStart;
    List<DungeonLocation> sites = new List<DungeonLocation>();

    internal Position Player;
    internal string Selected;
    internal Func<string, bool> Visited;
    internal event Action<string> DungeonPicked;
    internal bool Ready { get { return baseMap != null; } }

    internal IEnumerable<DungeonLocation> Sites {
        set { sites = value.GroupBy(d => d.Code).Select(g => g.First()).OrderBy(d => d.Code).ToList(); Invalidate(); }
    }

    internal WorldMapView() {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        VisibleChanged += delegate { if (Visible) EnsureBase(); };
    }

    internal void EnsureBase() {
        if (baseMap != null || rendering || !IsHandleCreated) return;
        rendering = true;
        ThreadPool.QueueUserWorkItem(delegate {
            Bitmap bitmap = null;
            string error = null;
            try {
                bitmap = ExpeditionRules.RenderWorld(MinX, MaxY, (int)((MaxX - MinX) * BaseScale), (int)((MaxY - MinY) * BaseScale), BaseScale);
            } catch (Exception e) { error = e.Message; }
            try {
                BeginInvoke((Action)delegate {
                    baseMap = bitmap;
                    renderError = error;
                    rendering = false;
                    Invalidate();
                });
            } catch (Exception) { if (bitmap != null) bitmap.Dispose(); }
        });
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        if (Visible) EnsureBase();
    }

    void Fit() {
        if (Width < 50 || Height < 50) return;
        // Walkable land spans about X -12900..7900 and Y -3500..3700.
        zoom = Math.Min((Width - 30) / 21200.0, (Height - 30) / 7500.0);
        cx = -2500;
        cy = 100;
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
        zoom = Math.Max(zoom, 0.06);
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

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var text = new SolidBrush(ForeColor))
        using (var small = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (var big = new Font("Segoe UI", 10.5f, FontStyle.Bold)) {
            if (baseMap != null) {
                var a = S(MinX, MaxY);
                var b = S(MaxX, MinY);
                g.InterpolationMode = zoom * 100 >= BaseScale * 100 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(baseMap, new RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y));
            } else {
                string message = renderError != null ? "Harita çizilemedi: " + renderError : "Harita hazırlanıyor…";
                g.DrawString(message, big, text, 20, 40);
            }
            foreach (var town in Towns) {
                var gate = ExpeditionRules.Network.Gates.FirstOrDefault(x => x.Id == town.Key);
                if (gate == null) continue;
                var p = S(gate.X, gate.Y);
                var diamond = new[] { new PointF(p.X, p.Y - 7), new PointF(p.X + 7, p.Y), new PointF(p.X, p.Y + 7), new PointF(p.X - 7, p.Y) };
                g.FillPolygon(Brushes.DodgerBlue, diamond);
                g.DrawPolygon(Pens.White, diamond);
                g.DrawString(town.Value, small, text, p.X + 9, p.Y - 8);
            }
            foreach (var d in sites) {
                var p = S(d.X, d.Y);
                bool selected = d.Code == Selected, visited = Visited != null && Visited(d.Code);
                if (selected) using (var ring = new Pen(Color.Gold, 3)) g.DrawEllipse(ring, p.X - 12, p.Y - 12, 24, 24);
                g.FillEllipse(visited ? Brushes.SeaGreen : Brushes.OrangeRed, p.X - 7, p.Y - 7, 14, 14);
                g.DrawEllipse(Pens.White, p.X - 7, p.Y - 7, 14, 14);
                g.DrawString(d.Code, selected ? big : small, selected ? Brushes.Gold : text, p.X + 10, p.Y - (selected ? 22 : 18));
                if (selected || zoom > 0.12) g.DrawString(d.Name, small, text, p.X + 10, p.Y + 4);
            }
            if (Player != null && Player.Outside) {
                var p = S(Player.X, Player.Y);
                g.FillEllipse(Brushes.Cyan, p.X - 6, p.Y - 6, 12, 12);
                g.DrawEllipse(Pens.Black, p.X - 6, p.Y - 6, 12, 12);
                g.DrawString("Sen", small, Brushes.Cyan, p.X + 8, p.Y - 8);
            }
            g.DrawString("Kırmızı: giriş · Yeşil: girildi · Altın halka: seçili · Mavi: şehir · Turkuaz: karakter\nFare tekerleği: yakınlaştır · Sürükle: kaydır · Çift tık: sığdır · Noktaya tıkla: dungeon seç", small, text, 10, 8);
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
        if (!click) return;
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
        if (d != null) worldMap.Reveal(d);
        worldMap.Invalidate();
    }

    void PickDungeonFromMap(string code) {
        foreach (DataGridViewRow row in dungeonGrid.Rows) {
            var location = row.Tag as DungeonLocation;
            if (location != null && location.Code == code) { dungeonGrid.CurrentCell = row.Cells[1]; return; }
        }
    }
}
