using System;using System.Collections.Generic;using System.Drawing;using System.IO;using System.Linq;using System.Reflection;using System.Diagnostics;using System.Web.Script.Serialization;using System.Windows.Forms;
sealed partial class MemorySelectV3Form {
 readonly ComboBox themePicker=new ComboBox();
 void BuildModernTabs(){
  var page=new TabPage("Nanzhang NPC");tabs.TabPages.Add(page);var split=new SplitContainer{Size=new Size(1400,650),Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,SplitterDistance=330};page.Controls.Add(split);
  var search=new TextBox{Dock=DockStyle.Top};var list=new ListBox{Dock=DockStyle.Fill};var viewer=new MapViewer{Dock=DockStyle.Fill};split.Panel1.Controls.Add(list);split.Panel1.Controls.Add(search);split.Panel2.Controls.Add(viewer);viewer.LoadResource("nanzhang-map.jpg");
  List<NpcMapRecord> records;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("nanzhang-data.json"))using(var r=new StreamReader(stream)){records=new JavaScriptSerializer{MaxJsonLength=16000000}.Deserialize<NpcMapData>(r.ReadToEnd()).NPCs;}
  Action filter=delegate{list.Items.Clear();foreach(var n in records.Where(n=>(n.name+" "+n.id).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0))list.Items.Add(n);};search.TextChanged+=delegate{filter();};filter();
  var hint=new Label{Dock=DockStyle.Bottom,Height=70,Text="İsim veya kimlik ara · Fare tekerleği: yakınlaştır · Sol tuş: sürükle\n134 canlı kayıt; şehir krokisi yaklaşık yapı izdüşümüdür."};split.Panel1.Controls.Add(hint);list.SelectedIndexChanged+=delegate{var n=list.SelectedItem as NpcMapRecord;if(n!=null){hint.Text=n.name+" · Kimlik "+n.id+"\nX "+n.x.ToString("F1")+" / Y "+n.y.ToString("F1")+" · Harita no "+n.number;viewer.FocusNpc(n.x,n.y);}};
  var themes=new TabPage("Görünüm");tabs.TabPages.Add(themes);var row=new FlowLayoutPanel{Dock=DockStyle.Top,Height=90,Padding=new Padding(20)};themes.Controls.Add(row);row.Controls.Add(new Label{Text="Tema",AutoSize=true});themePicker.Width=250;themePicker.DropDownStyle=ComboBoxStyle.DropDownList;themePicker.Items.AddRange(new[]{"Lacivert · Altın","Siyah · Turkuaz","Beyaz · Mavi","Siyah","Mavi · Gümüş","Siyah · Zümrüt","Gece · Mor"});row.Controls.Add(themePicker);themePicker.SelectedIndexChanged+=delegate{ApplyTheme();if(settings!=null){settings.Theme=themePicker.SelectedIndex;SaveSettings();}};themePicker.SelectedIndex=Math.Max(0,Math.Min(themePicker.Items.Count-1,settings.Theme));
 }
 Color accentColor=Color.FromArgb(193,151,72),sidebarColor=Color.FromArgb(14,24,40);
 void ApplyTheme(){
  int n=Math.Max(0,Math.Min(6,themePicker.SelectedIndex));
  Color[][] palettes={
   new[]{C(20,34,55),C(30,48,74),C(234,238,244),C(193,151,72),C(14,24,40)},
   new[]{C(22,25,30),C(34,38,46),C(234,238,244),C(30,155,161),C(15,17,21)},
   new[]{C(245,247,251),C(255,255,255),C(30,45,65),C(55,112,183),C(228,233,242)},
   new[]{C(22,25,30),C(36,38,44),C(234,238,244),C(96,102,114),C(15,17,21)},
   new[]{C(20,34,55),C(30,48,74),C(234,238,244),C(55,112,183),C(14,24,40)},
   new[]{C(18,30,28),C(28,46,42),C(232,242,238),C(46,160,120),C(11,21,19)},
   new[]{C(26,22,40),C(40,34,60),C(236,233,246),C(135,100,210),C(18,15,30)}};
  Color bg=palettes[n][0],input=palettes[n][1],fg=palettes[n][2],accent=palettes[n][3];sidebarColor=palettes[n][4];accentColor=accent;
  Color hover=Color.FromArgb(Math.Min(255,accent.R+28),Math.Min(255,accent.G+28),Math.Min(255,accent.B+28)),down=Color.FromArgb(accent.R*4/5,accent.G*4/5,accent.B*4/5);
  Action<Control> paint=null;
  paint=c=>{
   if(c is MapViewer||c is DungeonRadar)return;
   c.BackColor=bg;c.ForeColor=fg;
   if(c is TextBox||c is ComboBox||c is NumericUpDown||c is ListBox)c.BackColor=input;
   if(c is GroupBox)c.ForeColor=accent;
   var b=c as Button;
   if(b!=null){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.FlatAppearance.MouseOverBackColor=hover;b.FlatAppearance.MouseDownBackColor=down;b.BackColor=accent;b.ForeColor=Color.White;b.Cursor=Cursors.Hand;}
   var g=c as DataGridView;
   if(g!=null){g.EnableHeadersVisualStyles=false;g.BorderStyle=BorderStyle.None;g.GridColor=bg;g.BackgroundColor=bg;g.DefaultCellStyle.BackColor=input;g.DefaultCellStyle.ForeColor=fg;g.DefaultCellStyle.SelectionBackColor=accent;g.DefaultCellStyle.SelectionForeColor=Color.White;g.ColumnHeadersDefaultCellStyle.BackColor=accent;g.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;g.ColumnHeadersDefaultCellStyle.SelectionBackColor=accent;g.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;foreach(DataGridViewRow r in g.Rows){r.DefaultCellStyle.BackColor=input;r.DefaultCellStyle.ForeColor=fg;}}
   foreach(Control child in c.Controls)paint(child);
  };
  paint(this);
  if(sidebar!=null){sidebar.BackColor=sidebarColor;sidebar.Invalidate();}
 }
 static Color C(int r,int g,int b){return Color.FromArgb(r,g,b);} void OpenManual(){string path=Path.Combine(V3Settings.DirectoryPath,"Kullanim-Kilavuzu.pdf");Directory.CreateDirectory(V3Settings.DirectoryPath);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("manual.pdf"))using(var file=File.Create(path))stream.CopyTo(file);Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
 sealed class NpcMapData{public List<NpcMapRecord> NPCs;}
 sealed class NpcMapRecord{public string name;public int id,number;public double x,y;public override string ToString(){return number.ToString("000")+" · "+name;}}
}
