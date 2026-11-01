using System;using OsuLegacy;
class NotificationTests {
    static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main(){
        var center=new NotificationCenter();var info=center.Post("a","Imported","One set",NotificationKind.Success,false,null);
        Check(center.UnreadCount==1,"Unread badge counts retained alerts");
        center.Post("a","Imported","One set",NotificationKind.Success,false,null);Check(center.Items.Count==1,"Duplicate alert coalesces");
        Check(center.Toast(10,true)==null && !info.ToastShown,"Gameplay defers routine toast without consuming it");
        Check(center.Toast(20,false)==info && info.ToastUntil==26,"Deferred toast gets its full visible lifetime");
        Check(center.Toast(25,false)==info && center.Toast(27,false)==null,"Toast expires but remains in history");Check(center.UnreadCount==1,"Toast timeout does not mark history read");
        var critical=center.Post("critical","Error","Critical",NotificationKind.Error,true,null);Check(center.Toast(30,true)==critical && critical.ToastUntil==40,"Critical errors show during gameplay with longer lifetime");
        center.Progress("import","Importing","1 / 3",.33);center.Progress("import","Importing","2 / 3",.66);
        Check(center.Items.Count==3 && center.Items[0].Ongoing && center.Items[0].Progress==.66,"Progress updates one ongoing card");
        Check(!center.Dismiss(center.Items[0]),"Running task cannot be dismissed");
        center.MarkRead();Check(center.UnreadCount==0 && center.Toast(31,false)==null,"Opening history marks completed alerts read");
        center.Post("critical","Error","Critical",NotificationKind.Error,true,null);Check(center.UnreadCount==1 && center.Toast(32,true)==critical,"Acknowledged error can alert again if it recurs");
        center.ClearCompleted();Check(center.Items.Count==1 && center.Items[0].Ongoing,"Clear all preserves ongoing work");
        var complete=center.Post("import","Imported","3 sets",NotificationKind.Success,false,null);Check(!complete.Ongoing && center.UnreadCount==1,"Progress transitions to completed alert");
        center.Progress("import","Importing","Starting again",-1);Check(center.Items.Count==1 && center.Items[0].Ongoing,"New import reuses completed progress key safely");
        center.RemoveTask("import");Check(center.UnreadCount==0,"Silent no-change scan removes progress without alert");
        for(int i=0;i<130;i++)center.Post("item"+i,"Title","Message",NotificationKind.Info,false,null);Check(center.Items.Count==100,"History remains bounded");
        Check(center.Dismiss(center.Items[0]) && !center.Dismiss(center.Items[0]),"Dismissal is idempotent");
        int actions=0;var retry=center.Post("retry","Playback failed","Decode error",NotificationKind.Error,false,delegate {actions++;});retry.Activated();Check(actions==1,"Recovery action is retained");
        Console.WriteLine("PASS: "+checks+" notification unread, dedupe, suppression, timeout, progress, dismissal, bounds and recovery checks");
    }
}
