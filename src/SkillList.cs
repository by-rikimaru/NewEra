using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// One hotbar key in the skill list. Kind decides when it is pressed; Target, MinMobs, Seconds and Percent refine it.
sealed class SkillRow {
 public bool On = true;
 public string Name = "", Key = "", Kind = SkillRules.Attack, Target = SkillRules.All;
 public uint SkillId;
 public int MinMobs, Seconds, Percent = 50;
 internal SkillRow Copy() { return (SkillRow)MemberwiseClone(); }
 internal string StateKey { get { return Kind + "|" + Key + "|" + SkillId + "|" + Name; } }
 internal ushort Vk { get { return HuntCare.KeyOf(Key); } }
}

sealed class SkillProfile { public string Name = ""; public List<SkillRow> Rows = new List<SkillRow>(); }

static class SkillRules {
 internal const string Attack = "Saldırı", Buff = "Buff", Emergency = "Acil buff", Heal = "İyileştirme", Mana = "Mana";
 internal const string All = "Hepsi";
 internal static readonly string[] Kinds = { Attack, Buff, Emergency, Heal, Mana };
 internal static readonly string[] Targets = { All, "Normal", "Champion", "Giant", "Unique", "Diğer" };
 internal const double NearRange = 20;

 internal static bool TargetMatches(string target, int rank) {
  switch (target) {
   case All: return true;
   case "Normal": return rank == 0 || rank == 2;
   case "Champion": return rank == 1;
   case "Unique": return rank == 3;
   case "Giant": return rank == 4 || rank == 5;
   default: return rank != 0 && rank != 1 && rank != 2 && rank != 3 && rank != 4 && rank != 5;
  }
 }

 internal static int MobsNear(Snapshot s) {
  if (s == null || s.Player == null) return 0;
  return s.Monsters.Count(m => m.Alive && !m.Party && m.Position != null && m.Position.Distance(s.Player) <= NearRange);
 }

 internal static int HpPercent(Snapshot s) { return s.StatsKnown && s.MaxHp > 0 ? (int)((long)s.Hp * 100 / s.MaxHp) : 100; }
 internal static int MpPercent(Snapshot s) { return s.StatsKnown && s.MaxMp > 0 ? (int)((long)s.Mp * 100 / s.MaxMp) : 100; }

 // Ready-made rows the user can append and then adjust to their own hotbar.
 internal static readonly Dictionary<string, SkillRow[]> Templates = new Dictionary<string, SkillRow[]> {
  { "Temel saldırı (1-2-3)", new[] {
   new SkillRow { Name = "Saldırı 1", Key = "1" }, new SkillRow { Name = "Saldırı 2", Key = "2" }, new SkillRow { Name = "Saldırı 3", Key = "3" } } },
  { "Kalabalığa alan saldırısı", new[] {
   new SkillRow { Name = "Alan saldırısı", Key = "4", MinMobs = 3, Seconds = 8 } } },
  { "Güçlü yaratığa özel skill", new[] {
   new SkillRow { Name = "Giant skill", Key = "5", Target = "Giant", Seconds = 15 }, new SkillRow { Name = "Unique skill", Key = "5", Target = "Unique", Seconds = 15 } } },
  { "Pot ve iyileştirme", new[] {
   new SkillRow { Name = "İyileştirme", Key = "7", Kind = Heal, Percent = 50, Seconds = 3 }, new SkillRow { Name = "Mana", Key = "8", Kind = Mana, Percent = 40, Seconds = 3 } } },
  { "Savunma buff seti", new[] {
   new SkillRow { Name = "Savunma buff", Key = "9", Kind = Buff, Seconds = 120 }, new SkillRow { Name = "Kalkan buff", Key = "0", Kind = Buff, Seconds = 120 },
   new SkillRow { Name = "Acil savunma", Key = "6", Kind = Emergency, Percent = 35, MinMobs = 4, Seconds = 30 } } },
  { "Destek (Cleric / Bard)", new[] {
   new SkillRow { Name = "İyileştirme", Key = "1", Kind = Heal, Percent = 70, Seconds = 2 }, new SkillRow { Name = "Buff 1", Key = "2", Kind = Buff, Seconds = 120 },
   new SkillRow { Name = "Buff 2", Key = "3", Kind = Buff, Seconds = 120 }, new SkillRow { Name = "Mana", Key = "8", Kind = Mana, Percent = 40, Seconds = 3 } } },
 };
}

