using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OsuLegacy
{
    // Background / legacy Video-as-image / first storyboard Sprite fallback,
    // following the attached LegacyBeatmapDecoder event handling.
    public static class BeatmapArtwork
    {
        public static bool ReadVideoEvent(string beatmap,string line,out string path,out double offset)
        {
            path=null;offset=0;
            List<string> fields=Csv(line);
            if(fields.Count<3 || !(fields[0]=="1" || String.Equals(fields[0],"Video",StringComparison.OrdinalIgnoreCase)))return false;
            string extension=Path.GetExtension(fields[2]).ToLowerInvariant();
            if(extension==".jpg" || extension==".jpeg" || extension==".png" || extension==".bmp" || extension==".gif" || extension==".tif" || extension==".tiff")return false;
            try { offset=Beatmap.Number(fields[1]); }
            catch(FormatException) { return false; }
            path=Resolve(beatmap,fields[2]);
            return path!=null;
        }
        public static string ReadEvent(string beatmap, string line, ref int priority, string current)
        {
            List<string> fields=Csv(line);
            if(fields.Count<3)return current;
            string type=fields[0];int candidatePriority=0,index=2;
            if(type=="0" || String.Equals(type,"Background",StringComparison.OrdinalIgnoreCase))candidatePriority=3;
            else if(type=="1" || String.Equals(type,"Video",StringComparison.OrdinalIgnoreCase))
            {
                string extension=Path.GetExtension(fields[2]).ToLowerInvariant();
                if(extension==".jpg" || extension==".jpeg" || extension==".png" || extension==".bmp" || extension==".gif" || extension==".tif" || extension==".tiff")candidatePriority=2;
            }
            else if(type=="4" || String.Equals(type,"Sprite",StringComparison.OrdinalIgnoreCase)){candidatePriority=1;index=3;}
            if(candidatePriority==0 || fields.Count<=index || candidatePriority<priority || (candidatePriority==1 && priority==1))return current;
            string resolved=Resolve(beatmap,fields[index]);
            if(resolved==null)return current;
            priority=candidatePriority;return resolved;
        }
        public static List<string> Csv(string line)
        {
            var fields=new List<string>();var part=new StringBuilder();bool quoted=false;
            for(int i=0;i<line.Length;i++)
            {
                char c=line[i];
                if(c=='"')
                {
                    if(quoted && i+1<line.Length && line[i+1]=='"'){part.Append('"');i++;}
                    else quoted=!quoted;
                }
                else if(c==',' && !quoted){fields.Add(part.ToString().Trim());part.Length=0;}
                else part.Append(c);
            }
            if(quoted)return new List<string>();
            fields.Add(part.ToString().Trim());return fields;
        }
        public static string Resolve(string beatmap,string filename)
        {
            try
            {
                string relative=filename.Replace('\\','/');
                if(relative.Length==0 || relative[0]=='/' || relative.IndexOf(':')>=0)return null;
                string root=Path.GetFullPath(Path.GetDirectoryName(beatmap));
                string candidate=Path.GetFullPath(Path.Combine(root,relative));
                if(!candidate.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))return null;
                // Resolve each component to support Windows filename casing on
                // case-sensitive filesystems and reject symlink escapes.
                string current=root;
                foreach(string part in candidate.Substring(root.Length+1).Split(Path.DirectorySeparatorChar))
                {
                    string next=Path.Combine(current,part);
                    if(!File.Exists(next) && !Directory.Exists(next))
                    {
                        next=null;
                        foreach(string entry in Directory.GetFileSystemEntries(current))
                            if(String.Equals(Path.GetFileName(entry),part,StringComparison.OrdinalIgnoreCase)){next=entry;break;}
                    }
                    if(next==null || (File.GetAttributes(next)&FileAttributes.ReparsePoint)!=0)return null;
                    current=next;
                }
                return File.Exists(current)?current:null;
            }
            catch(IOException){return null;}
            catch(UnauthorizedAccessException){return null;}
            catch(ArgumentException){return null;}
            catch(NotSupportedException){return null;}
        }
    }
}
