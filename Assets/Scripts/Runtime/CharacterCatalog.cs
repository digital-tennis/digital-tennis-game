using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DigitalTennis
{
    [Serializable] public sealed class CharacterProfile
    {
        public string id, name, localName, country, city, gender, heritage, persona, costume;
        public string primary, accent, secondary, skin, courtCode, courtName, mood, surface, courtColor, sky, skillName;
        public int age, style;
        public float height;
        public CharacterNarrative narrative;
        public static Color ColorOf(string value) => ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
    }
    // Authored fiction and presentation direction only; never modifies balance or match state.
    [Serializable] public sealed class CharacterNarrative
    {
        public string persona, formativeIncident, motivationStakes, flaw, growth, relationshipId, relationship;
        public string voiceIdentity, outfitIdentity, accessoryIdentity, skillIdentity, courtIdentity, musicIdentity;

        public string Page(int index)
        {
            switch (index)
            {
                case 0: return persona + "\n\n전환 사건\n" + formativeIncident + "\n\n목표와 걸린 것\n" + motivationStakes;
                case 1: return "약점\n" + flaw + "\n\n성장\n" + growth + "\n\n이름으로 이어진 관계\n" + relationship;
                case 2: return "목소리 · " + voiceIdentity + "\n\n의상 · " + outfitIdentity + "\n소품 · " + accessoryIdentity +
                    "\n\n스킬 · " + skillIdentity + "\n코트 · " + courtIdentity + "\n\n음악 · " + musicIdentity;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
    [Serializable] internal sealed class CharacterRoster { public CharacterProfile[] characters; }
    public static class CharacterCatalog
    {
        public static readonly string[] Ids = { "KOR", "JPN", "CHN", "USA", "RUS", "IND", "DEU", "GBR", "ESP", "FRA", "AUS", "NLD", "BRA", "MAR", "ZAF" };
        private static CharacterProfile[] profiles;
        public static CharacterProfile[] All => profiles ?? (profiles = JsonUtility.FromJson<CharacterRoster>(Resources.Load<TextAsset>("Characters/roster").text).characters);
        public static bool ValidIndex(int index) => index >= 0 && index < Ids.Length;
        public static CharacterProfile Get(int index) => ValidIndex(index) ? All[index] : throw new ArgumentOutOfRangeException(nameof(index));
    }

    [Serializable] public sealed class CharacterBalance
    {
        public string id;
        public float serve, smash, volley, defense, stamina, speed;
        public float shotMultiplier, serveMultiplier, smashMultiplier, volleyMultiplier, defenseMultiplier, speedMultiplier, restore, duration, attackGain, defenseGain;
        public float[] Ratings => new[] { serve, smash, volley, defense, stamina, speed };
        public bool IsValid
        {
            get
            {
                if (Array.IndexOf(CharacterCatalog.Ids, id) < 0) return false;
                foreach (float r in Ratings) if (!FiniteRange(r, 1, 100)) return false;
                foreach (float m in new[] { shotMultiplier, serveMultiplier, smashMultiplier, volleyMultiplier, defenseMultiplier, speedMultiplier })
                    if (!FiniteRange(m, 1, 1.35f)) return false;
                return FiniteRange(restore, 0, 40) && FiniteRange(duration, 1, 10) && FiniteRange(attackGain, 1, 40) && FiniteRange(defenseGain, 1, 30);
            }
        }
        private static bool FiniteRange(float v, float low, float high) => BallSimulation.IsFinite(v) && v >= low && v <= high;
    }

    public static class CharacterBalanceCsv
    {
        public const string Header = "id,serve,smash,volley,defense,stamina,speed,shot_multiplier,serve_multiplier,smash_multiplier,volley_multiplier,defense_multiplier,speed_multiplier,restore,duration,attack_gain,defense_gain";
        public static Dictionary<string, CharacterBalance> Parse(string csv)
        {
            var rows = ReadRows(csv.TrimStart('\ufeff'));
            if (rows.Count != 16 || string.Join(",", rows[0]) != Header) throw new FormatException("CSV needs the documented header and exactly 15 character rows.");
            var result = new Dictionary<string, CharacterBalance>();
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count != 17) throw new FormatException("CSV row " + (i + 1) + " needs 17 columns.");
                float N(int n) => float.Parse(r[n], NumberStyles.Float, CultureInfo.InvariantCulture);
                var b = new CharacterBalance { id = r[0], serve = N(1), smash = N(2), volley = N(3), defense = N(4), stamina = N(5), speed = N(6),
                    shotMultiplier = N(7), serveMultiplier = N(8), smashMultiplier = N(9), volleyMultiplier = N(10), defenseMultiplier = N(11),
                    speedMultiplier = N(12), restore = N(13), duration = N(14), attackGain = N(15), defenseGain = N(16) };
                if (!b.IsValid || result.ContainsKey(b.id)) throw new FormatException("Invalid or duplicate character: " + b.id);
                result.Add(b.id, b);
            }
            foreach (var id in CharacterCatalog.Ids) if (!result.ContainsKey(id)) throw new FormatException("Missing " + id);
            return result;
        }
        // Quoted fields, escaped quotes, CRLF and UTF-8 BOM; never parse using the machine's number culture.
        internal static List<List<string>> ReadRows(string text)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder();
            bool quoted = false, closed = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') { quoted = false; closed = true; }
                    else field.Append(c);
                }
                else if (c == '"' && field.Length == 0 && !closed) quoted = true;
                else if (c == ',' || c == '\n' || c == '\r')
                {
                    row.Add(field.ToString()); field.Clear(); closed = false;
                    if (c != ',') { if (row.Count > 1 || row[0].Length > 0) rows.Add(row); row = new List<string>(); }
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else { if (closed || c == '"') throw new FormatException("Malformed CSV quote."); field.Append(c); }
            }
            if (quoted) throw new FormatException("Unclosed CSV quote.");
            if (field.Length > 0 || row.Count > 0 || closed) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
    }

    public sealed class CharacterBalanceStore
    {
        private Dictionary<string, CharacterBalance> data;
        public string Path { get; }
        public string Status { get; private set; }
        public uint Revision { get; private set; } = 1;
        public CharacterBalance this[int index] => data[CharacterCatalog.Ids[index]];
        public CharacterBalanceStore(string path, string defaults, bool load = true)
        {
            Path = path; data = CharacterBalanceCsv.Parse(defaults);
            try
            {
                if (!File.Exists(path)) { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); File.WriteAllText(path, defaults, new UTF8Encoding(false)); }
                if (load) Reload();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Status = "Using defaults: " + e.GetType().Name; }
        }
        internal void Commit(Dictionary<string,CharacterBalance> candidate) { data=candidate;Revision++;Status="CSV loaded. Revision "+Revision; }
        public bool Reload()
        {
            try
            {
                var candidate = CharacterBalanceCsv.Parse(File.ReadAllText(Path));
                data = candidate; Revision++; Status = "CSV loaded. Revision " + Revision; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is OverflowException)
            { Status = "CSV rejected; keeping previous values: " + e.Message; return false; }
        }
    }
}
