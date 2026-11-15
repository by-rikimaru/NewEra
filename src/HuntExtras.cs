using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.IO;

// Level table of every monster model (from the game's own character data) so the hunt can be limited to a level range.
static class MobData {
 static Dictionary<uint,int> levels;
 internal static int Level(uint model){
  if(levels==null){
   var map=new Dictionary<uint,int>();
   try{
    using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("mob-levels.json")){
     if(s!=null)using(var r=new StreamReader(s,Encoding.UTF8)){var raw=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<Dictionary<string,int>>(r.ReadToEnd());foreach(var kv in raw){uint id;if(uint.TryParse(kv.Key,out id))map[id]=kv.Value;}}
    }
   }catch{}
   levels=map;
  }
  int level;return levels.TryGetValue(model,out level)?level:0;
 }
}

// Which monsters the hunt may attack: level range and monster class. Unknown level (0) is never excluded.
static class HuntFilter {
 internal static int MinLevel=1,MaxLevel=255;
 internal static bool Normal=true,Champion=true,Giant=true,Unique=true,Other=true;
 internal static bool Allows(Monster m){
  if(m==null)return false;
  int level=MobData.Level(m.Model);
  if(level>0&&(level<MinLevel||level>MaxLevel))return false;
  return KindAllowed(m.Rank);
 }
 internal static bool KindAllowed(int rank){
  switch(rank){
   case 0:case 2:return Normal;
   case 1:return Champion;
   case 3:return Unique;
   case 4:case 5:return Giant;
   default:return Other;
  }
 }
}

// Potion and buff keys, pressed from the hunt loop.
sealed class HuntCare {
 internal long NextHp,NextMp,NextBuff;
 internal static bool NeedHp(Snapshot s,int percent){return percent>0&&s.StatsKnown&&s.MaxHp>0&&s.Hp>0&&(long)s.Hp*100<(long)percent*s.MaxHp;}
 internal static bool NeedMp(Snapshot s,int percent){return percent>0&&s.StatsKnown&&s.MaxMp>0&&(long)s.Mp*100<(long)percent*s.MaxMp;}
 internal static ushort KeyOf(string text){text=(text??"").Trim().ToUpperInvariant();return text.Length==1&&((text[0]>='0'&&text[0]<='9')||(text[0]>='A'&&text[0]<='Z'))?(ushort)text[0]:(ushort)0;}
}

sealed partial class MemorySelectV3Form {
 readonly NumericUpDown minLevel=new NumericUpDown(),maxLevel=new NumericUpDown(),hpPercent=new NumericUpDown(),mpPercent=new NumericUpDown(),buffSeconds=new NumericUpDown();
 readonly CheckBox kindNormal=new CheckBox(),kindChampion=new CheckBox(),kindGiant=new CheckBox(),kindUnique=new CheckBox(),kindOther=new CheckBox();
 readonly TextBox hpKey=new TextBox(),mpKey=new TextBox(),buffKeys=new TextBox();
 readonly HuntCare care=new HuntCare();

 void BuildHuntExtras(FlowLayoutPanel card){
  var row=Line(card);Num(minLevel,1,255,1,0,80);Num(maxLevel,1,255,255,0,80);
  row.Controls.Add(LabelOf("Hedef seviyesi",120));row.Controls.Add(minLevel);row.Controls.Add(LabelOf("en az",50));row.Controls.Add(maxLevel);row.Controls.Add(LabelOf("en çok · seviyesi bilinmeyen yaratık atlanmaz",420));
  row=Line(card);ConfigureCheck(kindNormal,"Normal",true,95);ConfigureCheck(kindChampion,"Champion",true,110);ConfigureCheck(kindGiant,"Giant",true,90);ConfigureCheck(kindUnique,"Unique",true,95);ConfigureCheck(kindOther,"Diğer (Titan / Elite)",true,200);
  row.Controls.Add(LabelOf("Yaratık türü",120));foreach(var c in new[]{kindNormal,kindChampion,kindGiant,kindUnique,kindOther})row.Controls.Add(c);
  row=Line(card);hpKey.Width=50;mpKey.Width=50;buffKeys.Width=160;Num(hpPercent,0,99,0,0,70);Num(mpPercent,0,99,0,0,70);Num(buffSeconds,10,3600,120,0,80);locked.Add(hpKey);locked.Add(mpKey);locked.Add(buffKeys);
  row.Controls.Add(LabelOf("HP pot tuşu",100));row.Controls.Add(hpKey);row.Controls.Add(LabelOf("HP % altı",90));row.Controls.Add(hpPercent);row.Controls.Add(LabelOf("MP pot tuşu",105));row.Controls.Add(mpKey);row.Controls.Add(LabelOf("MP % altı",90));row.Controls.Add(mpPercent);row.Controls.Add(LabelOf("0 = kapalı",90));
  row=Line(card);row.Controls.Add(LabelOf("Buff tuşları",100));row.Controls.Add(buffKeys);row.Controls.Add(LabelOf("her (sn)",75));row.Controls.Add(buffSeconds);row.Controls.Add(LabelOf("Boşluklu tuşlar sırayla basılır; avdan önce ve her süre dolunca yenilenir.",560));
 }

