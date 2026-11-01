using System;
using System.Collections.Generic;
namespace OsuLegacy {
    public enum NotificationKind { Info, Success, Warning, Error, Progress }
    public sealed class LegacyNotification {
        public string Key,Title,Message,Details;public NotificationKind Kind;
        public bool Read,Ongoing,Critical,Dismissed,ToastShown;
        public double Progress=-1,ToastUntil;public Action Activated;
    }
    // Local counterpart of NotificationOverlay: separate ongoing tasks,
    // retained history and transient toasts. All mutations run on the UI thread.
    public sealed class NotificationCenter {
        private readonly List<LegacyNotification> items=new List<LegacyNotification>();
        public IList<LegacyNotification> Items {get {return items.AsReadOnly();}}
        public int Revision {get;private set;}
        public int UnreadCount {get {int count=0;foreach(var item in items)if(!item.Dismissed && !item.Read && !item.Ongoing)count++;return count;}}
        public LegacyNotification Post(string key,string title,string message,NotificationKind kind,bool critical,Action action){
            LegacyNotification item=null;
            if(key!=null)foreach(var existing in items)if(existing.Key==key && !existing.Dismissed){item=existing;break;}
            if(item!=null && !item.Read && item.Title==title && item.Message==message && item.Kind==kind && item.Critical==critical && !item.Ongoing)return item;
            if(item==null){item=new LegacyNotification {Key=key};items.Insert(0,item);}
            item.Title=title??"";item.Message=message??"";item.Details=item.Message;item.Kind=kind;item.Critical=critical;item.Activated=action;
            item.Ongoing=kind==NotificationKind.Progress;item.Read=false;item.ToastShown=false;item.ToastUntil=0;
            Revision++;Trim();return item;
        }
        public void Progress(string key,string title,string message,double progress){
            LegacyNotification item=null;foreach(var existing in items)if(existing.Key==key && !existing.Dismissed){item=existing;break;}
            if(item==null || !item.Ongoing)item=Post(key,title,message,NotificationKind.Progress,false,null);
            if(item.Message==message && item.Progress==progress)return;
            item.Title=title;item.Message=message;item.Progress=progress;Revision++;
        }
        public void RemoveTask(string key){foreach(var item in items)if(item.Key==key && item.Ongoing){item.Dismissed=true;Revision++;}}
        public bool Dismiss(LegacyNotification item){if(item==null || item.Ongoing || item.Dismissed)return false;item.Dismissed=true;Revision++;return true;}
        public void ClearCompleted(){foreach(var item in items)if(!item.Ongoing)item.Dismissed=true;Revision++;Trim();}
        public void MarkRead(){foreach(var item in items)if(!item.Ongoing){item.Read=true;item.ToastShown=true;item.ToastUntil=0;}Revision++;}
        public LegacyNotification Toast(double time,bool gameplay){
            foreach(var item in items){
                if(item.Dismissed || item.Read || item.Ongoing || (gameplay && !item.Critical))continue;
                if(!item.ToastShown){item.ToastShown=true;item.ToastUntil=time+(item.Kind==NotificationKind.Error?10:6);Revision++;return item;}
                if(item.ToastUntil>time)return item;
            }
            return null;
        }
        private void Trim(){items.RemoveAll(delegate(LegacyNotification item){return item.Dismissed;});while(items.Count>100){int index=items.FindLastIndex(delegate(LegacyNotification item){return !item.Ongoing;});if(index<0)break;items.RemoveAt(index);}}
    }
}