// Decides which skill key to press. Keeps per-row cooldowns and the attack rotation.
sealed class SkillPlanner {
 internal bool BuffWhileAttacking = true, RestartOnTarget = true, FallbackAll = true, SupportOnly;
 readonly Dictionary<string, long> last = new Dictionary<string, long>();
 int attackIndex;
 uint lastTarget;
 const long BuffRetryMs = 3000, MinCareMs = 1500;

 internal void Reset() { last.Clear(); attackIndex = 0; lastTarget = 0; }
 // Forget when buffs were pressed so timed buffs are cast again (after revive or a teleport).
 internal void ResetBuffs() { foreach (var k in last.Keys.Where(k => k.StartsWith(SkillRules.Buff + "|") || k.StartsWith(SkillRules.Emergency + "|")).ToList()) last.Remove(k); }
 internal void Mark(SkillRow row, long now) { last[row.StateKey] = now; }

 bool Pressed(SkillRow row, out long at) { return last.TryGetValue(row.StateKey, out at); }
 bool Ready(SkillRow row, long now, long coolMs) { long at; return !Pressed(row, out at) || now >= at + coolMs; }
 static long Cool(SkillRow r) { return Math.Max(0, r.Seconds) * 1000L; }
 static IEnumerable<SkillRow> Usable(IEnumerable<SkillRow> rows, string kind) { return rows.Where(r => r != null && r.On && r.Kind == kind && r.Vk != 0); }

 // Emergency, healing, mana and buffs, in that order. Null when nothing is due.
 internal SkillRow Support(IList<SkillRow> rows, Snapshot s, int mobsNear, bool attacking, bool allowBuffs, long now) {
  if (s == null || !s.PlayerAlive) return null;
  int hp = SkillRules.HpPercent(s), mp = SkillRules.MpPercent(s);
  foreach (var r in Usable(rows, SkillRules.Emergency)) {
   bool trigger = (r.Percent > 0 && s.StatsKnown && hp < r.Percent) || (r.MinMobs > 0 && mobsNear >= r.MinMobs);
   if (!trigger) continue;
   if (r.SkillId != 0 && s.BuffsKnown && s.BuffIds.Contains(r.SkillId)) continue;
   if (Ready(r, now, Math.Max(Cool(r), MinCareMs))) return r;
  }
  foreach (var r in Usable(rows, SkillRules.Heal))
   if (r.Percent > 0 && s.StatsKnown && hp < r.Percent && Ready(r, now, Math.Max(Cool(r), MinCareMs))) return r;
  foreach (var r in Usable(rows, SkillRules.Mana))
   if (r.Percent > 0 && s.StatsKnown && mp < r.Percent && Ready(r, now, Math.Max(Cool(r), MinCareMs))) return r;
  if (!allowBuffs || (attacking && !BuffWhileAttacking)) return null;
  foreach (var r in Usable(rows, SkillRules.Buff)) {
   if (r.MinMobs > 0 && mobsNear < r.MinMobs) continue;
   if (r.SkillId != 0 && s.BuffsKnown) {
    // The character's real buff list decides: cast only when the buff is gone, retry after a short pause.
    if (!s.BuffIds.Contains(r.SkillId) && Ready(r, now, BuffRetryMs)) return r;
    continue;
   }
   long at;
   if (!Pressed(r, out at)) return r;
   if (r.Seconds > 0 && now >= at + Cool(r)) return r;
  }
  return null;
 }

 // Next attack key for this target: skills for the target's class first, then the "Hepsi" list.
 internal SkillRow Attack(IList<SkillRow> rows, Monster target, int mobsNear, long now) {
  if (SupportOnly || target == null) return null;
  if (target.Uid != lastTarget) { lastTarget = target.Uid; if (RestartOnTarget) attackIndex = 0; }
  var attacks = Usable(rows, SkillRules.Attack).Where(r => mobsNear >= r.MinMobs).ToList();
  var specific = attacks.Where(r => r.Target != SkillRules.All && SkillRules.TargetMatches(r.Target, target.Rank)).ToList();
  var pool = specific.Where(r => Ready(r, now, Cool(r))).ToList();
  if (pool.Count == 0 && (specific.Count == 0 || FallbackAll))
   pool = attacks.Where(r => r.Target == SkillRules.All && Ready(r, now, Cool(r))).ToList();
  if (pool.Count == 0) return null;
  var pick = pool[attackIndex % pool.Count];
  attackIndex++;
  return pick;
 }
}

