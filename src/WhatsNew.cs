using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

// "Yenilikler" window shown once after the program was updated. The notes are kept in the program itself.
static class ReleaseNotes {
    internal sealed class Entry { internal string Version; internal string Title; internal string[] Items; }

    internal static readonly Entry[] All = {
        new Entry { Version = "3.4.4.0", Title = "Dungeon sırası ve karakter paneli", Items = new[] {
            "Dungeon çalıştırma sırası: dungeonları seçtiğin sırayla otomatik çalıştırır (Ekle/Çıkar, ▲▼, haritada Shift+tık).",
            "Her dungeon bitince Dungeon Exit NPC'si ile otomatik çıkış; sıra bitince istersen Return Scroll ile şehre dönüş.",
            "Dünya haritası: oyunun kendi harita karolarıyla birebir koordinat, yakınlaştırma ve sıra çizgisi.",
            "Karakter durumu paneli: HP (kırmızı), MP (mavi), EXP (yeşil), SP (sarı) canlı çubukları.",
            "Büyük seviye göstergesi, oturumda atlanan seviye sayısı, dakikalık ve saatlik EXP/SP kazancı, seviyeye kalan süre.",
            "Bu \"Yenilikler\" penceresi: güncellemeden sonra değişiklikleri bir kez gösterir." } },
    };

    internal static string CurrentVersion {
        get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
    }

    internal static Entry Find(string version) {
        foreach (var e in All) if (e.Version == version) return e;
        return null;
    }
}

sealed class WhatsNewForm : Form {
    internal bool DontShowAgain { get { return hide.Checked; } }
    readonly CheckBox hide = new CheckBox();

    internal WhatsNewForm(ReleaseNotes.Entry entry, string version, Icon icon, Color back, Color fore) {
        Text = "Yenilikler · " + version;
        if (icon != null) Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        ClientSize = new Size(560, 150 + entry.Items.Length * 44);
        BackColor = back; ForeColor = fore;
        Font = new Font("Segoe UI", 10f);

        var head = new Label { Text = "Güncellendi: sürüm " + version, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(255, 205, 80), Left = 18, Top = 14, Width = 520, Height = 32 };
        var sub = new Label { Text = entry.Title, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Left = 20, Top = 48, Width = 520, Height = 22 };
        var list = new Label { Left = 20, Top = 78, Width = 520, Height = entry.Items.Length * 44 };
        list.Text = "• " + string.Join("\r\n• ", entry.Items);
        hide.Text = "Bir daha gösterme";
        hide.AutoSize = true; hide.Left = 20; hide.Top = ClientSize.Height - 44;
        var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Width = 110, Height = 30, Left = 430, Top = ClientSize.Height - 48, FlatStyle = FlatStyle.System };
        AcceptButton = ok;
        Controls.AddRange(new Control[] { head, sub, list, hide, ok });
    }
}

sealed partial class MemorySelectV3Form {
    // Shown once per new version, unless the user ticked "Bir daha gösterme".
    void ShowWhatsNew() {
        try {
            string version = ReleaseNotes.CurrentVersion;
            if (settings.HideWhatsNew || settings.LastSeenVersion == version) return;
            var entry = ReleaseNotes.Find(version);
            settings.LastSeenVersion = version;
            if (entry != null) {
                using (var form = new WhatsNewForm(entry, version, Icon, BackColor, ForeColor)) {
                    form.ShowDialog(this);
                    if (form.DontShowAgain) settings.HideWhatsNew = true;
                }
            }
            settings.Save();
        } catch { }
    }
}
