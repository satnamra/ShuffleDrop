using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ShuffleDrop
{
    internal sealed class Device
    {
        public string Root, Label;
        public int Tracks;
        public bool Legacy;
        public long Free;
        internal bool IsTest;
        public override string ToString() { return Label; }
        public string PathOf(string relative)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Kelias yra už grotuvo ribų.");
            for (string p = path; !String.Equals(p, Root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); p = Path.GetDirectoryName(p))
            {
                if (String.IsNullOrEmpty(p)) break;
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Grotuvo aplanke yra nuoroda į kitą vietą. Perkėlimas sustabdytas.");
            }
            return path;
        }
        public string DbPath { get { return PathOf("iPod_Control/iTunes/iTunesSD"); } }
        public string StatePath { get { return PathOf("iPod_Control/ShuffleDrop"); } }
        public static Device Open(string root, bool test)
        {
            string full = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            if (!test)
            {
                if (!String.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase) || full.StartsWith("\\\\"))
                    throw new IOException("Pasirink grotuvo diską.");
                if (String.Equals(full, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Sisteminis diskas negali būti naudojamas kaip „iPod“.");
                var drive = new DriveInfo(full);
                if (!drive.IsReady || (drive.DriveType != DriveType.Removable && drive.DriveType != DriveType.Fixed))
                    throw new IOException("Grotuvo diskas nepasiekiamas.");
                if (drive.DriveFormat != "FAT32" && drive.DriveFormat != "FAT") throw new IOException("Reikalingas FAT / FAT32 formato „iPod Shuffle“.");
            }
            var d = new Device { Root = full, IsTest = test };
            if (!Directory.Exists(d.PathOf("iPod_Control/Device"))) throw new IOException("Nerastas „iPod“ įrenginio aplankas.");
            if (File.Exists(d.PathOf("iPod_Control/ShuffleDrop/pending.txt")))
                d.Label = full + "  •  Reikia atkurti nutrūkusį perkėlimą";
            else
            {
                var db = ShuffleDatabase.Parse(ReadDb(d.DbPath)); d.Tracks = db.Tracks.Count;
                d.Legacy = db.Legacy;
                d.Label = full + "  •  " + new DriveInfo(Path.GetPathRoot(full)).VolumeLabel;
            }
            d.Free = new DriveInfo(Path.GetPathRoot(full)).AvailableFreeSpace;
            return d;
        }
        public static byte[] ReadDb(string path)
        {
            if (!File.Exists(path)) throw new IOException("Nerastas esamas „Shuffle“ muzikos sąrašas. Ši versija veikia su jau paruoštu grotuvu; jo neformatuoja ir neatkuria gamyklinės programinės įrangos.");
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("Muzikos sąrašas per didelis arba netinkamas.");
            return File.ReadAllBytes(path);
        }
        public static List<Device> Find()
        {
            var list = new List<Device>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            { try { list.Add(Open(drive.Name, false)); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { } }
            return list;
        }
    }

    internal sealed class TransferResult
    { public int Added, Skipped; public string Backup; }
    internal sealed class Song
    { public string Source, Relative, Hash; public uint Duration; public long Size; }
    internal sealed class Transfer
    {
        readonly Device device;
        readonly Action<int, string> report;
        readonly Func<bool> cancelled;
        internal Action<string> TestFault;
        internal string BackupRoot;
        public Transfer(Device d, Action<int, string> progress, Func<bool> cancel)
        {
            device = d; report = progress; cancelled = cancel;
            BackupRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShuffleDrop", "Backups");
        }
        void Check() { if (cancelled()) throw new OperationCanceledException("Perkėlimas atšauktas. Ankstesnis muzikos sąrašas išsaugotas."); }
        void Fault(string phase) { if (TestFault != null) TestFault(phase); }
        static bool Equal(byte[] a, byte[] b) { return a.SequenceEqual(b); }
        internal static string Hash(string path)
        {
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }
        static void Write(string path, byte[] bytes)
        { using (var f = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) { f.Write(bytes, 0, bytes.Length); f.Flush(true); } }
        FileStream Lock()
        {
            Device.Open(device.Root, device.IsTest);
            device.PathOf("iPod_Control/iTunes");
            return new FileStream(device.PathOf("iPod_Control/iTunes/ShuffleDrop.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        string Marker(string name) { return device.PathOf("iPod_Control/ShuffleDrop/" + name + ".txt"); }
        string Transaction(string id)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "N", out parsed)) throw new IOException("Netinkamas atkūrimo įrašas.");
            return device.PathOf("iPod_Control/ShuffleDrop/transactions/" + id);
        }
        string ReadId(string marker)
        {
            if (new FileInfo(marker).Length > 128) throw new IOException("Netinkamas atkūrimo įrašas.");
            string id = File.ReadAllText(marker).Trim(); Transaction(id); return id;
        }
        string Commit(byte[] before, byte[] after, IList<string> removed = null)
        {
            ShuffleDatabase.Parse(before); ShuffleDatabase.Parse(after);
            if (File.Exists(Marker("pending"))) throw new IOException("Pirmiausia paspausk „Atkurti sąrašą“ – rastas nutrūkęs perkėlimas.");
            if (!Equal(Device.ReadDb(device.DbPath), before)) throw new IOException("Kita programa pakeitė muzikos sąrašą. Uždaryk „iTunes“ ir bandyk iš naujo.");
            string id = Guid.NewGuid().ToString("N"), tx = Transaction(id);
            Directory.CreateDirectory(tx);
            Write(Path.Combine(tx, "before.db"), before); Write(Path.Combine(tx, "after.db"), after);
            // libgpod invalidates these caches after changing iTunesSD. Keep exact before
            // images so rollback also restores the device's original shuffle/statistics.
            foreach (string name in CacheNames)
            {
                string path = device.PathOf("iPod_Control/iTunes/" + name);
                if (File.Exists(path)) Write(Path.Combine(tx, name), Device.ReadDb(path));
                else Write(Path.Combine(tx, name + ".absent"), new byte[0]);
            }
            string local = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + id);
            Directory.CreateDirectory(local); Write(Path.Combine(local, "iTunesSD"), before);
            foreach (string name in CacheNames)
                if (File.Exists(Path.Combine(tx, name))) File.Copy(Path.Combine(tx, name), Path.Combine(local, name));
            File.WriteAllText(Path.Combine(local, "ATKURIMAS.txt"), "Ankstesnis muzikos sąrašas. Atkūrimui naudok programos mygtuką „Atkurti sąrašą“.\r\nGrotuvas: " + device.Root + "\r\nOperacija: " + id, Encoding.UTF8);
            if (removed != null && removed.Count > 0)
            {
                string audioBackup = Path.Combine(BackupRoot, "Removed", id); Directory.CreateDirectory(audioBackup);
                var lines = new List<string>();
                for (int i = 0; i < removed.Count; i++)
                {
                    Check(); string source = device.PathOf(removed[i].Substring(1));
                    if (!File.Exists(source)) continue;
                    report(i * 75 / removed.Count, "Saugoma šalinamos dainos kopija " + (i + 1) + "/" + removed.Count);
                    string dest = Path.Combine(audioBackup, lines.Count + ".audio");
                    CopyVerified(source, dest, Check);
                    lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(removed[i])) + "\t" + Hash(dest));
                }
                Write(Path.Combine(tx, "removed.tsv"), Encoding.UTF8.GetBytes(String.Join("\n", lines)));
            }
            Fault("before-marker"); Check();
            using (var marker = new FileStream(Marker("pending"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { byte[] data = Encoding.ASCII.GetBytes(id); marker.Write(data, 0, data.Length); marker.Flush(true); }
            // Leave the marker until the new database and undo pointer are durable. A power loss
            // at any point before marker removal is recovered from the on-device before image.
            try
            {
                Fault("before-write"); Write(device.DbPath, after); Fault("after-write");
                if (!Equal(Device.ReadDb(device.DbPath), after)) throw new IOException("Nepavyko patikrinti įrašyto muzikos sąrašo.");
                foreach (string name in CacheNames) File.Delete(device.PathOf("iPod_Control/iTunes/" + name));
                Fault("after-caches");
                DeleteBackedUpAudio(tx, id); Fault("after-audio-delete");
                Write(Marker("last"), Encoding.ASCII.GetBytes(id));
                Fault("before-finish"); File.Delete(Marker("pending"));
            }
            catch
            {
                // Preserve the pending marker if rollback itself cannot finish (e.g. unplug).
                try { RestoreAudio(tx, id); Write(device.DbPath, before); RestoreCaches(tx); if (Equal(Device.ReadDb(device.DbPath), before)) File.Delete(Marker("pending")); }
                catch { }
                throw;
            }
            return local;
        }
        internal static void CopyVerified(string source, string dest, Action check)
        {
            string partial = dest + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[262144]; int n;
                        while ((n = input.Read(buffer, 0, buffer.Length)) > 0) { check(); output.Write(buffer, 0, n); }
                        output.Flush(true);
                    }
                    input.Position = 0;
                    using (var sha = SHA256.Create())
                    { string expected = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant(); if (Hash(partial) != expected) throw new IOException("Nukopijuotas failas nesutampa."); }
                }
                File.Move(partial, dest);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }
        List<string[]> RemovedManifest(string tx)
        {
            string path = Path.Combine(tx, "removed.tsv"); var result = new List<string[]>();
            if (!File.Exists(path)) return result;
            byte[] bytes = Device.ReadDb(path);
            var before = ShuffleDatabase.Parse(Device.ReadDb(Path.Combine(tx, "before.db")));
            var indexed = new HashSet<string>(before.Tracks.Select(ShuffleDatabase.TrackPath), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in Encoding.UTF8.GetString(bytes).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('\t'); if (parts.Length != 2 || parts[1].Length != 64 || parts[1].Any(c => !Uri.IsHexDigit(c))) throw new IOException("Netinkama dainų atkūrimo kopija.");
                string relative = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                if (!indexed.Contains(relative) || !seen.Add(relative)) throw new IOException("Atkūrimo kopija neatitinka muzikos sąrašo.");
                device.PathOf(relative.Substring(1)); result.Add(new[] { relative, parts[1] });
            }
            return result;
        }
        void DeleteBackedUpAudio(string tx, string id)
        {
            var entries = RemovedManifest(tx);
            for (int i = 0; i < entries.Count; i++)
            {
                string path = device.PathOf(entries[i][0].Substring(1));
                string copy = Path.Combine(BackupRoot, "Removed", id, i + ".audio");
                if (!File.Exists(copy) || Hash(copy) != entries[i][1]) throw new IOException("Nepavyko patikrinti atsarginės dainos kopijos. Failas nebus šalinamas.");
                if (File.Exists(path)) { if (Hash(path) != entries[i][1]) throw new IOException("Dainos failas pasikeitė. Šalinimas sustabdytas."); File.Delete(path); }
            }
        }
        void RestoreAudio(string tx, string id)
        {
            var entries = RemovedManifest(tx);
            // Validate all required backups before copying or changing the database.
            for (int i = 0; i < entries.Count; i++)
            {
                string dest = device.PathOf(entries[i][0].Substring(1));
                if (File.Exists(dest)) { if (Hash(dest) != entries[i][1]) throw new IOException("Dainos vietoje yra kitas failas. Atkūrimas sustabdytas."); continue; }
                string copy = Path.Combine(BackupRoot, "Removed", id, i + ".audio");
                if (!File.Exists(copy) || Hash(copy) != entries[i][1]) throw new IOException("Trūksta pašalintos dainos kopijos. Atkurk naudodamas tą patį kompiuterį ir Windows paskyrą.");
            }
            for (int i = 0; i < entries.Count; i++)
            {
                string dest = device.PathOf(entries[i][0].Substring(1));
                if (File.Exists(dest)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                CopyVerified(Path.Combine(BackupRoot, "Removed", id, i + ".audio"), dest, () => { });
            }
        }
        public string Manage(byte[] expected, IList<string> orderedPaths, bool removeFiles)
        {
            using (Lock())
            {
                Check(); byte[] before = Device.ReadDb(device.DbPath);
                if (!Equal(before, expected)) throw new IOException("Biblioteka pasikeitė. Paspausk „Atnaujinti“ ir pakartok.");
                var db = ShuffleDatabase.Parse(before); var paths = db.Tracks.Select(ShuffleDatabase.TrackPath).ToList();
                var lookup = paths.Select((p, i) => new { p, i }).ToDictionary(x => x.p, x => x.i, StringComparer.OrdinalIgnoreCase);
                if (orderedPaths.Any(p => !lookup.ContainsKey(p))) throw new IOException("Pasirinkta daina nepriklauso šiai bibliotekai.");
                db.KeepOrder(orderedPaths.Select(p => lookup[p]).ToList());
                var keep = new HashSet<string>(orderedPaths, StringComparer.OrdinalIgnoreCase);
                var removed = paths.Where(p => !keep.Contains(p)).ToList();
                if (!removeFiles && removed.Count > 0) throw new IOException("Dainų tvarkos keitimas negali pašalinti dainų.");
                byte[] after = db.Serialize(); if (Equal(before, after)) return null;
                string backup = Commit(before, after, removeFiles ? removed : null);
                report(100, removeFiles ? "Pasirinktos dainos pašalintos. Kopijos išsaugotos kompiuteryje." : "Dainų tvarka išsaugota."); return backup;
            }
        }
        static readonly string[] CacheNames = { "iTunesShuffle", "iTunesStats" };
        void RestoreCaches(string tx)
        {
            foreach (string name in CacheNames)
            {
                string dest = device.PathOf("iPod_Control/iTunes/" + name), source = Path.Combine(tx, name);
                if (File.Exists(source)) { byte[] data = Device.ReadDb(source); Write(dest, data); if (!Equal(File.ReadAllBytes(dest), data)) throw new IOException("Nepavyko atkurti pagalbinių failų."); }
                else if (File.Exists(Path.Combine(tx, name + ".absent"))) File.Delete(dest);
                else throw new IOException("Trūksta atkūrimo kopijos dalies.");
            }
        }
        public TransferResult Add(IList<string> inputs)
        {
            using (Lock())
            {
                if (File.Exists(Marker("pending"))) throw new IOException("Rastas nutrūkęs perkėlimas. Paspausk „Atkurti sąrašą“.");
                byte[] original = Device.ReadDb(device.DbPath); var db = ShuffleDatabase.Parse(original);
                var result = new TransferResult(); var plans = new List<Song>(); var hashes = new HashSet<string>();
                var existing = new Dictionary<long, List<string>>();
                foreach (byte[] t in db.Tracks)
                {
                    Check(); string path = device.PathOf(ShuffleDatabase.TrackPath(t).Substring(1));
                    if (!File.Exists(path)) throw new IOException("Grotuvo sąraše trūksta dainos failo. Pirmiausia sutvarkyk esamą biblioteką: " + Path.GetFileName(path));
                    long size = new FileInfo(path).Length;
                    if (!existing.ContainsKey(size)) existing[size] = new List<string>();
                    existing[size].Add(path);
                }
                var existingHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < inputs.Count; i++)
                {
                    Check(); string source = Path.GetFullPath(inputs[i]);
                    report(i * 30 / Math.Max(1, inputs.Count), "Tikrinama: " + Path.GetFileName(source));
                    if (!String.Equals(Path.GetExtension(source), ".mp3", StringComparison.OrdinalIgnoreCase)) throw new IOException("Šioje versijoje galima pridėti MP3 failus.");
                    uint duration = Mp3.Duration(source); string hash = Hash(source); long size = new FileInfo(source).Length;
                    bool duplicate = !hashes.Add(hash);
                    List<string> candidates;
                    if (!duplicate && existing.TryGetValue(size, out candidates))
                    {
                        foreach (string path in candidates)
                        {
                            Check(); string stored;
                            if (!existingHashes.TryGetValue(path, out stored)) { stored = Hash(path); existingHashes[path] = stored; }
                            if (stored == hash) { duplicate = true; break; }
                        }
                    }
                    if (duplicate) { result.Skipped++; continue; }
                    plans.Add(new Song { Source = source, Hash = hash, Duration = duration, Size = size, Relative = "iPod_Control/Music/SDrop/" + hash + ".mp3" });
                }
                if (plans.Count == 0) return result;
                if (db.Tracks.Count + plans.Count > 30000) throw new IOException("Per daug dainų.");
                long needed = plans.Sum(p => p.Size) + 16L * 1024 * 1024;
                if (new DriveInfo(Path.GetPathRoot(device.Root)).AvailableFreeSpace < needed) throw new IOException("Grotuve nepakanka laisvos vietos. Atlaisvink vietos ir bandyk dar kartą.");
                foreach (Song song in plans)
                {
                    Check(); int index = plans.IndexOf(song);
                    string dest = device.PathOf(song.Relative); Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    if (File.Exists(dest))
                    { if (Hash(dest) != song.Hash) throw new IOException("Grotuve rastas nesutampantis failas. Esamas failas nebus perrašomas."); }
                    else
                    {
                        string partial = device.PathOf(song.Relative + "." + Guid.NewGuid().ToString("N") + ".part");
                        try
                        {
                            using (var input = new FileStream(song.Source, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[256 * 1024]; int read; long done = 0;
                                while ((read = input.Read(buf, 0, buf.Length)) > 0)
                                {
                                    Check(); output.Write(buf, 0, read); done += read;
                                    report(30 + (int)(60.0 * (index + Math.Min(1.0, (double)done / song.Size)) / plans.Count), "Kopijuojama " + (index + 1) + "/" + plans.Count + ": " + Path.GetFileName(song.Source));
                                }
                                output.Flush(true);
                            }
                            if (Hash(partial) != song.Hash) throw new IOException("Failas pasikeitė arba kopija nesutampa: " + Path.GetFileName(song.Source));
                            File.Move(partial, dest);
                        }
                        finally { if (File.Exists(partial)) File.Delete(partial); }
                    }
                    // Old track and playlist IDs stay intact, including existing VoiceOver files.
                    song.Duration = Mp3.Duration(dest);
                    byte[] id = new byte[8]; for (int k = 0; k < 8; k++) id[k] = Convert.ToByte(song.Hash.Substring(k * 2, 2), 16);
                    db.Append("/" + song.Relative, song.Duration, id);
                }
                Check(); report(94, "Išsaugomas muzikos sąrašas ir atsarginė kopija…");
                result.Backup = Commit(original, db.Serialize()); result.Added = plans.Count;
                report(100, "Muzika perkelta. Saugiai išimk grotuvą per „Windows“.");
                return result;
            }
        }
        public bool HasPending { get { return File.Exists(Marker("pending")); } }
        public void Restore()
        {
            using (Lock())
            {
                if (HasPending)
                {
                    string id = ReadId(Marker("pending")), tx = Transaction(id); byte[] before = Device.ReadDb(Path.Combine(tx, "before.db"));
                    ShuffleDatabase.Parse(before); RestoreAudio(tx, id); Write(device.DbPath, before);
                    RestoreCaches(tx);
                    if (!Equal(Device.ReadDb(device.DbPath), before)) throw new IOException("Nepavyko atkurti sąrašo. Vėl prijunk grotuvą ir pakartok.");
                    File.Delete(Marker("pending"));
                }
                else
                {
                    if (!File.Exists(Marker("last"))) throw new IOException("Šiame grotuve dar nėra „ShuffleDrop“ atkūrimo kopijos.");
                    string id = ReadId(Marker("last")), tx = Transaction(id);
                    byte[] before = Device.ReadDb(Path.Combine(tx, "before.db")), after = Device.ReadDb(Path.Combine(tx, "after.db"));
                    if (!Equal(Device.ReadDb(device.DbPath), after)) throw new IOException("Sąrašas jau atkurtas arba pakeistas kitos programos. Automatinis atkūrimas sustabdytas.");
                    RestoreAudio(tx, id);
                    Commit(after, before);
                }
                report(100, "Ankstesnė biblioteka atkurta.");
            }
        }
    }
}
