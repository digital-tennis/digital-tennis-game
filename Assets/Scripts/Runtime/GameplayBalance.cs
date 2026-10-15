using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DigitalTennis.AI;
using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Validated immutable tuning. Defaults are CSV data; the schema only defines safe limits.</summary>
    public sealed class GameplayBalance
    {
        private static readonly Dictionary<string,(float low,float high)> Schema = new Dictionary<string,(float,float)>(StringComparer.Ordinal)
        {
            { "physics.gravity", (1f, 30f) },
            { "physics.radius", (0.03f, 0.3f) },
            { "physics.bounce", (0.1f, 1f) },
            { "physics.ground_retention", (0.1f, 1f) },
            { "physics.net_bounce", (0f, 1f) },
            { "physics.rest_speed", (0.05f, 2f) },
            { "shot.base_speed", (2f, 30f) },
            { "shot.power_speed", (1f, 30f) },
            { "shot.yaw", (1f, 45f) },
            { "shot.pitch", (5f, 45f) },
            { "shot.pitch_aim", (0f, 20f) },
            { "shot.default_power", (0f, 1f) },
            { "smash.flight", (0.2f, 2f) },
            { "smash.aim_width", (0f, 4f) },
            { "smash.target_width", (0.5f, 4.115f) },
            { "smash.target_depth", (2f, 10f) },
            { "smash.aim_depth", (0f, 3f) },
            { "lob.horizontal", (0.1f, 1.5f) },
            { "lob.lift", (3f, 15f) },
            { "lob.aim_lift", (0f, 4f) },
            { "lob.forward", (0.1f, 1.5f) },
            { "slice.lift", (0.1f, 1.5f) },
            { "slice.forward", (0.1f, 1.5f) },
            { "volley.lift", (0.1f, 1.5f) },
            { "volley.forward", (0.1f, 1.5f) },
            { "hit.reach", (0.3f, 3f) },
            { "hit.min_height", (0.05f, 1f) },
            { "hit.max_height", (1f, 4f) },
            { "hit.smash_height", (0.5f, 4f) },
            { "hit.net_gap", (0.05f, 1f) },
            { "move.speed", (1f, 10f) },
            { "move.home_depth", (3f, 11.885f) },
            { "move.min_depth", (0.2f, 3f) },
            { "move.contact_height", (0.2f, 2.5f) },
            { "serve.offset", (0.3f, 3.8f) },
            { "serve.height", (1f, 3.5f) },
            { "serve.depth", (11.9f, 15f) },
            { "serve.target_depth", (1f, 6.4f) },
            { "serve.flight", (0.7f, 2f) },
            { "serve.receiver_depth", (5f, 11.885f) },
            { "match.next_point_delay", (0.2f, 5f) },
            { "match.auto_serve_delay", (0.1f, 5f) },
            { "rating.center", (1f, 100f) },
            { "rating.scale", (0f, 0.01f) },
            { "energy.move_floor", (0.5f, 1f) },
            { "energy.shot_floor", (0.5f, 1f) },
            { "energy.hit_cost", (0f, 20f) },
            { "energy.hit_factor", (1f, 2f) },
            { "energy.hit_stamina", (0f, 0.01f) },
            { "energy.move_cost", (0.1f, 2f) },
            { "energy.move_stamina", (0f, 0.01f) },
            { "energy.recovery", (0f, 20f) },
            { "energy.recovery_stamina", (0f, 0.2f) },
            { "meter.fast_ball", (1f, 50f) },
            { "meter.fast_bonus", (0f, 30f) },
            { "meter.serve_gain", (0f, 2f) },
            { "recovery.speed", (1f, 10f) },
            { "recovery.pickup", (0.1f, 3f) },
            { "ai.target_width", (0f, 4.115f) },
            { "ai.target_depth", (2f, 11.885f) },
            { "ai.easy.reaction", (0f, 1f) },
            { "ai.easy.error", (0f, 3f) },
            { "ai.easy.probability", (0f, 1f) },
            { "ai.easy.max_power", (0f, 1f) },
            { "ai.easy.skill_stock", (1f, 3f) },
            { "ai.medium.reaction", (0f, 1f) },
            { "ai.medium.error", (0f, 3f) },
            { "ai.medium.probability", (0f, 1f) },
            { "ai.medium.max_power", (0f, 1f) },
            { "ai.medium.skill_stock", (1f, 3f) },
            { "ai.hard.reaction", (0f, 1f) },
            { "ai.hard.error", (0f, 3f) },
            { "ai.hard.probability", (0f, 1f) },
            { "ai.hard.max_power", (0f, 1f) },
            { "ai.hard.skill_stock", (1f, 3f) },
        };
        private static readonly string[] Keys=Schema.Keys.OrderBy(k=>k,StringComparer.Ordinal).ToArray();
        private readonly Dictionary<string,float> data;
        private static GameplayBalance defaults;
        public static GameplayBalance Defaults => defaults ?? (defaults = Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Balance","gameplay.csv"))));
        public float this[string key] => data[key];
        public string Hash { get; }
        public float[] Values => Keys.Select(k=>data[k]).ToArray();
        public IDictionary<string,float> CopyValues() => new Dictionary<string,float>(data);
        private GameplayBalance(Dictionary<string,float> values)
        {
            if (values.Count != Schema.Count) throw new FormatException("gameplay.csv: missing or extra keys.");
            foreach (var s in Schema)
                if (!values.TryGetValue(s.Key,out float v) || !BallSimulation.IsFinite(v) || v<s.Value.low || v>s.Value.high)
                    throw new FormatException("gameplay.csv: out of range or missing " + s.Key);
            data = new Dictionary<string,float>(values,StringComparer.Ordinal);
            if (this["hit.min_height"] >= this["hit.max_height"] || this["hit.smash_height"] < this["hit.min_height"] || this["hit.smash_height"] > this["hit.max_height"] ||
                this["move.contact_height"] < this["hit.min_height"] || this["move.contact_height"] > this["hit.max_height"] ||
                this["shot.pitch"] - this["shot.pitch_aim"] < 1 || this["shot.pitch"] + this["shot.pitch_aim"] >= 80 ||
                this["smash.target_depth"] + this["smash.aim_depth"] > CourtBounds.HalfLength ||
                this["smash.target_depth"] - this["smash.aim_depth"] <= 0 ||
                this["energy.hit_factor"] < 100*this["energy.hit_stamina"] || this["energy.move_cost"] < 100*this["energy.move_stamina"] ||
                1+(1-this["rating.center"])*this["rating.scale"] <= 0)
                throw new FormatException("gameplay.csv: inconsistent heights / angles / stamina / target bounds.");
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty))) Difficulty(d);
            using (var sha=SHA256.Create()) Hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                string.Join("\n",data.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value.ToString("R",CultureInfo.InvariantCulture)))))).Replace("-", "").ToLowerInvariant();
        }
        public DifficultySettings Difficulty(Difficulty d)
        {
            if (!Enum.IsDefined(typeof(Difficulty),d)) throw new ArgumentOutOfRangeException(nameof(d));
            string p="ai."+d.ToString().ToLowerInvariant()+".";
            float stock=this[p+"skill_stock"];
            if (stock != (int)stock) throw new FormatException(p+"skill_stock must be an integer.");
            return new DifficultySettings(d,this[p+"reaction"],this[p+"error"],this[p+"probability"],this[p+"max_power"],(int)stock);
        }
        public BallPhysicsSettings Physics() => new BallPhysicsSettings {
            Gravity=this["physics.gravity"],Radius=this["physics.radius"],BounceRestitution=this["physics.bounce"],
            GroundSpeedRetention=this["physics.ground_retention"],NetRestitution=this["physics.net_bounce"],RestSpeed=this["physics.rest_speed"] };
        public AiTuning AiTuning() => new AiTuning(CopyValues());
        public static GameplayBalance Parse(string text)
        {
            var rows=CharacterBalanceCsv.ReadRows(text.TrimStart('\ufeff'));
            if (rows.Count==0 || string.Join(",",rows[0])!="key,value,unit,description") throw new FormatException("gameplay.csv: invalid header.");
            var values=new Dictionary<string,float>(StringComparer.Ordinal);
            foreach (var row in rows.Skip(1))
            {
                if(row.Count!=4 || !Schema.ContainsKey(row[0]) || values.ContainsKey(row[0])) throw new FormatException("gameplay.csv: unknown or duplicate key.");
                values.Add(row[0],float.Parse(row[1],NumberStyles.Float,CultureInfo.InvariantCulture));
            }
            return new GameplayBalance(values);
        }
        public static bool TryWire(float[] values,string hash,out GameplayBalance balance)
        {
            balance=null;
            if(values==null || values.Length!=Schema.Count || hash==null || hash.Length!=64) return false;
            try {
                var data=new Dictionary<string,float>();int i=0;foreach(var k in Keys)data.Add(k,values[i++]);
                var candidate=new GameplayBalance(data);if(candidate.Hash!=hash)return false;balance=candidate;return true;
            } catch(Exception e) when(e is FormatException || e is ArgumentException) { return false; }
        }
    }
    /// <summary>Read every gameplay/character candidate before committing any value. Effects remain local cosmetics.</summary>
    public sealed class BalanceBundleStore
    {
        private GameplayBalance local;
        private uint localRevision=1;
        public GameplayBalance Current { get; private set; }
        public CharacterBalanceStore Characters { get; }
        public string Path { get; }
        public string Status { get; private set; }
        public uint Revision { get; private set; } = 1;
        public BalanceBundleStore(string gameplayPath,string characterPath,string gameplayDefaults,string characterDefaults)
        {
            Path=gameplayPath;Current=local=GameplayBalance.Parse(gameplayDefaults);
            Characters=new CharacterBalanceStore(characterPath,characterDefaults,false);
            try {
                if(!File.Exists(Path)){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));File.WriteAllText(Path,gameplayDefaults,new UTF8Encoding(false));}
                Reload();
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { Status="기본 CSV 사용: "+e.Message; }
        }
        public bool Reload()
        {
            try {
                var candidate=GameplayBalance.Parse(File.ReadAllText(Path));
                var characters=CharacterBalanceCsv.Parse(File.ReadAllText(Characters.Path));
                Current=local=candidate;Characters.Commit(characters);Revision=++localRevision;
                Status="밸런스 적용 완료 · 버전 "+Revision;return true;
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is FormatException || e is OverflowException || e is ArgumentException)
            { Status="적용 실패 · 이전 설정 유지: "+e.Message;return false; }
        }
        public void RestoreLocal() { Current=local;Revision=localRevision; }
        public void Refuse() { Status="서브 대기 중이며 스킬 효과가 끝난 호스트에서 적용할 수 있습니다."; }
        public void Present(GameplayBalance value,uint revision) { Current=value;Revision=revision;Status="호스트 밸런스 · 버전 "+revision; }
        public static BalanceBundleStore Open()
        {
            string Arg(string name,string fallback) {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            string dir=System.IO.Path.Combine(Application.persistentDataPath,"Balance");
            return new BalanceBundleStore(Arg("-dt-gameplay-file",System.IO.Path.Combine(dir,"gameplay.csv")),Arg("-dt-balance-file",System.IO.Path.Combine(dir,"characters.csv")),
                File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","gameplay.csv")),File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","characters.csv")));
        }
    }
}