 void RestoreHuntExtras(){
  SetValue(minLevel,settings.HuntMinLevel);SetValue(maxLevel,settings.HuntMaxLevel);
  kindNormal.Checked=settings.KindNormal;kindChampion.Checked=settings.KindChampion;kindGiant.Checked=settings.KindGiant;kindUnique.Checked=settings.KindUnique;kindOther.Checked=settings.KindOther;
  hpKey.Text=settings.HpKey??"";mpKey.Text=settings.MpKey??"";buffKeys.Text=settings.BuffKeys??"";SetValue(hpPercent,settings.HpPercent);SetValue(mpPercent,settings.MpPercent);SetValue(buffSeconds,settings.BuffSeconds);
  ApplyHuntFilter();
 }
 void StoreHuntExtras(){
  settings.HuntMinLevel=(int)minLevel.Value;settings.HuntMaxLevel=(int)maxLevel.Value;
  settings.KindNormal=kindNormal.Checked;settings.KindChampion=kindChampion.Checked;settings.KindGiant=kindGiant.Checked;settings.KindUnique=kindUnique.Checked;settings.KindOther=kindOther.Checked;
  settings.HpKey=hpKey.Text.Trim();settings.MpKey=mpKey.Text.Trim();settings.BuffKeys=buffKeys.Text.Trim();settings.HpPercent=(int)hpPercent.Value;settings.MpPercent=(int)mpPercent.Value;settings.BuffSeconds=(int)buffSeconds.Value;
 }
 void ApplyHuntFilter(){
  HuntFilter.MinLevel=(int)minLevel.Value;HuntFilter.MaxLevel=(int)Math.Max(minLevel.Value,maxLevel.Value);
  HuntFilter.Normal=kindNormal.Checked;HuntFilter.Champion=kindChampion.Checked;HuntFilter.Giant=kindGiant.Checked;HuntFilter.Unique=kindUnique.Checked;HuntFilter.Other=kindOther.Checked;
 }
 // Potions and buffs. Presses at most one key per call and only while the game window is in front.
 void HuntCareTick(long now){
  ApplyHuntFilter();
  if(heldScan!=0||!reader.IsForeground()||!snapshot.PlayerAlive)return;
  ushort key;
  if(HuntCare.NeedHp(snapshot,(int)hpPercent.Value)&&now>=care.NextHp&&(key=HuntCare.KeyOf(hpKey.Text))!=0){if(Press(key)){care.NextHp=now+1500;maintenance.Text="HP düşük: pot kullanıldı.";}return;}
  if(HuntCare.NeedMp(snapshot,(int)mpPercent.Value)&&now>=care.NextMp&&(key=HuntCare.KeyOf(mpKey.Text))!=0){if(Press(key)){care.NextMp=now+1500;maintenance.Text="MP düşük: pot kullanıldı.";}return;}
  var buffs=buffKeys.Text.Split(new[]{' ',',',';'},StringSplitOptions.RemoveEmptyEntries).Select(HuntCare.KeyOf).Where(k=>k!=0).ToArray();
  if(buffs.Length==0||operation==OperationMode.Travel)return;
  if(care.NextBuff==0)care.NextBuff=now;
  if(now<care.NextBuff)return;
  if(buffIndex>=buffs.Length)buffIndex=0;
  if(Press(buffs[buffIndex])){buffIndex++;care.NextBuff=buffIndex>=buffs.Length?now+(long)buffSeconds.Value*1000:now+1200;if(buffIndex>=buffs.Length)buffIndex=0;maintenance.Text="Buff basıldı.";}
 }
 int buffIndex;
}
