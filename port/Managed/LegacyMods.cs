using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace OsuLegacy
{
    public sealed class LegacyMod
    {
        public string Acronym,Name,Category,Description;
        public double Multiplier;
        public bool Available;
        public LegacyMod(string a,string n,string c,double m,bool available,string description)
        { Acronym=a;Name=n;Category=c;Multiplier=m;Available=available;Description=description; }
    }
    public sealed class LegacyModState
    {
        public static readonly string[] Categories={"reduction","increase","automation","conversion","fun"};
        public readonly List<LegacyMod> All=new List<LegacyMod>();
        private readonly Dictionary<string,bool> selected=new Dictionary<string,bool>(StringComparer.Ordinal);
        public string SettingsPath;
        public double SpeedOverride,MinimumAccuracy=90,DACs=-1,DAAr=-1,DAOd=-1,DAHp=-1;public int Seed;private bool loading;
        public LegacyModState()
        {
            Add("NF","No Fail","reduction",.50,true,"You cannot fail.");Add("EZ","Easy","reduction",.50,true,"Larger circles and forgiving timing.");
            Add("HT","Half Time","reduction",.30,true,"Slower playback without changing pitch.");Add("DC","Daycore","reduction",.30,true,"Slower playback with lower pitch.");
            Add("HR","Hard Rock","increase",1.06,true,"Smaller circles, stricter timing, flipped playfield.");Add("SD","Sudden Death","increase",1,true,"Miss once and fail.");
            Add("PF","Perfect","increase",1,true,"Anything below 300 fails.");Add("DT","Double Time","increase",1.12,true,"Faster playback without changing pitch.");
            Add("NC","Nightcore","increase",1.12,true,"Faster playback with higher pitch.");Add("HD","Hidden","increase",1.06,true,"Objects fade before they are hit.");
            Add("FL","Flashlight","increase",1.12,true,"Restricted playfield visibility.");Add("BL","Blinds","increase",1.12,true,"Rising health closes blinds across the playfield.");
            Add("ST","Strict Tracking","increase",1.04,true,"Strict slider tracking.");Add("AC","Accuracy Challenge","increase",1,true,"Maintain the target accuracy.");
            Add("TC","Traceable","increase",1.06,true,"Only approach traces remain visible.");
            Add("AT","Autoplay","automation",1,true,"Watch a perfect automated play.");Add("RX","Relax","automation",0,true,"Aim while taps happen automatically.");
            Add("AP","Autopilot","automation",0,true,"Tap while aiming happens automatically.");Add("SO","Spun Out","automation",.90,true,"Spinners complete automatically.");
            Add("CN","Cinema","automation",0,true,"Watch without gameplay overlays.");
            Add("TP","Target Practice","conversion",1,true,"Generated targets follow the song beat.");Add("AL","Alternate","conversion",1,true,"Alternate inputs.");
            Add("SG","Single Tap","conversion",1,true,"Use one input stream.");Add("CL","Classic","conversion",.96,true,"Stable-era scoring and slider rules.");
            Add("DA","Difficulty Adjust","conversion",1,true,"Custom difficulty values.");Add("RD","Random","conversion",1,true,"Randomise object positions.");
            Add("MR","Mirror","conversion",1,true,"Mirror the playfield horizontally.");
            Add("WG","Wiggle","fun",1,true,"Objects wiggle around their positions.");Add("TR","Transform","fun",1,true,"Objects continuously transform.");
            Add("BR","Barrel Roll","fun",1,true,"Rotate the whole playfield.");Add("BU","Bubbles","fun",1,true,"Hit objects behave like bubbles.");
            Add("SI","Spin In","fun",1,true,"Objects spin into view.");Add("GR","Grow","fun",1,true,"Objects grow toward hit time.");
            Add("DF","Deflate","fun",1,true,"Objects shrink toward hit time.");Add("FR","Freeze Frame","fun",1,true,"Combo objects appear together.");
            Add("DP","Depth","fun",1,true,"Adds depth motion to objects.");Add("MG","Magnetised","fun",1,true,"Cursor attracts hit objects.");
            Add("RP","Repel","fun",1,true,"Cursor repels hit objects.");Add("MU","Muted","fun",1,true,"Gameplay audio fades with combo.");
            Add("SY","Synesthesia","fun",1,true,"Rhythm divisions colour objects.");Add("AD","Approach Different","fun",1,true,"Alternative approach animation.");
            Add("BM","Bloom","fun",1,true,"Cursor grows as combo rises.");Add("NS","No Scope","fun",1,true,"Cursor fades as combo rises.");
        }
        private void Add(string a,string n,string c,double m,bool available,string d){All.Add(new LegacyMod(a,n,c,m,available,d));}
        public bool Enabled(string acronym){bool value;return selected.TryGetValue(acronym,out value)&&value;}
        public double DisplayRate {get{if(Enabled("DT") || Enabled("NC"))return SpeedOverride>1 && SpeedOverride<=2?SpeedOverride:1.5;if(Enabled("HT") || Enabled("DC"))return SpeedOverride>=.5 && SpeedOverride<1?SpeedOverride:.75;return SpeedOverride>0?SpeedOverride:1;}}
        public double Multiplier { get {double value=1;foreach(LegacyMod m in All)if(Enabled(m.Acronym))value*=m.Multiplier;return value;} }
        public string Summary { get {List<string> values=new List<string>();foreach(LegacyMod m in All)if(Enabled(m.Acronym))values.Add(m.Acronym);return values.Count==0?"None":String.Join(",",values.ToArray());} }
        public bool AvailableFor(LegacyMod mod,int ruleset){
            if(!mod.Available)return false;if(ruleset==0)return true;
            string allowed=ruleset==1?",NF,EZ,HT,DC,HR,SD,PF,DT,NC,HD,FL,AC,RD,DA,CL,SG,AT,CN,RX,MU,":ruleset==2?",NF,EZ,HT,DC,HR,SD,PF,DT,NC,HD,FL,AC,DA,CL,MR,AT,CN,RX,MU,NS,SY,":",NF,EZ,HT,DC,HR,SD,PF,DT,NC,HD,FL,AC,RD,MR,DA,CL,AT,CN,MU,";
            return allowed.Contains(","+mod.Acronym+",");
        }
        public string SummaryFor(int ruleset){var values=new List<string>();foreach(LegacyMod m in All)if(Enabled(m.Acronym) && AvailableFor(m,ruleset))values.Add(m.Acronym);return values.Count==0?"None":String.Join(",",values.ToArray());}
        public double MultiplierFor(int ruleset){double value=1;foreach(LegacyMod m in All)if(Enabled(m.Acronym) && AvailableFor(m,ruleset))value*=ruleset==2 && m.Acronym=="HR"?1.12:m.Multiplier;return value;}
        public void Toggle(LegacyMod mod)
        {
            if(!mod.Available)return;bool enable=!Enabled(mod.Acronym);
            if(enable) {
                string a=mod.Acronym;
                string[] groups={"HT,DC,DT,NC","WG,TR,MG,RP,DP","GR,DF,SI","AL,SG","AT,CN,RX,AP"};foreach(string group in groups)if((","+group+",").Contains(","+a+","))foreach(string other in group.Split(','))if(other!=a)selected.Remove(other);
                string[] conflicts={"NF:SD,PF,AC","EZ:HR,AC","HR:EZ,MR","MR:HR","PF:SD,AC","SD:PF","AC:NF,EZ,PF","SO:AP,TP","ST:CL,TP","CL:ST","TP:RD,DA,ST","RD:TP","DA:TP","HD:SI,TC,DP,FR","TC:HD,SI,DP","SI:HD,TC,DP,FR","FR:HD,AD,TR,DP,SI","AD:FR","DP:HD,FR,TC,SI","BR:BU","BU:BR,MG,RP","MG:BU,AT,CN,RX,AP","RP:BU,AT,CN,AP","BM:FL,NS","NS:BM","FL:BM,BL","BL:FL","AL:AT,CN,RX","SG:AT,CN,RX","AT:AL,SG,MG,RP","CN:AL,SG,MG,RP","RX:AL,SG,MG","AP:MG,RP"};
                foreach(string pair in conflicts){string[] fields=pair.Split(':');if(fields[0]==a)foreach(string other in fields[1].Split(','))selected.Remove(other);else if((","+fields[1]+",").Contains(","+a+","))selected.Remove(fields[0]);}
                if(a=="SD"||a=="PF")selected.Remove("NF");

                if(mod.Acronym=="EZ")selected.Remove("HR");if(mod.Acronym=="HR")selected.Remove("EZ");
                if(mod.Acronym=="HT"||mod.Acronym=="DC"){selected.Remove("DT");selected.Remove("NC");}
                if(mod.Acronym=="DT"||mod.Acronym=="NC"){selected.Remove("HT");selected.Remove("DC");}
                if(mod.Acronym=="SD")selected.Remove("PF");if(mod.Acronym=="PF")selected.Remove("SD");
                if(mod.Acronym=="AT"||mod.Acronym=="CN"){selected.Remove("RX");selected.Remove("AP");selected.Remove("SO");}
                if(mod.Acronym=="RX"){selected.Remove("AT");selected.Remove("CN");selected.Remove("AP");}
                if(mod.Acronym=="AP"){selected.Remove("AT");selected.Remove("CN");selected.Remove("RX");}
                selected[mod.Acronym]=true;
            } else selected.Remove(mod.Acronym);Save();
        }
        public void Clear(){selected.Clear();Save();}
        public List<LegacyMod> InCategory(string category){return All.FindAll(delegate(LegacyMod m){return m.Category==category;});}
        public ModSettings Capture(int ruleset){return new ModSettings(this,ruleset);}
        public void Load(){
            if(String.IsNullOrEmpty(SettingsPath)||!File.Exists(SettingsPath))return;string[] lines=File.ReadAllLines(SettingsPath);if(lines.Length==0)return;loading=true;
            try{selected.Clear();foreach(string acronym in lines[0].Split(','))foreach(LegacyMod mod in All)if(mod.Acronym==acronym && mod.Available && !Enabled(acronym))Toggle(mod);
                for(int i=1;i<lines.Length;i++){string[] f=lines[i].Split('=');double n;if(f.Length!=2 || !double.TryParse(f[1],NumberStyles.Float,CultureInfo.InvariantCulture,out n) || Double.IsNaN(n)||Double.IsInfinity(n))continue;
                    if(f[0]=="Rate")SpeedOverride=Math.Max(0,Math.Min(2,n));if(f[0]=="Accuracy")MinimumAccuracy=Math.Max(60,Math.Min(99.9,n));if(f[0]=="CS")DACs=Math.Max(-1,Math.Min(11,n));if(f[0]=="AR")DAAr=Math.Max(-1,Math.Min(11,n));if(f[0]=="OD")DAOd=Math.Max(-1,Math.Min(11,n));if(f[0]=="HP")DAHp=Math.Max(-1,Math.Min(10,n));if(f[0]=="Seed" && n>=int.MinValue && n<=int.MaxValue)Seed=(int)n;
                }
            }finally{loading=false;}
        }
        public void Save(){if(loading || String.IsNullOrEmpty(SettingsPath))return;File.WriteAllText(SettingsPath,(Summary=="None"?"":Summary)+"\nRate="+SpeedOverride.ToString(CultureInfo.InvariantCulture)+"\nAccuracy="+MinimumAccuracy.ToString(CultureInfo.InvariantCulture)+"\nCS="+DACs.ToString(CultureInfo.InvariantCulture)+"\nAR="+DAAr.ToString(CultureInfo.InvariantCulture)+"\nOD="+DAOd.ToString(CultureInfo.InvariantCulture)+"\nHP="+DAHp.ToString(CultureInfo.InvariantCulture)+"\nSeed="+Seed);}
    }
}
