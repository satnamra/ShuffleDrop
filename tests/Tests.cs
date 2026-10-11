using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ShuffleDrop;

internal static class Tests
{
    static int passed;
    static string root;
    static void Assert(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    static void Reject(Action action, string name) { try { action(); } catch { Assert(true, name); return; } throw new Exception("FAIL (accepted): " + name); }
    static void Put(byte[] b, int pos, uint val) { BitConverter.GetBytes(val).CopyTo(b, pos); }
    static byte[] Empty(bool legacy)
    {
        if (legacy) return new byte[] { 0,0,0,1,8,0,0,0,18,0,0,0,0,0,0,0,0,0 };
        // Independently laid out empty master playlist fixture from the published format.
        byte[] b = new byte[152];
        Encoding.ASCII.GetBytes("bdhs").CopyTo(b,0); Put(b,4,0x02000003); Put(b,8,64); Put(b,16,1); Put(b,36,64); Put(b,40,84);
        Encoding.ASCII.GetBytes("hths").CopyTo(b,64); Put(b,68,20);
        Encoding.ASCII.GetBytes("hphs").CopyTo(b,84); Put(b,88,24); Put(b,92,1);
        b[96]=255; b[97]=255; b[98]=1; b[100]=255; b[101]=255; Put(b,104,108);
        Encoding.ASCII.GetBytes("lphs").CopyTo(b,108); Put(b,112,44); Put(b,132,1);
        return b;
    }
    static string Audio(string name, byte payload)
    {
        string path = Path.Combine(root,name); byte[] b = new byte[417 * 40];
        for(int i=0;i<40;i++) { int p=i*417; b[p]=255; b[p+1]=251; b[p+2]=144; b[p+3]=0; b[p+100]=payload; }
        File.WriteAllBytes(path,b); return path;
    }
    static Device Fake(bool legacy, string name)
    {
        string path=Path.Combine(root,name); Directory.CreateDirectory(Path.Combine(path,"iPod_Control","Device"));
        Directory.CreateDirectory(Path.Combine(path,"iPod_Control","iTunes")); File.WriteAllBytes(Path.Combine(path,"iPod_Control","iTunes","iTunesSD"),Empty(legacy));
        File.WriteAllBytes(Path.Combine(path,"iPod_Control","iTunes","iTunesShuffle"),new byte[] {1,2,3});
        File.WriteAllBytes(Path.Combine(path,"iPod_Control","iTunes","iTunesStats"),new byte[] {4,5,6});
        return Device.Open(path,true);
    }
    static Transfer Engine(Device d) { var t=new Transfer(d,(p,s)=>{},()=>false); t.BackupRoot=Path.Combine(root,"backups"); return t; }
    static void Suite(bool legacy)
    {
        string label=legacy?"legacy":"modern"; Device d=Fake(legacy,label); var engine=Engine(d);
        byte[] original=File.ReadAllBytes(d.DbPath);
        string a=Audio(label+"-Žąsis.mp3",3), b=Audio(label+"-second.mp3",7);
        Assert(Mp3.Duration(a)==1045,"MP3 frame-derived duration");
        var r=engine.Add(new[]{a,a});
        Assert(r.Added==1 && r.Skipped==1,label+" transfer and in-batch duplicate");
        var db=ShuffleDatabase.Parse(File.ReadAllBytes(d.DbPath));
        Assert(db.Tracks.Count==1 && db.Legacy==legacy,label+" database format and count");
        string music=d.PathOf(ShuffleDatabase.TrackPath(db.Tracks[0]).Substring(1));
        Assert(File.ReadAllBytes(music).SequenceEqual(File.ReadAllBytes(a)),label+" verified audio copy");
        Assert(File.ReadAllBytes(Path.Combine(r.Backup,"iTunesSD")).SequenceEqual(original),label+" exact computer backup");
        Assert(!File.Exists(d.PathOf("iPod_Control/iTunes/iTunesShuffle")) && !File.Exists(d.PathOf("iPod_Control/iTunes/iTunesStats")),label+" playback caches invalidated");
        byte[] first=File.ReadAllBytes(d.DbPath), firstRecord=db.Tracks[0];
        var duplicate=engine.Add(new[]{a});
        Assert(duplicate.Added==0 && duplicate.Skipped==1 && File.ReadAllBytes(d.DbPath).SequenceEqual(first),label+" repeat transfer leaves database untouched");
        engine.Add(new[]{b}); db=ShuffleDatabase.Parse(File.ReadAllBytes(d.DbPath));
        Assert(db.Tracks.Count==2 && db.Tracks[0].SequenceEqual(firstRecord),label+" existing track metadata preserved");
        engine.Restore(); Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(first),label+" undo restores exact previous database");
        foreach(string phase in new[]{"before-marker","before-write","after-write","after-caches","before-finish"})
        {
            Device fail=Fake(legacy,label+"-"+phase); byte[] before=File.ReadAllBytes(fail.DbPath); var tx=Engine(fail);
            tx.TestFault=p=>{if(p==phase)throw new IOException("Injected failure");};
            Reject(()=>tx.Add(new[]{a}),label+" failure injected "+phase);
            Assert(File.ReadAllBytes(fail.DbPath).SequenceEqual(before),label+" rollback "+phase);
            Assert(File.ReadAllBytes(fail.PathOf("iPod_Control/iTunes/iTunesShuffle")).SequenceEqual(new byte[]{1,2,3}) && File.ReadAllBytes(fail.PathOf("iPod_Control/iTunes/iTunesStats")).SequenceEqual(new byte[]{4,5,6}),label+" cache rollback "+phase);
        }
        Device crash=Fake(legacy,label+"-crash"); var crashEngine=Engine(crash); string id=Guid.NewGuid().ToString("N");
        string state=crash.PathOf("iPod_Control/ShuffleDrop"); string path=Path.Combine(state,"transactions",id); Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path,"before.db"),Empty(legacy));
        File.WriteAllBytes(Path.Combine(path,"iTunesShuffle"),new byte[]{1,2,3}); File.WriteAllBytes(Path.Combine(path,"iTunesStats"),new byte[]{4,5,6});
        File.WriteAllText(Path.Combine(state,"pending.txt"),id); File.WriteAllBytes(crash.DbPath,new byte[]{0,1});
        Device.Open(crash.Root,true); crashEngine.Restore();
        Assert(File.ReadAllBytes(crash.DbPath).SequenceEqual(Empty(legacy)) && !crashEngine.HasPending,label+" reconnect recovers interrupted write");
        var canceled=new Transfer(crash,(p,s)=>{},()=>true); canceled.BackupRoot=Path.Combine(root,"backups");
        Reject(()=>canceled.Add(new[]{a}),label+" cancel"); Assert(File.ReadAllBytes(crash.DbPath).SequenceEqual(Empty(legacy)),label+" cancel preserves database");
        string escaping=Path.Combine(root,"outside.mp3"); Reject(()=>d.PathOf("../outside.mp3"),label+" path traversal blocked");
    }
    static void ManagerSuite(bool legacy)
    {
        string label=legacy?"legacy":"modern";
        var d=Fake(legacy,label+"-manager"); var tx=Engine(d);
        string a=Audio(label+"-manager-a.mp3",21), b=Audio(label+"-manager-b.mp3",22), c=Audio(label+"-manager-c.mp3",23);
        using(var tag=TagLib.File.Create(a)) { tag.Tag.Title="Žąsis / testas"; tag.Tag.Performers=new[]{"Atlikėjas"}; tag.Tag.Album="Albumas"; tag.Save(); }
        tx.Add(new[]{a,b,c}); byte[] original=File.ReadAllBytes(d.DbPath);
        var library=LibrarySnapshot.Read(d,(p,s)=>{},()=>false);
        Assert(library.Tracks.Count==3 && library.Tracks[0].Title=="Žąsis / testas" && library.Tracks[0].Artist=="Atlikėjas" && library.Tracks[0].Album=="Albumas",label+" library reads Unicode tags");
        Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(original),label+" library is read-only");
        string[] paths=library.Tracks.Select(t=>t.Relative).ToArray(); string[] hashes=library.Tracks.Select(t=>Transfer.Hash(t.FullPath)).ToArray();
        tx.Manage(original,paths.Reverse().ToArray(),false);
        Assert(ShuffleDatabase.Parse(File.ReadAllBytes(d.DbPath)).Tracks.Select(ShuffleDatabase.TrackPath).SequenceEqual(paths.Reverse()),label+" saves play order");
        Reject(()=>tx.Manage(original,paths,false),label+" stale snapshot rejected");
        tx.Restore(); Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(original),label+" undo order exact");
        Reject(()=>tx.Manage(original,new[]{paths[0],paths[0],paths[2]},false),label+" duplicate order rejected");
        Reject(()=>tx.Manage(original,new[]{paths[0]},false),label+" reorder cannot delete");
        string output=Path.Combine(root,label+"-export"); Directory.CreateDirectory(output);
        Assert(LibrarySnapshot.Export(d,library.Tracks,output,(p,s)=>{},()=>false)==3,label+" exports all songs");
        Assert(Directory.GetFiles(output).Select(Transfer.Hash).OrderBy(x=>x).SequenceEqual(hashes.OrderBy(x=>x)),label+" exported bytes match");
        LibrarySnapshot.Export(d,library.Tracks,output,(p,s)=>{},()=>false);
        Assert(Directory.GetFiles(output).Length==6,label+" repeated export does not overwrite");
        Reject(()=>LibrarySnapshot.Export(d,library.Tracks,d.Root,(p,s)=>{},()=>false),label+" export to device blocked");
        foreach(string phase in new[]{"before-marker","after-write","after-audio-delete","before-finish"})
        {
            tx.TestFault=p=>{if(p==phase)throw new IOException("Injected");};
            Reject(()=>tx.Manage(original,new[]{paths[1]},true),label+" delete failure "+phase);
            Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(original) && library.Tracks.Select(t=>Transfer.Hash(t.FullPath)).SequenceEqual(hashes),label+" delete rollback restores database and audio "+phase);
        }
        tx.TestFault=null; tx.Manage(original,new[]{paths[1]},true);
        Assert(ShuffleDatabase.Parse(File.ReadAllBytes(d.DbPath)).Tracks.Count==1 && !File.Exists(library.Tracks[0].FullPath) && File.Exists(library.Tracks[1].FullPath) && !File.Exists(library.Tracks[2].FullPath),label+" delete frees only selected audio");
        string id=File.ReadAllText(d.PathOf("iPod_Control/ShuffleDrop/last.txt")); string backup=Path.Combine(tx.BackupRoot,"Removed",id,"0.audio"); byte[] saved=File.ReadAllBytes(backup); File.WriteAllBytes(backup,new byte[]{1});
        byte[] deleted=File.ReadAllBytes(d.DbPath); Reject(()=>tx.Restore(),label+" corrupt audio backup blocks undo");
        Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(deleted),label+" blocked undo preserves current database");
        File.WriteAllBytes(backup,saved); tx.Restore();
        Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(original) && library.Tracks.Select(t=>Transfer.Hash(t.FullPath)).SequenceEqual(hashes),label+" undo delete restores exact audio and database");
        tx.Manage(original,new string[0],true); Assert(ShuffleDatabase.Parse(File.ReadAllBytes(d.DbPath)).Tracks.Count==0,label+" delete all valid empty library");
        tx.Restore(); Assert(File.ReadAllBytes(d.DbPath).SequenceEqual(original),label+" undo delete all");
    }
    static int Main(string[] args)
    {
        try
        {
            root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"run",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Suite(false); Suite(true); ManagerSuite(false); ManagerSuite(true);
            var playlists=ShuffleDatabase.Parse(Empty(false)); playlists.Append("/iPod_Control/Music/F00/old.mp3",2000,new byte[8]);
            byte[] custom=new byte[48]; Encoding.ASCII.GetBytes("lphs").CopyTo(custom,0); Put(custom,4,48); Put(custom,8,1); Put(custom,12,1); Put(custom,16,12345); Put(custom,24,2); Put(custom,44,0);
            playlists.Playlists.Add(custom); playlists=ShuffleDatabase.Parse(playlists.Serialize());
            playlists.Append("/iPod_Control/Music/F00/new.mp3",2000,new byte[8]); var round=ShuffleDatabase.Parse(playlists.Serialize());
            Assert(round.Playlists[1].SequenceEqual(custom) && ShuffleDatabase.U(round.Playlists[0],8)==2,"custom playlist and VoiceOver ID preserved while master grows");
            round.KeepOrder(new[]{1,0}); round=ShuffleDatabase.Parse(round.Serialize());
            Assert(ShuffleDatabase.U(round.Playlists[1],44)==1 && ShuffleDatabase.U(round.Playlists[1],16)==12345,"custom playlist remaps reordered track and preserves VoiceOver ID");
            round.KeepOrder(new[]{0}); round=ShuffleDatabase.Parse(round.Serialize());
            Assert(ShuffleDatabase.U(round.Playlists[1],8)==0 && ShuffleDatabase.U(round.Playlists[0],8)==1,"delete removes custom playlist entries and updates master");
            string bad=Path.Combine(root,"fake.mp3"); File.WriteAllText(bad,"This is not audio"); Reject(()=>Mp3.Duration(bad),"renamed non-audio rejected");
            string cut=Audio("truncated.mp3",1); using(var f=new FileStream(cut,FileMode.Open,FileAccess.Write))f.SetLength(700); Reject(()=>Mp3.Duration(cut),"truncated audio rejected");
            byte[] invalid=Empty(false); Put(invalid,104,uint.MaxValue); Reject(()=>ShuffleDatabase.Parse(invalid),"out-of-bounds database offset rejected");
            byte[] raw=Empty(false); for(int i=0;i<raw.Length;i++) { byte[] shortDb=raw.Take(i).ToArray(); try { ShuffleDatabase.Parse(shortDb); throw new Exception("Accepted truncated database"); } catch(InvalidDataException){} }
            Assert(true,"every truncated modern header rejected");
            Device wrong=Fake(false,"wrong-state"); string idEscape=wrong.PathOf("iPod_Control/ShuffleDrop"); Directory.CreateDirectory(idEscape); File.WriteAllText(Path.Combine(idEscape,"pending.txt"),"../../outside");
            byte[] unchanged=File.ReadAllBytes(wrong.DbPath); Reject(()=>Engine(wrong).Restore(),"unsafe recovery identifier blocked"); Assert(File.ReadAllBytes(wrong.DbPath).SequenceEqual(unchanged),"invalid recovery leaves database untouched");
            Reject(()=>Device.Open(Path.GetPathRoot(Environment.SystemDirectory),false),"system drive blocked");
            if(args.Length>0)
            {
                byte[] original=File.ReadAllBytes(args[0]); var deviceDb=ShuffleDatabase.Parse(original);
                Assert(deviceDb.Serialize().SequenceEqual(original),"REAL DEVICE snapshot lossless round trip (read-only)");
                Console.WriteLine("Real device format="+(deviceDb.Legacy?"1/2":"3/4")+" tracks="+deviceDb.Tracks.Count);
                var old=deviceDb.Tracks.Select(t=>(byte[])t.Clone()).ToList(); deviceDb.Append("/iPod_Control/Music/SDrop/test.mp3",1000,new byte[8]);
                var changed=ShuffleDatabase.Parse(deviceDb.Serialize());
                Assert(changed.Tracks.Count==old.Count+1 && old.Select((t,i)=>t.SequenceEqual(changed.Tracks[i])).All(x=>x),"REAL DEVICE snapshot append preserves all existing records");
            }
            if(args.Length>1)
            {
                var actual=Device.Open(args[1],false); var db=ShuffleDatabase.Parse(Device.ReadDb(actual.DbPath)); int valid=0, missing=0, unsupported=0;
                foreach(byte[] t in db.Tracks)
                {
                    string p=actual.PathOf(ShuffleDatabase.TrackPath(t).Substring(1));
                    if(!File.Exists(p)) {missing++;continue;}
                    if(valid+unsupported < 3 && Path.GetExtension(p).Equals(".mp3",StringComparison.OrdinalIgnoreCase))
                    { Console.WriteLine("Reading representative MP3 sample "+(valid+unsupported+1)); try { Mp3.Duration(p); valid++; } catch(Exception error) { unsupported++; Console.WriteLine(error.Message); } }
                }
                Assert(missing==0,"REAL DEVICE all indexed files exist (read-only)");
                Console.WriteLine("Real MP3 accepted="+valid+" rejected="+unsupported);
                Assert(unsupported==0 && valid>0,"REAL DEVICE representative MP3 parser compatibility (read-only)");
                byte[] before=Device.ReadDb(actual.DbPath); var live=LibrarySnapshot.Read(actual,(p,s)=>{},()=>false);
                Assert(live.Tracks.Count==db.Tracks.Count && live.Tracks.All(t=>!t.Missing) && live.Tracks.Any(t=>t.Artist.Length>0),"REAL DEVICE full tagged library (read-only)");
                Assert(Device.ReadDb(actual.DbPath).SequenceEqual(before),"REAL DEVICE library database unchanged");
            }
            Console.WriteLine("TOTAL PASS: "+passed); return 0;
        }
        catch(Exception e) {Console.WriteLine(e.ToString());return 1;}
    }
}
