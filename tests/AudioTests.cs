using System;
using System.IO;
using System.Threading;
using ShuffleDrop;
using NAudio.Wave;
static class AudioTests
{
    static void Check(bool result,string text) { if(!result)throw new Exception(text); Console.WriteLine("PASS AUDIO: "+text); }
    [STAThread] static int Main(string[] args)
    {
        var d=Device.Open(args[0],false); var db=ShuffleDatabase.Parse(Device.ReadDb(d.DbPath));
        string file=d.PathOf(ShuffleDatabase.TrackPath(db.Tracks[0]).Substring(1));
        using(var decoded=new MediaFoundationReader(file))
        {
            byte[] pcm=new byte[32768]; int read=decoded.Read(pcm,0,pcm.Length);
            Check(read>0 && decoded.TotalTime.TotalSeconds>0,"real iPod MP3 decodes to PCM");
        }
        using(var player=new AudioPreview())
        {
            player.Volume=0; player.Play(file); Thread.Sleep(600);
            Check(player.Playing && player.Position.TotalMilliseconds>0 && player.TakeError()==null,"WASAPI output advances with muted test playback");
            player.PauseResume(); Check(player.Paused,"pause"); player.Seek(0.5); Check(player.Position.TotalSeconds>=player.Duration.TotalSeconds*0.49,"seek");
            player.PauseResume(); Thread.Sleep(300); Check(player.Playing && player.TakeError()==null,"resume"); player.Dispose(); Check(!player.Opened,"stop releases output");
            player.Play(file); Thread.Sleep(200); Check(player.TakeError()==null,"repeated play");
        }
        return 0;
    }
}
