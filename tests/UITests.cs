using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ShuffleDrop;
static class UITests
{
    static object Get(object o,string n) { return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o); }
    static void Call(object o,string n,params object[] a) { o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a); }
    static void Check(bool x,string n) { if(!x)throw new Exception(n); Console.WriteLine("PASS UI: "+n); }
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();
        using(var f=new MainForm())
        {
            f.PreparePreview(); f.Show(); Application.DoEvents();
            var list=(ListView)Get(f,"library"); var search=(Krypton.Toolkit.KryptonTextBox)Get(f,"search"); var devices=(Krypton.Toolkit.KryptonComboBox)Get(f,"devices");
            Check(list.Items.Count>=2,"connected device has at least two songs for UI smoke tests"); Check(devices.SelectedIndex>=0,"device selected");
            string artist=list.Items.Cast<ListViewItem>().Select(r=>r.SubItems[2].Text).FirstOrDefault(a=>!String.IsNullOrWhiteSpace(a));
            Check(artist!=null,"device has an artist tag for search test");
            search.Text=artist; Check(list.Items.Count>0 && list.Items.Cast<ListViewItem>().All(r=>r.SubItems[2].Text.IndexOf(artist,StringComparison.OrdinalIgnoreCase)>=0),"artist search filters library");
            search.Text="nothingshouldmatchthis123"; Check(list.Items.Count==0,"empty search results"); search.Clear();
            string first=list.Items[0].SubItems[1].Text; list.Items[0].Selected=true; Call(f,"MoveSelected",1);
            Check(list.Items[1].SubItems[1].Text==first && ((UiButton)Get(f,"saveOrder")).Enabled,"move down keeps selection and enables save");
            Check(!((UiButton)Get(f,"delete")).Enabled,"unsaved order blocks delete"); Call(f,"MoveSelected",-1); Check(list.Items[0].SubItems[1].Text==first,"move up restores order in view");
            f.GetType().GetField("dirty",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,false);
            ((PageHost)Get(f,"tabs")).SelectedIndex=1; Call(f,"AddPaths",new object[]{new[]{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"missing.mp3")}});
            Check(((ListView)Get(f,"queue")).Items.Count==0,"missing import file skipped");
            f.SetTheme(false,false); Check(list.BackColor==System.Drawing.Color.White,"light mode changes library surface"); f.SetTheme(true,false); Check(list.BackColor==Theme.Surface && list.BackColor!=System.Drawing.Color.White,"dark mode changes library surface");
            Call(f,"SwitchPage",false); Check(((PageHost)Get(f,"tabs")).SelectedTab==(ContentPage)Get(f,"libraryTab"),"sidebar returns to library");
            Check(((LinkLabel)Get(f,"batteryInfo")).Text.Contains("unavailable"),"battery limitation explicit"); f.Close();
        }
        return 0;
    }
}
