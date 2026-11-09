using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Dungeon queue: the player picks Solo Trial dungeons in the order they should run, and the bot walks to
// each entrance, enters, clears it and moves on to the next one once the character is back outside.
// Leaving a finished dungeon is not automated by the game command layer (there is no exit link in the transit
// data), so the player clicks the Dungeon Exit NPC, or optionally the bot uses a Return Scroll.
sealed partial class MemorySelectV3Form {
    readonly List<DungeonLocation> queue = new List<DungeonLocation>();
    readonly ListBox queueList = new ListBox();
    readonly CheckBox queueReturnScroll = new CheckBox();
    readonly Label queueInfo = new Label();
    bool queueRunning, queueAdvancing, queueReturnUsed;
    int queueIndex;

    internal List<string> QueueOrder() { return queue.Select(d => d.Code).ToList(); }

    void BuildQueuePanel(Control host) {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 276, Padding = new Padding(6) };
        var title = new Label { Text = "Çalıştırma sırası · Solo Trial · seçtiğin sırayla çalışır", Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleLeft };
        queueInfo.Dock = DockStyle.Top;
        queueInfo.Height = 40;
        queueInfo.Text = "Sıra boş. Tablodan bir dungeon seçip Ekle / Çıkar'a bas; tabloda çift tık da çalışır.";
        queueList.Dock = DockStyle.Fill;
        queueList.IntegralHeight = false;
        queueList.HorizontalScrollbar = true;
        queueList.DoubleClick += delegate { Safe(RemoveSelectedFromQueue); };
        var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 190, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        side.Controls.Add(Btn("Ekle / Çıkar", ToggleSelectedInQueue, 170));
        var arrows = new FlowLayoutPanel { Width = 176, Height = 38, WrapContents = false, Margin = new Padding(0) };
        arrows.Controls.Add(Btn("▲", delegate { MoveQueue(-1); }, 80));
        arrows.Controls.Add(Btn("▼", delegate { MoveQueue(1); }, 80));
        side.Controls.Add(arrows);
        side.Controls.Add(Btn("Temizle", ClearQueue, 170));
        side.Controls.Add(Btn("Sırayı başlat", StartQueue, 170));
        queueReturnScroll.Text = "Bitince Return Scroll ile çık (denenmedi)";
        queueReturnScroll.AutoSize = false;
        queueReturnScroll.Width = 176;
        queueReturnScroll.Height = 46;
        queueReturnScroll.CheckedChanged += delegate { SaveSettings(); };
        locked.Add(queueReturnScroll);
        side.Controls.Add(queueReturnScroll);
        panel.Controls.Add(queueList);
        panel.Controls.Add(side);
        panel.Controls.Add(queueInfo);
        panel.Controls.Add(title);
        host.Controls.Add(panel);
        dungeonGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex < 0 || e.ColumnIndex == 0) return;
            var location = dungeonGrid.Rows[e.RowIndex].Tag as DungeonLocation;
            if (location != null) Safe(delegate { ToggleQueue(location); });
        };
    }
    void ToggleSelectedInQueue() {
        var d = SelectedDungeon();
        if (d == null) throw new InvalidOperationException("Dungeon seç.");
        ToggleQueue(d);
    }

    void ToggleQueue(DungeonLocation d) {
        if (d.Mode != "Solo Trial") throw new InvalidOperationException("Sıra yalnızca Solo Trial içindir. Solo Trial'ı işaretle.");
        if (queueRunning) throw new InvalidOperationException("Sıra çalışırken değiştirilemez. Önce durdur.");
        var existing = queue.FirstOrDefault(x => x.Code == d.Code);
        if (existing != null) queue.Remove(existing); else queue.Add(d);
        QueueChanged();
    }

    void RemoveSelectedFromQueue() {
        if (queueRunning) throw new InvalidOperationException("Sıra çalışırken değiştirilemez. Önce durdur.");
        int i = queueList.SelectedIndex;
        if (i < 0 || i >= queue.Count) return;
        queue.RemoveAt(i);
        QueueChanged();
    }

    void MoveQueue(int delta) {
        if (queueRunning) throw new InvalidOperationException("Sıra çalışırken değiştirilemez. Önce durdur.");
        int i = queueList.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= queue.Count) return;
        var item = queue[i];
        queue[i] = queue[j];
        queue[j] = item;
        QueueChanged();
        queueList.SelectedIndex = j;
    }

    void ClearQueue() {
        if (queueRunning) throw new InvalidOperationException("Sıra çalışırken değiştirilemez. Önce durdur.");
        queue.Clear();
        QueueChanged();
    }

    void RestoreQueue() {
        queue.Clear();
        foreach (string code in settings.DungeonQueue ?? new List<string>()) {
            var d = V3Data.Dungeons.FirstOrDefault(x => x.Code == code && x.Mode == "Solo Trial");
            if (d != null && queue.All(q => q.Code != code)) queue.Add(d);
        }
        queueReturnScroll.Checked = settings.QueueReturnScroll;
        UpdateQueueUi();
    }

    void QueueChanged() {
        UpdateQueueUi();
        SaveSettings();
    }

    void UpdateQueueUi() {
        int selected = queueList.SelectedIndex;
        queueList.BeginUpdate();
        queueList.Items.Clear();
        for (int i = 0; i < queue.Count; i++)
            queueList.Items.Add((queueRunning && i == queueIndex ? "▶ " : "   ") + (i + 1) + " · " + queue[i].Code + " · " + queue[i].Name);
        queueList.EndUpdate();
        if (selected >= 0 && selected < queueList.Items.Count) queueList.SelectedIndex = selected;
        if (queue.Count == 0) queueInfo.Text = "Sıra boş. Tablodan bir dungeon seçip Ekle / Çıkar'a bas; tabloda çift tık da çalışır.";
        else if (queueRunning) queueInfo.Text = "Çalışıyor " + (queueIndex + 1) + "/" + queue.Count + " · " + string.Join(" → ", queue.Select(d => d.Code));
        else queueInfo.Text = "Hazır: " + string.Join(" → ", queue.Select(d => d.Code)) + " · Sırayı dungeon dışından başlat.";
        worldMap.Invalidate();
    }

    void SelectDungeonRow(DungeonLocation d) {
        foreach (DataGridViewRow row in dungeonGrid.Rows) {
            var location = row.Tag as DungeonLocation;
            if (location != null && location.Code == d.Code && location.Mode == d.Mode) { dungeonGrid.CurrentCell = row.Cells[1]; return; }
        }
    }

    void StartQueue() {
        RequireReader();
        if (queue.Count == 0) throw new InvalidOperationException("Sıra boş. Önce dungeon ekle.");
        if (!autoEnterGrim.Checked) { autoEnterGrim.Checked = true; Log("Sıra için otomatik giriş açıldı."); }
        snapshot = reader.Capture();
        if (V3Data.IsCrypt(snapshot.Player)) throw new InvalidOperationException("Sırayı dungeon dışından başlat.");
        queueRunning = true;
        queueIndex = 0;
        Log("Sıra başladı: " + string.Join(" → ", queue.Select(d => d.Code)));
        BeginQueueEntry();
    }

    void BeginQueueEntry() {
        var d = queue[queueIndex];
        queueReturnUsed = false;
        SelectDungeonRow(d);
        queueAdvancing = true;
        try { WalkDungeon(d); }
        catch (Exception) { queueRunning = false; throw; }
        finally { queueAdvancing = false; UpdateQueueUi(); }
        Log("Sıra " + (queueIndex + 1) + "/" + queue.Count + " · " + d);
    }

    void MarkVisited(DungeonLocation d) {
        foreach (DataGridViewRow row in dungeonGrid.Rows) {
            var location = row.Tag as DungeonLocation;
            if (location != null && location.Code == d.Code && location.Mode == d.Mode) { row.Cells[0].Value = true; return; }
        }
        visitedDungeons.Add(d.Code + "|" + d.Mode);
        SaveSettings();
    }

    // Called every tick while the bot is inside a Grim run. Returns true when the queue handled this tick.
    bool QueueAfterComplete(long now) {
        if (!queueRunning || queueAdvancing || queueIndex >= queue.Count) return false;
        var d = queue[queueIndex];
        if (grim.Location == null || grim.Location.Code != d.Code || !grim.Complete) return false;
        if (!grim.IsRegion(snapshot.Player)) {
            // The dungeon is finished and the character is back outside: go on with the next entry.
            MarkVisited(d);
            Log("Sıra " + (queueIndex + 1) + "/" + queue.Count + " bitti · " + d.Code);
            queueIndex++;
            if (queueIndex >= queue.Count) {
                queueRunning = false;
                UpdateQueueUi();
                Stop("Sıra tamamlandı: " + queue.Count + " dungeon.");
                return true;
            }
            try { BeginQueueEntry(); }
            catch (Exception e) { queueRunning = false; Stop(e.Message); }
            return true;
        }
        if (!exitHolding) return false;
        if (queueReturnScroll.Checked && !queueReturnUsed) {
            queueReturnUsed = true;
            var item = snapshot.Items.Where(i => ExpeditionRules.ReturnIds.Contains(i.Model) && i.Quantity > 0 && i.Type != 0 && i.Slot >= 13).OrderBy(i => i.Slot).FirstOrDefault();
            if (item == null || !reader.ValidateItem(item)) { Log("Sıra: kullanılabilir Return Scroll yok; Dungeon Exit'e elle tıkla."); return true; }
            if (!sender.UseReturn(item)) { Log("Sıra: Return Scroll isteği reddedildi; Dungeon Exit'e elle tıkla."); return true; }
            Log("Sıra: Return Scroll ile çıkış istendi · ID " + item.Model);
            return true;
        }
        status.Text = "Sıra " + (queueIndex + 1) + "/" + queue.Count + " bitti · Dungeon Exit'e tıkla; çıkınca sıradaki dungeon otomatik başlar.";
        return true;
    }
}
