using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Format {
    public static class VsqxImporter {
        static readonly XNamespace ns3 = "http://www.yamaha.co.jp/vocaloid/schema/vsq3/";
        static readonly XNamespace ns4 = "http://www.yamaha.co.jp/vocaloid/schema/vsq4/";
        public static UProject Load(string path) {
            var xml = XDocument.Load(path);
            var root = xml.Root;
            if (root.Name.LocalName == "vsq4") return ParseVsq4(root, ns4);
            if (root.Name.LocalName == "vsq3") return ParseVsq3(root, ns3);
            throw new InvalidDataException("not a vsq3 or vsq4 file");
        }
        static UProject ParseVsq4(XElement root, XNamespace ns) {
            var proj = new UProject();
            var master = root.Element(ns + "masterTrack");
            foreach (var t in master.Elements(ns + "tempo"))
                proj.tempos.Add(new UTempo((int)t.Element(ns+"t"), (int)t.Element(ns+"v")/100.0));
            foreach (var ts in master.Elements(ns + "timeSig"))
                proj.timeSignatures.Add(new UTimeSignature(
                    (int)ts.Element(ns+"m"), (int)ts.Element(ns+"nu"), (int)ts.Element(ns+"de")));
            foreach (var vsTrack in root.Elements(ns + "vsTrack")) {
                var track = new UTrack { TrackName = (string)vsTrack.Element(ns+"name") ?? "Track" };
                foreach (var part in vsTrack.Elements(ns + "vsPart")) {
                    var upart = new UVoicePart {
                        position = (int)part.Element(ns+"t"),
                        Duration = (int)part.Element(ns+"playTime")
                    };
                    foreach (var note in part.Elements(ns + "note")) {
                        var un = new UNote {
                            position = (int)note.Element(ns+"t"),
                            duration = (int)note.Element(ns+"dur"),
                            tone = (int)note.Element(ns+"n"),
                            lyric = (string)note.Element(ns+"y") ?? "a"
                        };
                        var pElem = note.Element(ns+"p");
                        if (pElem != null && (string)pElem.Attribute("lock") == "1")
                            un.phonemeOverrides.Add(new UPhonemeOverride { index = 0, phoneme = pElem.Value });
                        upart.notes.Add(un);
                    }
                    upart.trackNo = proj.tracks.Count; proj.parts.Add(upart);
                }
                proj.tracks.Add(track);
            }
            return proj;
        }
        static UProject ParseVsq3(XElement root, XNamespace ns) {
            var proj = new UProject();
            var master = root.Element(ns + "masterTrack");
            foreach (var t in master.Elements(ns + "tempo"))
                proj.tempos.Add(new UTempo((int)t.Element(ns+"posTick"), (int)t.Element(ns+"bpm")/100.0));
            foreach (var ts in master.Elements(ns + "timeSig"))
                proj.timeSignatures.Add(new UTimeSignature(
                    (int)ts.Element(ns+"posMes"), (int)ts.Element(ns+"nume"), (int)ts.Element(ns+"denomi")));
            foreach (var vsTrack in root.Elements(ns + "vsTrack")) {
                var track = new UTrack { TrackName = (string)vsTrack.Element(ns+"trackName") ?? "Track" };
                foreach (var part in vsTrack.Elements(ns + "musicalPart")) {
                    var upart = new UVoicePart {
                        position = (int)part.Element(ns+"posTick"),
                        Duration = (int)part.Element(ns+"playTime")
                    };
                    foreach (var note in part.Elements(ns + "note")) {
                        var un = new UNote {
                            position = (int)note.Element(ns+"posTick"),
                            duration = (int)note.Element(ns+"durTick"),
                            tone = (int)note.Element(ns+"noteNum"),
                            lyric = (string)note.Element(ns+"lyric") ?? "a"
                        };
                        var pElem = note.Element(ns+"phnms");
                        if (pElem != null && (string)pElem.Attribute("lock") == "1")
                            un.phonemeOverrides.Add(new UPhonemeOverride { index = 0, phoneme = pElem.Value });
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