sealed partial class MemorySelectV3Form {
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
 const uint WmKeyDown = 0x100, WmKeyUp = 0x101;

 readonly CheckBox skillsEnabled = new CheckBox(), skillsBackground = new CheckBox(), skillsBuffAttack = new CheckBox(), skillsRebuff = new CheckBox(),
  skillsRestart = new CheckBox(), skillsFallback = new CheckBox(), skillsSupport = new CheckBox();
 readonly DataGridView skillGrid = new DataGridView();
 readonly ComboBox skillProfileBox = new ComboBox(), skillTemplateBox = new ComboBox();
 readonly SkillPlanner planner = new SkillPlanner();
 List<SkillRow> skillRows = new List<SkillRow>();
 bool skillsBuilt, fillingSkills, skillWasAlive = true, heldPosted;
 Position skillLastPosition;
 IntPtr gameWindow; int gameWindowPid;
 Timer backgroundProbe;

 bool SkillsActive { get { return skillsEnabled.Checked; } }
 // Automation may act while the game is behind other windows only when background keys are on.
 bool CanAct() { return reader != null && (reader.IsForeground() || skillsBackground.Checked); }

 // Keys used for the Start checks: attack keys, or every key in support-only mode.
 string SkillKeyText() {
  var rows = skillRows.Where(r => r.On && r.Vk != 0 && (skillsSupport.Checked || r.Kind == SkillRules.Attack));
  return string.Join(" ", rows.Select(r => r.Key.Trim().ToUpperInvariant()).Distinct().ToArray());
 }

