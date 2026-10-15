using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    public enum TennisEffectKind { Serve, Hit, Bounce, Charge, Skill }
    public enum EffectQuality { Low, Normal, Rich }

    [Serializable] public sealed class TennisEffectEvent
    {
        public uint id;
        public int kind, player, character, stroke;
        public float age;
        public float[] position, direction;
        public bool IsValid => id != 0 && kind >= 0 && kind <= 4 && player >= 0 && player <= 1 &&
            CharacterCatalog.ValidIndex(character) && stroke >= 0 && stroke <= 6 &&
            BallSimulation.IsFinite(age) && age >= 0 && age <= 2 &&
            GamePayload.VectorValid(position) && GamePayload.VectorValid(direction) && GamePayload.Unpack(direction).sqrMagnitude <= 1.01f;
        public TennisEffectEvent AtAge(float value) => new TennisEffectEvent { id=id,kind=kind,player=player,character=character,
            stroke=stroke,age=value,position=(float[])position.Clone(),direction=(float[])direction.Clone() };
    }

    /// <summary>Small authoritative cosmetic history; no simulation, scoring or device input.</summary>
    public sealed class TennisEffectJournal
    {
        public const int Capacity = 16;
        private readonly List<(float at,TennisEffectEvent item)> entries = new List<(float,TennisEffectEvent)>();
        public uint Sequence { get; private set; }
        public void Clear() { entries.Clear(); Sequence=0; }
        public TennisEffectEvent Add(TennisEffectKind kind,CourtSide side,int character,StrokeKind stroke,Vector3 position,Vector3 direction,float now)
        {
            if(++Sequence==0)Sequence=1;
            var e=new TennisEffectEvent {id=Sequence,kind=(int)kind,player=(int)side,character=character,stroke=(int)stroke,
                position=GamePayload.Pack(position),direction=GamePayload.Pack(direction.normalized)};
            if(!e.IsValid)throw new ArgumentException("Invalid cosmetic event.");
            entries.Add((now,e));if(entries.Count>Capacity)entries.RemoveAt(0);return e;
        }
        public TennisEffectEvent[] Capture(float now)
        {
            var result=new List<TennisEffectEvent>();
            foreach(var e in entries) {float age=Mathf.Max(0,now-e.at);if(age<=2)result.Add(e.item.AtAge(age));}
            return result.ToArray();
        }
        public static bool Valid(uint sequence,TennisEffectEvent[] events)
        {
            if(events==null)return sequence==0;
            if(events.Length>Capacity)return false;
            uint previous=0;
            for(int i=0;i<events.Length;i++)
            {var e=events[i];if(e==null||!e.IsValid||(i>0&&!Sequence32.IsNewer(e.id,previous)))return false;previous=e.id;}
            return events.Length==0 || previous==sequence;
        }
    }

    /// <summary>First snapshot of a match/epoch is a checkpoint, never an instruction to replay old flashes.</summary>
    public sealed class TennisEffectGate
    {
        private bool baseline=true;
        private uint last;
        private string match;
        public int Baselines { get; private set; }
        public void Reset() {baseline=true;}
        public void Consume(string matchId,uint sequence,TennisEffectEvent[] events,float extraAge,Action<TennisEffectEvent> play)
        {
            if(!TennisEffectJournal.Valid(sequence,events))return;
            if(baseline||match!=matchId) {baseline=false;match=matchId;last=sequence;Baselines++;return;}
            if(!Sequence32.IsNewer(sequence,last))return;
            if(events!=null)foreach(var item in events)
                if(Sequence32.IsNewer(item.id,last))play(item.AtAge(Mathf.Min(2,item.age+Mathf.Max(0,extraAge))));
            last=sequence;
        }
    }

    public sealed class TennisEffectPreset
    {
        public bool Enabled;
        public int Count;
        public float Duration, Size, Speed;
        public string Tint;
    }
    public static class TennisEffectsCsv
    {
        public const string Header="effect,enabled,count,duration,size,speed,color";
        private static readonly string[] Names={"serve","hit","bounce","charge","skill"};
        public static TennisEffectPreset[] Parse(string text)
        {
            if(text==null||text.Length>32768)throw new FormatException("Effects CSV is too large.");
            var rows=text.TrimStart('\ufeff').Trim().Replace("\r","").Split('\n');
            if(rows.Length!=6||rows[0]!=Header)throw new FormatException("Effects CSV needs its header and five effect rows.");
            var result=new TennisEffectPreset[5];
            for(int row=1;row<rows.Length;row++)
            {
                var c=rows[row].Split(',');if(c.Length!=7)throw new FormatException("Effects CSV needs seven columns.");
                int index=Array.IndexOf(Names,c[0]);
                if(index<0||result[index]!=null||(c[1]!="0"&&c[1]!="1"))throw new FormatException("Unknown or duplicate effect.");
                float N(int col)=>float.Parse(c[col],NumberStyles.Float,CultureInfo.InvariantCulture);
                var p=new TennisEffectPreset {Enabled=c[1]=="1",Count=int.Parse(c[2],CultureInfo.InvariantCulture),Duration=N(3),Size=N(4),Speed=N(5),Tint=c[6]};
                if(p.Count<0||p.Count>64||!Range(p.Duration,.06f,index==4?1.2f:.8f)||!Range(p.Size,.05f,2)||!Range(p.Speed,0,5)||
                    (p.Tint!="character"&&(!ColorUtility.TryParseHtmlString(p.Tint,out var color)||color.a<.5f)))throw new FormatException("Effect value outside safe visual limits.");
                result[index]=p;
            }
            return result;
        }
        private static bool Range(float n,float lo,float hi)=>BallSimulation.IsFinite(n)&&n>=lo&&n<=hi;
    }
    public sealed class TennisEffectStore
    {
        public string Path { get; }
        public string Status { get; private set; }
        public uint Revision { get; private set; }=1;
        private TennisEffectPreset[] presets;
        public TennisEffectPreset this[TennisEffectKind kind]=>presets[(int)kind];
        public TennisEffectStore(string path,string defaults)
        {
            Path=path;presets=TennisEffectsCsv.Parse(defaults);
            try {Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));if(!File.Exists(path))File.WriteAllText(path,defaults);Reload();}
            catch(IOException){Status="기본 효과 사용 · 파일을 확인하세요.";}
            catch(UnauthorizedAccessException){Status="기본 효과 사용 · 파일 권한을 확인하세요.";}
        }
        public bool Reload()
        {
            try
            {
                if(new FileInfo(Path).Length>32768)throw new FormatException();
                var next=TennisEffectsCsv.Parse(File.ReadAllText(Path));presets=next;Revision++;Status="효과 CSV 적용 완료";return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is FormatException||e is OverflowException)
            {Status="효과 CSV 오류 · 이전 설정 유지";return false;}
        }
    }
}
