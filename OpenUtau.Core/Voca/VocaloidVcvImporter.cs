using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Linq;

namespace OpenUtau.Core.Voca {
    public enum VocaloidVersion { V3, V4, V5, Unknown }
    public class VocaloidPhoneme {
        public string Alias;
        public byte[] PcmData;
        public int SampleRate;
        public int Channels;
        public int BitDepth;
        public string EvecStyle;
    }
    public class VocaloidVcvBank {
        public string Name;
        public VocaloidVersion Version;
        public List<VocaloidPhoneme> Phonemes = new();
    }
    public static class VocaloidVcvImporter {
        public static VocaloidVcvBank Load(string bankDir) {
            var dir = new DirectoryInfo(bankDir);
            foreach (var f in dir.GetFiles("*.vvd"))
                if (IsZipFile(f.FullName)) return LoadV5(f.FullName);
            var ddis = dir.GetFiles("*.ddi");
            foreach (var ddi in ddis) {
                string ddbPath = Path.Combine(bankDir,
                    Path.GetFileNameWithoutExtension(ddi.Name) + ".ddb");
                if (File.Exists(ddbPath))
                    return LoadV4(ddi.FullName, ddbPath,
                        Path.GetFileNameWithoutExtension(ddi.Name));
            }
            throw new InvalidDataException($"no Vocaloid bank in {bankDir}");
        }
        static VocaloidVcvBank LoadV5(string vvdPath) {
            var bank = new VocaloidVcvBank {
                Name = Path.GetFileNameWithoutExtension(vvdPath),
                Version = VocaloidVersion.V5
            };
            using var zip = ZipFile.OpenRead(vvdPath);
            var symbolMap = new Dictionary<string, string>();
            var voiceEntry = zip.GetEntry("voice.xml");
            if (voiceEntry != null) {
                using var stream = voiceEntry.Open();
                var xml = XDocument.Load(stream);
                var ns = xml.Root?.Name.Namespace ?? XNamespace.None;
                foreach (var ph in xml.Descendants(ns + "phoneme")) {
                    string sym = ph.Attribute("symbol")?.Value ?? "";
                    string file = ph.Attribute("file")?.Value ?? sym;
                    if (!string.IsNullOrEmpty(sym)) symbolMap[file] = sym;
                }
            }
            foreach (var entry in zip.Entries) {
                if (!entry.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;
                string stem = Path.GetFileNameWithoutExtension(entry.Name);
                string alias = symbolMap.TryGetValue(stem, out var s) ? s : stem;
                using var ms = new MemoryStream();
                using (var es = entry.Open()) es.CopyTo(ms);
                var (pcm, sr, ch, bits) = StripWavHeader(ms.ToArray());
                bank.Phonemes.Add(new VocaloidPhoneme {
                    Alias = alias, PcmData = pcm, SampleRate = sr,
                    Channels = ch, BitDepth = bits,
                    EvecStyle = ParseEvecStyle(entry.FullName)
                });
            }
            return bank;
        }
        struct DdiEntry {
            public string Alias; public long Offset; public int Length;
            public int SampleRate; public int Channels; public int BitDepth;
            public string EvecStyle;
        }
        static VocaloidVcvBank LoadV4(string ddiPath, string ddbPath, string name) {
            var bank = new VocaloidVcvBank { Name = name, Version = VocaloidVersion.V4 };
            var entries = ParseDdi(ddiPath);
            using var ddb = new FileStream(ddbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            foreach (var e in entries) {
                if (e.Length == 0) continue;
                ddb.Seek(e.Offset, SeekOrigin.Begin);
                byte[] pcm = new byte[e.Length];
                if (ddb.Read(pcm, 0, pcm.Length) < pcm.Length) continue;
                bank.Phonemes.Add(new VocaloidPhoneme {
                    Alias = e.Alias, PcmData = pcm,
                    SampleRate = e.SampleRate > 0 ? e.SampleRate : 44100,
                    Channels = e.Channels > 0 ? e.Channels : 1,
                    BitDepth = e.BitDepth > 0 ? e.BitDepth : 16,
                    EvecStyle = e.EvecStyle
                });
            }
            return bank;
        }
        static List<DdiEntry> ParseDdi(string path) {
            var entries = new List<DdiEntry>();
            byte[] raw = File.ReadAllBytes(path);
            int i = 0;
            while (i < raw.Length - 20) {
                if (raw[i] < 32 || raw[i] > 126) { i++; continue; }
                int start = i;
                while (i < raw.Length && raw[i] != 0 && raw[i] >= 32 && raw[i] <= 126) i++;
                int nameLen = i - start;
                if (nameLen < 1 || nameLen > 32 || i >= raw.Length || raw[i] != 0) { i = start+1; continue; }
                string alias = Encoding.ASCII.GetString(raw, start, nameLen);
                int after = i + 1;
                if (after + 20 > raw.Length) break;
                long offset = BitConverter.ToInt64(raw, after);
                int length = BitConverter.ToInt32(raw, after + 8);
                int sr = BitConverter.ToInt32(raw, after + 12);
                short ch = BitConverter.ToInt16(raw, after + 16);
                short bits = BitConverter.ToInt16(raw, after + 18);
                if (offset >= 0 && offset < 3_000_000_000L && length > 0 && length < 100_000_000
                    && (sr == 0 || sr == 44100 || sr == 48000) && (ch == 0 || ch == 1 || ch == 2)) {
                    entries.Add(new DdiEntry { Alias=alias, Offset=offset, Length=length,
                        SampleRate=sr, Channels=ch, BitDepth=bits, EvecStyle=ParseEvecStyle(alias) });
                    i = after + 20; continue;
                }
                i = start + 1;
            }
            return entries;
        }
        static (byte[] pcm, int sr, int ch, int bits) StripWavHeader(byte[] wav) {
            if (wav.Length < 44 || wav[0] != 'R') return (wav, 44100, 1, 16);
            int sr = BitConverter.ToInt32(wav, 24);
            short ch = BitConverter.ToInt16(wav, 22);
            short bits = BitConverter.ToInt16(wav, 34);
            int pos = 12;
            while (pos < wav.Length - 8) {
                string tag = Encoding.ASCII.GetString(wav, pos, 4);
                int sz = BitConverter.ToInt32(wav, pos + 4);
                if (tag == "data") {
                    byte[] pcm = new byte[sz];
                    Array.Copy(wav, pos+8, pcm, 0, Math.Min(sz, wav.Length-pos-8));
                    return (pcm, sr, ch, bits);
                }
                pos += 8 + sz;
            }
            return (wav, sr, ch, bits);
        }
        static bool IsZipFile(string path) {
            try {
                byte[] h = new byte[4];
                using var f = File.OpenRead(path);
                f.Read(h, 0, 4);
                return h[0]=='P' && h[1]=='K';
            } catch { return false; }
        }
        static string ParseEvecStyle(string s) {
            foreach (var style in new[]{"Original","Soft","Dark","Vivid","Sweet","Power"})
                if (s.Contains(style, StringComparison.OrdinalIgnoreCase)) return style;
            return "Original";
        }
    }
}