 void BuildSkillsTab() {
  var page = new TabPage("Skiller"); tabs.TabPages.Add(page);
  var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; page.Controls.Add(scroll);
  var root = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14, 12, 14, 12) };
  scroll.Controls.Add(root);
  var box = new GroupBox { Text = "Skiller · buff · savunma", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 8, 14, 12), MinimumSize = new Size(1080, 0) };
  var card = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
  box.Controls.Add(card); root.Controls.Add(box);

  card.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(1040, 0), Margin = new Padding(0, 4, 0, 8),
   Text = "Hotbar tuşlarını türe göre sıraya diz: saldırı skill'leri hedef türüne ve yakındaki mob sayısına göre seçilir; Skill ID girilen buff'lar karakterin gerçek buff listesine bakılarak bittiğinde yenilenir, Skill ID yoksa süre dolunca basılır; acil buff'lar can düşünce ya da etrafı mob sarınca basılır. Liste Av ve Grim Crypt için ortaktır." });

  card.Controls.Add(SectionLabel("Genel"));
  var row = Line(card);
  ConfigureSkillCheck(skillsEnabled, "Skill listesini kullan · Av / Ayarlar'daki Tuşlar kutusu yerine geçer", 560); row.Controls.Add(skillsEnabled);
  row = Line(card);
  ConfigureSkillCheck(skillsBackground, "Oyun önde değilken de tuşları gönder (arka plan)", 400); row.Controls.Add(skillsBackground);
  row.Controls.Add(Btn("Arka plan tuşunu sına", TestBackgroundKey, 200));
  row = Line(card);
  ConfigureSkillCheck(skillsBuffAttack, "Saldırırken de buff yenile", 260); ConfigureSkillCheck(skillsRebuff, "Dirilince / ışınlanınca buffları yenile", 330);
  row.Controls.Add(skillsBuffAttack); row.Controls.Add(skillsRebuff);
  row = Line(card);
  ConfigureSkillCheck(skillsRestart, "Yeni hedefte saldırı sırasını baştan başlat", 380); ConfigureSkillCheck(skillsFallback, "Türe özel skill hazır değilse Hepsi listesini kullan", 420);
  row.Controls.Add(skillsRestart); row.Controls.Add(skillsFallback);
  row = Line(card);
  ConfigureSkillCheck(skillsSupport, "Saldırma, yalnız buff / iyileştirme yap (Cleric, Bard gibi)", 520); row.Controls.Add(skillsSupport);

  card.Controls.Add(SectionLabel("Profil ve şablon"));
  row = Line(card);
  skillProfileBox.Width = 260; skillProfileBox.DropDownStyle = ComboBoxStyle.DropDown;
  row.Controls.Add(LabelOf("Profil", 80)); row.Controls.Add(skillProfileBox);
  row.Controls.Add(Btn("Kaydet", SaveSkillProfile, 110, false)); row.Controls.Add(Btn("Yükle", LoadSkillProfile, 110, false)); row.Controls.Add(Btn("Sil", DeleteSkillProfile, 90, false));
  row = Line(card);
  skillTemplateBox.Width = 260; skillTemplateBox.DropDownStyle = ComboBoxStyle.DropDownList;
  skillTemplateBox.Items.AddRange(SkillRules.Templates.Keys.Cast<object>().ToArray()); skillTemplateBox.SelectedIndex = 0;
  row.Controls.Add(LabelOf("Şablon", 80)); row.Controls.Add(skillTemplateBox); row.Controls.Add(Btn("Şablonu listeye ekle", AddSkillTemplate, 200, false));

  card.Controls.Add(SectionLabel("Skill listesi"));
  BuildSkillGrid(); card.Controls.Add(skillGrid);
  row = Line(card);
  row.Controls.Add(Btn("Satır ekle", delegate { AppendSkillRows(new[] { new SkillRow() }); }, 130, false));
  row.Controls.Add(Btn("Seçili satırı sil", DeleteSkillRow, 160, false));
  row.Controls.Add(Btn("Aktif buffları ekle", AddActiveBuffs, 180, false));
  row.Controls.Add(LabelOf("Tuş: A-Z / 0-9 · Süre sn: bekleme / yenileme süresi · Mob ≥: yakında (20 birim) en az bu kadar mob · HP/MP %: bu yüzdenin altında bas", 640));

  foreach (var c in new[] { skillsEnabled, skillsBackground, skillsBuffAttack, skillsRebuff, skillsRestart, skillsFallback, skillsSupport })
   c.CheckedChanged += delegate { ApplySkillOptions(); if (!fillingSkills) SaveSettings(); };
  skillsBuilt = true;
  RestoreSkills();
 }

 Label SectionLabel(string text) { return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(0, 12, 0, 4) }; }
 void ConfigureSkillCheck(CheckBox c, string text, int width) { c.Text = text; c.Width = width; c.Height = 29; }

 void BuildSkillGrid() {
  var g = skillGrid;
  g.Size = new Size(1050, 300); g.AllowUserToAddRows = false; g.AllowUserToDeleteRows = true; g.RowHeadersVisible = false;
  g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.EditMode = DataGridViewEditMode.EditOnEnter; g.ColumnHeadersHeight = 32; g.RowTemplate.Height = 28;
  g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
  g.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Açık", Width = 50 });
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ad", Width = 210 });
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tuş", Width = 55 });
  var kind = new DataGridViewComboBoxColumn { HeaderText = "Tür", Width = 135, FlatStyle = FlatStyle.Flat }; kind.Items.AddRange(SkillRules.Kinds); g.Columns.Add(kind);
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Skill ID", Width = 95 });
  var target = new DataGridViewComboBoxColumn { HeaderText = "Hedef türü", Width = 130, FlatStyle = FlatStyle.Flat }; target.Items.AddRange(SkillRules.Targets); g.Columns.Add(target);
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mob ≥", Width = 80 });
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Süre sn", Width = 90 });
  g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HP/MP %", Width = 95 });
  foreach (DataGridViewColumn c in g.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
  g.CurrentCellDirtyStateChanged += delegate { if (g.IsCurrentCellDirty && (g.CurrentCell is DataGridViewCheckBoxCell || g.CurrentCell is DataGridViewComboBoxCell)) g.CommitEdit(DataGridViewDataErrorContexts.Commit); };
  g.CellValueChanged += delegate { if (!fillingSkills) { ReadSkillGrid(); SaveSettings(); } };
  g.RowsRemoved += delegate { if (!fillingSkills) { ReadSkillGrid(); SaveSettings(); } };
  g.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
 }

 static string CellText(DataGridViewRow r, int i) { var v = r.Cells[i].Value; return v == null ? "" : Convert.ToString(v).Trim(); }
 static int CellInt(DataGridViewRow r, int i) { int v; return int.TryParse(CellText(r, i), out v) ? Math.Max(0, v) : 0; }

 void ReadSkillGrid() {
  var list = new List<SkillRow>();
  foreach (DataGridViewRow r in skillGrid.Rows) {
   if (r.IsNewRow) continue;
   uint id; uint.TryParse(CellText(r, 4), out id);
   string kind = CellText(r, 3), target = CellText(r, 5);
   list.Add(new SkillRow {
    On = r.Cells[0].Value is bool && (bool)r.Cells[0].Value,
    Name = CellText(r, 1), Key = CellText(r, 2).ToUpperInvariant(),
    Kind = SkillRules.Kinds.Contains(kind) ? kind : SkillRules.Attack, SkillId = id,
    Target = SkillRules.Targets.Contains(target) ? target : SkillRules.All,
    MinMobs = CellInt(r, 6), Seconds = CellInt(r, 7), Percent = Math.Min(99, CellInt(r, 8)) });
  }
  skillRows = list;
  foreach (DataGridViewRow r in skillGrid.Rows) {
   if (r.IsNewRow) continue;
   bool bad = CellText(r, 2).Length > 0 && HuntCare.KeyOf(CellText(r, 2)) == 0;
   r.Cells[2].Style.ForeColor = bad ? Color.OrangeRed : Color.Empty;
   r.Cells[2].ToolTipText = bad ? "Tuş A-Z veya 0-9 olmalı; bu satır atlanır." : "";
  }
 }

 void FillSkillGrid(IEnumerable<SkillRow> rows) {
  fillingSkills = true;
  try { skillGrid.Rows.Clear(); foreach (var r in rows) AddGridRow(r); } finally { fillingSkills = false; }
  ReadSkillGrid(); ApplyTheme();
 }
 void AddGridRow(SkillRow r) {
  skillGrid.Rows.Add(r.On, r.Name, r.Key, SkillRules.Kinds.Contains(r.Kind) ? r.Kind : SkillRules.Attack, r.SkillId == 0 ? "0" : r.SkillId.ToString(),
   SkillRules.Targets.Contains(r.Target) ? r.Target : SkillRules.All, r.MinMobs.ToString(), r.Seconds.ToString(), r.Percent.ToString());
 }
 void AppendSkillRows(IEnumerable<SkillRow> rows) {
  fillingSkills = true;
  try { foreach (var r in rows) AddGridRow(r.Copy()); } finally { fillingSkills = false; }
  ReadSkillGrid(); ApplyTheme(); SaveSettings();
  if (skillGrid.Rows.Count > 0) skillGrid.FirstDisplayedScrollingRowIndex = skillGrid.Rows.Count - 1;
 }
 void DeleteSkillRow() {
  var rows = skillGrid.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
  if (rows.Count == 0 && skillGrid.CurrentRow != null) rows.Add(skillGrid.CurrentRow);
  foreach (var r in rows) skillGrid.Rows.Remove(r);
 }
 void AddSkillTemplate() {
  var name = skillTemplateBox.SelectedItem as string; SkillRow[] rows;
  if (name == null || !SkillRules.Templates.TryGetValue(name, out rows)) return;
  AppendSkillRows(rows); status.Text = "Şablon eklendi: " + name + " · tuşları kendi hotbar'ına göre düzenle.";
 }
 // Adds a Buff row for every active buff that is not in the list yet, so its Skill ID can be tracked.
 void AddActiveBuffs() {
  RequireReader(); snapshot = reader.Capture();
  if (!snapshot.BuffsKnown) throw new InvalidOperationException("Karakterin buff listesi okunamadı.");
  var known = new HashSet<uint>(skillRows.Select(r => r.SkillId));
  var add = snapshot.BuffIds.Where(id => !known.Contains(id)).OrderBy(id => id).Select(id => new SkillRow { Name = "Buff " + id, Kind = SkillRules.Buff, SkillId = id, On = false }).ToList();
  if (add.Count == 0) { status.Text = snapshot.BuffIds.Count == 0 ? "Karakterde aktif buff yok." : "Aktif buffların hepsi listede."; return; }
  AppendSkillRows(add); status.Text = add.Count + " aktif buff eklendi; her birine tuş yaz ve Açık'ı işaretle.";
 }

 void SaveSkillProfile() {
  string name = skillProfileBox.Text.Trim();
  if (name.Length == 0) throw new InvalidOperationException("Profil adı yaz.");
  ReadSkillGrid();
  settings.SkillProfiles = settings.SkillProfiles ?? new List<SkillProfile>();
  settings.SkillProfiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
  settings.SkillProfiles.Add(new SkillProfile { Name = name, Rows = skillRows.Select(r => r.Copy()).ToList() });
  SaveSettings(); RefreshSkillProfiles(name); status.Text = "Skill profili kaydedildi: " + name;
 }
 void LoadSkillProfile() {
  string name = skillProfileBox.Text.Trim();
  var p = (settings.SkillProfiles ?? new List<SkillProfile>()).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
  if (p == null) throw new InvalidOperationException("Bu adda profil yok.");
  FillSkillGrid(p.Rows ?? new List<SkillRow>()); SaveSettings(); status.Text = "Skill profili yüklendi: " + p.Name;
 }
 void DeleteSkillProfile() {
  string name = skillProfileBox.Text.Trim();
  if (settings.SkillProfiles == null || settings.SkillProfiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) == 0) throw new InvalidOperationException("Bu adda profil yok.");
  SaveSettings(); RefreshSkillProfiles(""); status.Text = "Skill profili silindi: " + name;
 }
 void RefreshSkillProfiles(string select) {
  skillProfileBox.Items.Clear();
  foreach (var p in (settings.SkillProfiles ?? new List<SkillProfile>()).OrderBy(p => p.Name)) skillProfileBox.Items.Add(p.Name);
  skillProfileBox.Text = select;
 }

 void RestoreSkills() {
  if (!skillsBuilt || settings == null) return;
  fillingSkills = true;
  try {
   skillsEnabled.Checked = settings.SkillsEnabled; skillsBackground.Checked = settings.SkillsBackground; skillsBuffAttack.Checked = settings.SkillsBuffWhileAttacking;
   skillsRebuff.Checked = settings.SkillsRebuff; skillsRestart.Checked = settings.SkillsRestartOnTarget; skillsFallback.Checked = settings.SkillsFallbackAll; skillsSupport.Checked = settings.SkillsSupportOnly;
  } finally { fillingSkills = false; }
  FillSkillGrid(settings.Skills ?? new List<SkillRow>());
  RefreshSkillProfiles("");
  ApplySkillOptions();
 }
 void StoreSkills() {
  if (!skillsBuilt || settings == null || fillingSkills) return;
  settings.SkillsEnabled = skillsEnabled.Checked; settings.SkillsBackground = skillsBackground.Checked; settings.SkillsBuffWhileAttacking = skillsBuffAttack.Checked;
  settings.SkillsRebuff = skillsRebuff.Checked; settings.SkillsRestartOnTarget = skillsRestart.Checked; settings.SkillsFallbackAll = skillsFallback.Checked; settings.SkillsSupportOnly = skillsSupport.Checked;
  settings.Skills = skillRows.Select(r => r.Copy()).ToList();
 }
 void ApplySkillOptions() {
  planner.BuffWhileAttacking = skillsBuffAttack.Checked; planner.RestartOnTarget = skillsRestart.Checked;
  planner.FallbackAll = skillsFallback.Checked; planner.SupportOnly = skillsSupport.Checked;
 }

 // Revive and teleport detection: timed buffs are cast again afterwards.
 void TrackSkillState() {
  if (snapshot == null) return;
  bool alive = snapshot.PlayerAlive;
  bool jumped = skillLastPosition != null && snapshot.Player != null && (skillLastPosition.Outside != snapshot.Player.Outside || skillLastPosition.Distance(snapshot.Player) > 80);
  if (skillsRebuff.Checked && ((alive && !skillWasAlive) || (alive && jumped))) planner.ResetBuffs();
  skillWasAlive = alive;
  if (snapshot.Player != null) skillLastPosition = snapshot.Player.Copy();
 }

 // Emergency, heal, mana and buff keys from the skill list. Returns true when a key was pressed.
 bool SkillSupportTick(long now) {
  ApplySkillOptions();
  var row = planner.Support(skillRows, snapshot, SkillRules.MobsNear(snapshot), wanted != 0 || snapshot.Selected != 0, operation != OperationMode.Travel, now);
  if (row == null || !Press(row.Vk)) return false;
  planner.Mark(row, now);
  maintenance.Text = row.Kind + ": " + (row.Name.Length > 0 ? row.Name : row.Key) + " (" + row.Key + ")";
  return true;
 }
 void SkillAttackTick(Monster target, long now) {
  if (!RunSendKeys || now < nextKey || heldScan != 0) return;
  ApplySkillOptions();
  var row = planner.Attack(skillRows, target, SkillRules.MobsNear(snapshot), now);
  if (row == null || !Press(row.Vk)) return;
  planner.Mark(row, now);
  nextKey = now + RunDelay;
 }

 IntPtr GameWindow() {
  if (reader == null) return IntPtr.Zero;
  if (gameWindow == IntPtr.Zero || gameWindowPid != reader.Process.Id) { gameWindow = FindClientWindow(reader.Process); gameWindowPid = reader.Process.Id; }
  return gameWindow;
 }
 static IntPtr KeyParam(ushort scan, bool up) { uint p = 1u | ((uint)scan << 16); if (up) p |= (1u << 30) | (1u << 31); return new IntPtr(unchecked((int)p)); }
 // Sends the key to the game window's message queue; works while the game is behind other windows.
 bool PostKey(ushort vk) {
  IntPtr window = GameWindow();
  if (window == IntPtr.Zero) return false;
  ushort scan = (ushort)Native.MapVirtualKeyEx(vk, 0, Native.GetKeyboardLayout(0));
  if (!PostMessage(window, WmKeyDown, new IntPtr(vk), KeyParam(scan, false))) return false;
  heldScan = scan == 0 ? (ushort)1 : scan; heldVk = vk; heldPosted = true; release.Start();
  return true;
 }
 ushort heldVk;
 void ReleasePostedKey() {
  IntPtr window = GameWindow();
  if (window != IntPtr.Zero) PostMessage(window, WmKeyUp, new IntPtr(heldVk), KeyParam(heldScan, true));
  heldPosted = false; heldScan = 0; heldVk = 0;
 }

 // Presses one key from the list in the background and checks the character for a reaction (MP, HP or buff change).
 void TestBackgroundKey() {
  RequireReader();
  if (active) throw new InvalidOperationException("Otomasyon çalışırken sınanamaz.");
  ReadSkillGrid();
  var row = skillRows.Where(r => r.On && r.Vk != 0).OrderBy(r => r.Kind == SkillRules.Buff ? 0 : r.Kind == SkillRules.Heal || r.Kind == SkillRules.Mana ? 1 : 2).FirstOrDefault()
   ?? skillRows.FirstOrDefault(r => r.Vk != 0);
  if (row == null) throw new InvalidOperationException("Önce skill listesine tuşu olan bir satır ekle.");
  if (GameWindow() == IntPtr.Zero) throw new InvalidOperationException("Oyun penceresi bulunamadı.");
  var before = reader.Capture();
  ReleaseKey();
  if (!PostKey(row.Vk)) throw new InvalidOperationException("Windows tuş mesajını kabul etmedi.");
  status.Text = "Arka plan tuşu gönderildi: " + row.Key + " (" + (row.Name.Length > 0 ? row.Name : row.Kind) + ") · tepki bekleniyor...";
  Log(status.Text);
  if (backgroundProbe != null) backgroundProbe.Dispose();
  backgroundProbe = new Timer { Interval = 2500 };
  backgroundProbe.Tick += delegate {
   backgroundProbe.Stop();
   try {
    if (reader == null) return;
    var after = reader.Capture();
    bool reacted = after.Mp < before.Mp || after.Hp != before.Hp || !after.BuffIds.SetEquals(before.BuffIds) || after.Selected != before.Selected;
    status.Text = reacted
     ? "Arka plan tuşu çalıştı: oyun " + row.Key + " tuşuna tepki verdi."
     : "Tepki görülmedi: " + row.Key + " tuşunda skill / pot var mı, bekleme süresinde mi kontrol et; oyunda kendin de bakabilirsin.";
    Log(status.Text);
   } catch (Exception e) { status.Text = "Sınama okunamadı: " + e.Message; }
  };
  backgroundProbe.Start();
 }
}
