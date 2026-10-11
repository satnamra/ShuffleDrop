using System;
using System.IO;

namespace ShuffleDrop
{
    internal static class Mp3
    {
        // Walk MPEG Layer III frame headers rather than trusting the extension or ID3 duration.
        public static uint Duration(string path)
        {
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
            {
                byte[] h = new byte[10];
                if (f.Length < 8 || f.Length > 1024L * 1024 * 1024) throw Bad(path);
                f.Read(h, 0, 10); long start = 0, end = f.Length;
                if (h[0] == 73 && h[1] == 68 && h[2] == 51)
                {
                    if (h[3] < 2 || h[3] > 4 || (h[6] | h[7] | h[8] | h[9]) >= 128) throw Bad(path);
                    start = 10L + (h[6] << 21) + (h[7] << 14) + (h[8] << 7) + h[9];
                    if (h[3] == 4 && (h[5] & 16) != 0) start += 10;
                }
                if (end >= 128)
                {
                    f.Position = end - 128; f.Read(h, 0, 3);
                    if (h[0] == 84 && h[1] == 65 && h[2] == 71) end -= 128;
                }
                if (end >= 32)
                {
                    f.Position = end - 32; byte[] ape = new byte[32]; f.Read(ape, 0, 32);
                    if (System.Text.Encoding.ASCII.GetString(ape, 0, 8) == "APETAGEX")
                    {
                        uint size = BitConverter.ToUInt32(ape, 12);
                        if (size < 32 || size > end - start) throw Bad(path);
                        end -= size;
                        if ((BitConverter.ToUInt32(ape, 20) & 0x80000000) != 0) end -= 32;
                    }
                }
                if (start < 0 || start >= end) throw Bad(path);
                long p = start; int frames = 0, firstRate = 0; double milliseconds = 0;
                byte[] payload = new byte[4096]; f.Position = start;
                int[] br1 = { 0,32,40,48,56,64,80,96,112,128,160,192,224,256,320,0 };
                int[] br2 = { 0,8,16,24,32,40,48,56,64,80,96,112,128,144,160,0 };
                int[] rates = { 44100,48000,32000 };
                while (p + 4 <= end)
                {
                    if (f.Read(h, 0, 4) != 4) throw Bad(path);
                    if (h[0] == 0 && h[1] == 0 && h[2] == 0 && h[3] == 0 && frames >= 2)
                    {
                        f.Position = p; int v; while ((v = f.ReadByte()) == 0 && f.Position < end) { }
                        if (v != 0 && v != -1) throw Bad(path); p = end; break;
                    }
                    int version = (h[1] >> 3) & 3, layer = (h[1] >> 1) & 3, ri = (h[2] >> 2) & 3;
                    if (h[0] != 255 || (h[1] & 224) != 224 || version == 1 || layer != 1 || ri == 3) throw Bad(path);
                    int bitrate = (version == 3 ? br1 : br2)[h[2] >> 4];
                    int rate = rates[ri] / (version == 3 ? 1 : version == 2 ? 2 : 4);
                    if (bitrate == 0 || rate < 16000 || (firstRate != 0 && firstRate != rate)) throw Bad(path);
                    firstRate = rate;
                    int size = (version == 3 ? 144000 : 72000) * bitrate / rate + ((h[2] >> 1) & 1);
                    if (p + size > end) throw Bad(path);
                    int remaining = size - 4;
                    while (remaining > 0)
                    {
                        int read = f.Read(payload, 0, Math.Min(remaining, payload.Length));
                        if (read == 0) throw Bad(path); remaining -= read;
                    }
                    milliseconds += (version == 3 ? 1152.0 : 576.0) * 1000.0 / rate;
                    frames++; p += size;
                }
                if (frames < 2 || p != end || milliseconds > uint.MaxValue) throw Bad(path);
                return (uint)Math.Ceiling(milliseconds);
            }
        }
        static Exception Bad(string path)
        { return new InvalidDataException("Invalid, unsupported, or incomplete MP3 file: " + Path.GetFileName(path)); }
    }
}
