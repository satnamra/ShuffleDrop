using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShuffleDrop
{
    // iTunesSD 3G/4G layout: public reverse-engineered format, see SOURCES.md.
    internal sealed class ShuffleDatabase
    {
        public byte[] Header, TrackHeader, PlaylistHeader;
        public bool Legacy;
        public readonly List<byte[]> Tracks = new List<byte[]>();
        public readonly List<byte[]> Playlists = new List<byte[]>();
        public static uint U(byte[] b, int p) { Bounds(b, p, 4); return BitConverter.ToUInt32(b, p); }
        public static void Put(byte[] b, int p, uint n) { Buffer.BlockCopy(BitConverter.GetBytes(n), 0, b, p, 4); }
        static void Bounds(byte[] b, int p, int n)
        { if (p < 0 || n < 0 || p > b.Length - n) throw Invalid(); }
        static Exception Invalid() { return new InvalidDataException("The iPod library is unsupported or damaged. The device will not be changed."); }
        static void Magic(byte[] b, int p, string s)
        { Bounds(b, p, 4); if (Encoding.ASCII.GetString(b, p, 4) != s) throw Invalid(); }
        static byte[] Slice(byte[] b, int p, int n)
        { Bounds(b, p, n); byte[] r = new byte[n]; Buffer.BlockCopy(b, p, r, 0, n); return r; }
        static int Count(byte[] b, int p, int maximum)
        { uint n = U(b, p); if (n > maximum) throw Invalid(); return (int)n; }
        public static ShuffleDatabase Parse(byte[] b)
        {
            if (b.Length > 16 * 1024 * 1024) throw Invalid();
            if (b.Length >= 18 && b[3] == 1 && (b[4] == 6 || b[4] == 8) && b[5] == 0)
            {
                int n = b[0] * 65536 + b[1] * 256 + b[2];
                if (n > 30000 || b[6] != 0 || b[7] != 0 || b[8] != 18 || b.Length != 18 + n * 558) throw Invalid();
                var old = new ShuffleDatabase { Legacy = true, Header = Slice(b, 0, 18) };
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < n; i++)
                {
                    byte[] t = Slice(b, 18 + i * 558, 558);
                    if (t[0] != 0 || t[1] != 2 || t[2] != 46 || !seen.Add(TrackPath(t))) throw Invalid();
                    old.Tracks.Add(t);
                }
                return old;
            }
            Magic(b, 0, "bdhs");
            if (U(b, 4) != 0x02000003 || U(b, 8) != 64 || U(b, 36) != 64) throw Invalid();
            int nt = Count(b, 12, 30000), np = Count(b, 16, 1000), ph = Count(b, 40, b.Length);
            if (np < 1 || U(b, 32) > nt) throw Invalid();
            var d = new ShuffleDatabase { Header = Slice(b, 0, 64) };
            Magic(b, 64, "hths");
            if (U(b, 68) != 20 + nt * 4 || U(b, 72) != nt) throw Invalid();
            d.TrackHeader = Slice(b, 64, 20);
            int cursor = 84 + nt * 4;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nt; i++)
            {
                if (U(b, 84 + i * 4) != cursor) throw Invalid();
                Magic(b, cursor, "rths");
                if (U(b, cursor + 4) != 372) throw Invalid();
                byte[] t = Slice(b, cursor, 372);
                string path = TrackPath(t);
                if (!paths.Add(path)) throw new InvalidDataException("The library contains duplicate file paths. This library layout is not supported yet.");
                d.Tracks.Add(t); cursor += 372;
            }
            if (cursor != ph) throw Invalid();
            Magic(b, ph, "hphs");
            if (U(b, ph + 4) != 20 + np * 4 || U(b, ph + 8) != np) throw Invalid();
            d.PlaylistHeader = Slice(b, ph, 20);
            cursor = ph + 20 + np * 4;
            int masters = 0;
            for (int i = 0; i < np; i++)
            {
                if (U(b, ph + 20 + i * 4) != cursor) throw Invalid();
                Magic(b, cursor, "lphs");
                int n = Count(b, cursor + 8, 30000), len = 44 + n * 4;
                if (U(b, cursor + 4) != len || U(b, cursor + 12) > n) throw Invalid();
                byte[] p = Slice(b, cursor, len);
                if (U(p, 24) == 1) masters++;
                for (int j = 0; j < n; j++) if (U(p, 44 + j * 4) >= nt) throw Invalid();
                d.Playlists.Add(p); cursor += len;
            }
            if (masters != 1 || cursor != b.Length) throw Invalid();
            return d;
        }
        public static string TrackPath(byte[] track)
        {
            string s;
            if (track.Length == 558)
            {
                int end = 33;
                while (end < 555 && (track[end] != 0 || track[end + 1] != 0)) end += 2;
                if (end == 555) throw Invalid();
                s = new UnicodeEncoding(false, false, true).GetString(track, 33, end - 33);
            }
            else
            {
                int end = Array.IndexOf(track, (byte)0, 24, 256);
                if (end < 0) throw Invalid();
                s = new UTF8Encoding(false, true).GetString(track, 24, end - 24);
            }
            if (!s.StartsWith("/", StringComparison.Ordinal) || s.Length < 2 || s.Contains("\\") || s.Contains(":")) throw Invalid();
            foreach (string part in s.Substring(1).Split('/'))
                if (part == ".." || part == "." || part.Length == 0 || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw Invalid();
            return s;
        }
        public void Append(string devicePath, uint duration, byte[] id)
        {
            if (Legacy)
            {
                byte[] record = new byte[558]; record[1] = 2; record[2] = 46; record[3] = 90; record[4] = 165; record[5] = 1;
                record[26] = 100; record[29] = 1; record[31] = 2; record[555] = 1;
                byte[] name = Encoding.Unicode.GetBytes(devicePath);
                if (name.Length > 520) throw new InvalidDataException("The file path is too long.");
                name.CopyTo(record, 33); Tracks.Add(record); return;
            }
            byte[] t = new byte[372]; Encoding.ASCII.GetBytes("rths").CopyTo(t, 0);
            Put(t, 4, 372); Put(t, 12, duration); Put(t, 20, 1);
            byte[] path = Encoding.UTF8.GetBytes(devicePath);
            if (path.Length >= 256) throw new InvalidDataException("The file path is too long.");
            path.CopyTo(t, 24); t[284] = 1;
            Put(t, 288, 512); Put(t, 292, 512); t[316] = 1;
            Buffer.BlockCopy(id, 0, t, 328, 8);
            int index = Tracks.Count; Tracks.Add(t);
            int master = Playlists.FindIndex(p => U(p, 24) == 1);
            byte[] old = Playlists[master], updated = new byte[old.Length + 4];
            old.CopyTo(updated, 0); Put(updated, 4, (uint)updated.Length);
            Put(updated, 8, U(old, 8) + 1); Put(updated, 12, U(old, 12) + 1);
            Put(updated, old.Length, (uint)index); Playlists[master] = updated;
            Put(Header, 32, U(Header, 32) + 1);
        }
        public byte[] Serialize()
        {
            if (Legacy)
            {
                byte[] data = new byte[18 + Tracks.Count * 558]; Header.CopyTo(data, 0);
                data[0] = (byte)(Tracks.Count >> 16); data[1] = (byte)(Tracks.Count >> 8); data[2] = (byte)Tracks.Count;
                for (int i = 0; i < Tracks.Count; i++) Tracks[i].CopyTo(data, 18 + i * 558);
                Parse(data); return data;
            }
            int ph = 64 + 20 + Tracks.Count * 4 + Tracks.Sum(t => t.Length);
            byte[] header = (byte[])Header.Clone();
            Put(header, 12, (uint)Tracks.Count); Put(header, 16, (uint)Playlists.Count); Put(header, 40, (uint)ph);
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(header);
                byte[] th = (byte[])TrackHeader.Clone(); Put(th, 4, (uint)(20 + Tracks.Count * 4)); Put(th, 8, (uint)Tracks.Count);
                writer.Write(th); int pos = 84 + Tracks.Count * 4;
                foreach (byte[] t in Tracks) { writer.Write(pos); pos += t.Length; }
                foreach (byte[] t in Tracks) writer.Write(t);
                byte[] plh = (byte[])PlaylistHeader.Clone(); Put(plh, 4, (uint)(20 + Playlists.Count * 4)); Put(plh, 8, (uint)Playlists.Count);
                writer.Write(plh); pos = ph + 20 + Playlists.Count * 4;
                foreach (byte[] p in Playlists) { writer.Write(pos); pos += p.Length; }
                foreach (byte[] p in Playlists) writer.Write(p);
                byte[] result = stream.ToArray(); Parse(result); return result;
            }
        }
        public void KeepOrder(IList<int> order)
        {
            int count = Tracks.Count;
            if (order.Count > count || order.Distinct().Count() != order.Count || order.Any(i => i < 0 || i >= count))
                throw new InvalidDataException("Invalid track order.");
            bool deleting = order.Count != count;
            if (!Legacy)
            {
                if (deleting && (U(Header, 32) != count || Playlists.Any(p => U(p, 8) != U(p, 12))))
                    throw new InvalidDataException("This library contains podcasts or audiobooks. Removing them is not supported in this version.");
                var map = new Dictionary<int, int>(); for (int i = 0; i < order.Count; i++) map.Add(order[i], i);
                for (int i = 0; i < Playlists.Count; i++)
                {
                    byte[] p = Playlists[i]; var entries = new List<int>();
                    for (int j = 0; j < U(p, 8); j++) { int old = (int)U(p, 44 + j * 4); if (map.ContainsKey(old)) entries.Add(map[old]); }
                    if (U(p, 24) == 1) entries.Sort();
                    byte[] next = new byte[44 + entries.Count * 4]; Buffer.BlockCopy(p, 0, next, 0, 44);
                    Put(next, 4, (uint)next.Length); Put(next, 8, (uint)entries.Count);
                    if (deleting) Put(next, 12, (uint)entries.Count);
                    for (int j = 0; j < entries.Count; j++) Put(next, 44 + j * 4, (uint)entries[j]);
                    Playlists[i] = next;
                }
                if (deleting) Put(Header, 32, (uint)order.Count);
            }
            var tracks = order.Select(i => Tracks[i]).ToList(); Tracks.Clear(); Tracks.AddRange(tracks);
        }
    }
}
