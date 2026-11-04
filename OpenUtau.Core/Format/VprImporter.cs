using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Format {
    public static class VprImporter {
        public static UProject Load(string path) {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("sequence.json")
                        ?? throw new InvalidDataException("sequence.json not found");
            using var stream = entry.Open();
            var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var proj = new UProject(); proj.tracks.Clear();
            var master = root.GetProperty("masterTrack");
            foreach (var t in master.GetProperty("tempos").EnumerateArray())
                proj.tempos.Add(new UTempo(
                    t.GetProperty("pos").GetInt32(),
                    t.GetProperty("bpm").GetDouble()));
            foreach (var ts in master.GetProperty("timeSigns").EnumerateArray())
                proj.timeSignatures.Add(new UTimeSignature(
                    ts.GetProperty("bar").GetInt32(),
                    ts.GetProperty("numer").GetInt32(),
                    ts.GetProperty("denom").GetInt32()));
            foreach (var t in root.GetProperty("tracks").EnumerateArray()) {
                var track = new UTrack {
                    TrackName = t.TryGetProperty("name", out var tn) ? tn.GetString() : "Track"
                };
                if (!t.TryGetProperty("parts", out var parts)) continue;
                foreach (var part in parts.EnumerateArray()) {
                    var upart = new UVoicePart {
                        position = part.GetProperty("pos").GetInt32(),
                        Duration = part.GetProperty("duration").GetInt32()
                    };
                    if (!part.TryGetProperty("notes", out var notes)) continue;
                    foreach (var note in notes.EnumerateArray()) {
                        var un = new UNote {
                            position = note.GetProperty("pos").GetInt32(),
                            duration = note.GetProperty("duration").GetInt32(),
                            tone = note.GetProperty("number").GetInt32(),
                            lyric = note.TryGetProperty("lyric", out var ly) ? ly.GetString() : "a"
                        };
                        if (note.TryGetProperty("phoneme", out var ph) && !string.IsNullOrEmpty(ph.GetString()))
                            un.phonemeOverrides.Add(new UPhonemeOverride { index = 0, phoneme = ph.GetString() });
                        upart.notes.Add(un);
                    }
                    upart.trackNo = proj.tracks.Count; proj.parts.Add(upart);
                }
                proj.tracks.Add(track);
            }
            return proj;
        }
    }
}
