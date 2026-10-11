using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace ShuffleDrop
{
    internal sealed class LibraryTrack
    {
        public string Relative, FullPath, Title, Artist = "", Album = "", Error = "";
        public long Size; public TimeSpan Duration; public bool Missing;
        public int Position;
        public string DurationText { get { return Duration.TotalSeconds > 0 ? ((int)Duration.TotalMinutes) + ":" + Duration.Seconds.ToString("00") : "—"; } }
    }
    internal sealed class LibrarySnapshot
    {
        public Device Device;
        public byte[] Database;
        public readonly List<LibraryTrack> Tracks = new List<LibraryTrack>();
        public static LibrarySnapshot Read(Device d, Action<int, string> progress, Func<bool> cancelled)
        {
            var s = new LibrarySnapshot { Device = d, Database = Device.ReadDb(d.DbPath) };
            var db = ShuffleDatabase.Parse(s.Database);
            for (int i = 0; i < db.Tracks.Count; i++)
            {
                if (cancelled()) throw new OperationCanceledException();
                string relative = ShuffleDatabase.TrackPath(db.Tracks[i]); string path = d.PathOf(relative.Substring(1));
                var t = new LibraryTrack { Relative = relative, FullPath = path, Title = Path.GetFileNameWithoutExtension(path), Position = i + 1 };
                if (!File.Exists(path)) { t.Missing = true; t.Error = "Trūksta failo"; }
                else
                {
                    t.Size = new FileInfo(path).Length;
                    try
                    {
                        using (var tag = TagLib.File.Create(path))
                        {
                            if (!String.IsNullOrWhiteSpace(tag.Tag.Title)) t.Title = tag.Tag.Title.Trim();
                            t.Artist = String.Join(", ", tag.Tag.Performers ?? new string[0]);
                            t.Album = tag.Tag.Album ?? ""; t.Duration = tag.Properties.Duration;
                        }
                    }
                    catch (IOException) { t.Error = "Nepavyko perskaityti žymų"; }
                    catch (UnauthorizedAccessException) { t.Error = "Failas nepasiekiamas"; }
                    catch (TagLib.CorruptFileException) { t.Error = "Nepavyko perskaityti žymų"; }
                    catch (TagLib.UnsupportedFormatException) { t.Error = "Neatpažintas garso formatas"; }
                }
                s.Tracks.Add(t); progress((i + 1) * 100 / Math.Max(1, db.Tracks.Count), "Skaitoma biblioteka: " + (i + 1) + "/" + db.Tracks.Count);
            }
            return s;
        }
        public static string ExportName(LibraryTrack track)
        {
            string title = String.IsNullOrWhiteSpace(track.Artist) ? track.Title : track.Artist + " - " + track.Title;
            foreach (char c in Path.GetInvalidFileNameChars()) title = title.Replace(c, '_');
            title = title.Trim().TrimEnd('.'); if (title.Length > 120) title = title.Substring(0, 120).TrimEnd('.');
            if (title.Length == 0) title = "Daina";
            // Prefix makes Windows reserved names (CON, AUX, COM1...) harmless as well.
            return track.Position.ToString("000") + " - " + title + Path.GetExtension(track.FullPath);
        }
        public static int Export(Device device, IList<LibraryTrack> tracks, string directory, Action<int, string> progress, Func<bool> cancel)
        {
            string target = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
            if (target.StartsWith(device.Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Eksportui pasirink aplanką kompiuteryje, ne tame pačiame iPod.");
            if (!Directory.Exists(target)) throw new IOException("Eksporto aplankas nepasiekiamas.");
            int count = 0;
            foreach (var track in tracks)
            {
                if (cancel()) throw new OperationCanceledException("Eksportas atšauktas. Jau nukopijuoti failai išsaugoti.");
                string source = device.PathOf(track.Relative.Substring(1));
                if (!File.Exists(source)) throw new IOException("Dainos failas nepasiekiamas: " + track.Title);
                string name = ExportName(track), dest = Path.Combine(target, name); int suffix = 2;
                while (File.Exists(dest)) dest = Path.Combine(target, Path.GetFileNameWithoutExtension(name) + " (" + suffix++ + ")" + Path.GetExtension(name));
                progress(count * 100 / Math.Max(1, tracks.Count), "Išsaugoma: " + track.Title);
                Transfer.CopyVerified(source, dest, () => { if (cancel()) throw new OperationCanceledException(); }); count++;
            }
            return count;
        }
    }
}
