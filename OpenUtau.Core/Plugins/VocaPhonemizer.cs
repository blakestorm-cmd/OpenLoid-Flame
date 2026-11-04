using System.Collections.Generic;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Voca;

namespace OpenUtau.Plugin.Builtin {
    public abstract class VocaPhonemizerBase : Phonemizer {
        protected VocaloidVcvBank bank;
        protected abstract HashSet<string> Vowels { get; }
        private USinger singer;
        private readonly Dictionary<int, string> vowelCache = new();
        public override void SetSinger(USinger singer) {
            this.singer = singer;
            bank = VocaloidVcvImporter.Load(singer.Location);
            vowelCache.Clear();
        }
        protected string ExtractTrailingVowel(string lyric) {
            if (string.IsNullOrEmpty(lyric)) return "-";
            for (int i = lyric.Length - 1; i >= 0; i--)
                if (Vowels.Contains(lyric[i].ToString().ToLower()))
                    return lyric[i].ToString().ToLower();
            return "-";
        }
        protected string MakeAlias(string prev, string lyric) =>
            string.IsNullOrEmpty(prev) || prev == "-" ? lyric : $"{prev} {lyric}";
        protected bool BankHas(string alias) =>
            bank?.Phonemes.Any(p => p.Alias == alias) ?? false;
        public override Result Process(Note[] notes, Note?[] prev, Note?[] next,
            Note?[] prevNeighbour, Note?[] nextNeighbour, Note[] prevs) {
            var note = notes[0];
            string lyric = note.lyric.Trim();
            string prevVowel = "-";
            if (prevNeighbour?.Length > 0 && prevNeighbour[0].HasValue)
                vowelCache.TryGetValue(prevNeighbour[0].Value.position, out prevVowel);
            prevVowel ??= "-";
            string alias = MakeAlias(prevVowel, lyric);
            vowelCache[note.position] = ExtractTrailingVowel(lyric);
            if (!BankHas(alias)) alias = lyric;
            if (!BankHas(alias)) return MakeSimpleResult(lyric);
            return new Result { phonemes = new Phoneme[] {
                new Phoneme { phoneme = alias, position = 0 } } };
        }
    }
    [Phonemizer("VocaPhonemizer JA", "JA VOCA-VCV", "sussy", Language = "JA")]
    public class VocaPhonemizerJA : VocaPhonemizerBase {
        protected override HashSet<string> Vowels => new() { "a","i","u","e","o" };
    }
    [Phonemizer("VocaPhonemizer EN", "EN VOCA-VCV", "sussy", Language = "EN")]
    public class VocaPhonemizerEN : VocaPhonemizerBase {
        protected override HashSet<string> Vowels => new() {
            "aa","ae","ah","ao","aw","ax","ay","eh","er","ey",
            "ih","iy","ow","oy","uh","uw","a","e","i","o","u" };
    }
    [Phonemizer("VocaPhonemizer ZH", "ZH VOCA-VCV", "sussy", Language = "ZH")]
    public class VocaPhonemizerZH : VocaPhonemizerBase {
        protected override HashSet<string> Vowels => new() {
            "a","o","e","i","u","v","ai","ei","ui","ao","ou","iu",
            "ie","ve","er","an","en","in","un","vn","ang","eng","ing","ong" };
    }
    [Phonemizer("VocaPhonemizer KO", "KO VOCA-VCV", "sussy", Language = "KO")]
    public class VocaPhonemizerKO : VocaPhonemizerBase {
        protected override HashSet<string> Vowels => new() {
            "a","ae","ya","yae","eo","e","yeo","ye","o","wa","wae",
            "oe","yo","u","wo","we","wi","yu","eu","ui","i" };
    }
    [Phonemizer("VocaPhonemizer ES", "ES VOCA-VCV", "sussy", Language = "ES")]
    public class VocaPhonemizerES : VocaPhonemizerBase {
        protected override HashSet<string> Vowels => new() { "a","e","i","o","u" };
    }
}
