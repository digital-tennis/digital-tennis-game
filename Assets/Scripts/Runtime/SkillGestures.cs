using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DigitalTennis
{
    // These are device-independent motion primitives, not BLE packet or player-role fields.
    public enum GestureToken { Left,Right,Up,Down,Push,Clockwise,Counterclockwise,Hold }
    public sealed class SkillGesture
    {
        public string Id;
        public GestureToken[] Tokens;
        public double MaxGap,TotalTime,Cooldown;
    }
    public static class SkillGestureCatalog
    {
        public static Dictionary<string,SkillGesture> Parse(string csv)
        {
            if(csv==null||csv.Length>32768)throw new FormatException("Gesture CSV is too large.");
            var rows=CharacterBalanceCsv.ReadRows(csv.TrimStart('\ufeff'));
            if(rows.Count!=16||string.Join(",",rows[0])!="id,sequence,max_gap,total_time,cooldown")throw new FormatException("Gesture CSV needs all fifteen profiles.");
            var result=new Dictionary<string,SkillGesture>();var patterns=new HashSet<string>();
            foreach(var row in rows.Skip(1))
            {
                if(row.Count!=5||Array.IndexOf(CharacterCatalog.Ids,row[0])<0||result.ContainsKey(row[0])||!patterns.Add(row[1]))throw new FormatException("Duplicate or invalid gesture profile.");
                var tokens=row[1].Split('|').Select(t=>{if(!Enum.TryParse(t,out GestureToken token)||!Enum.IsDefined(typeof(GestureToken),token))throw new FormatException("Unknown gesture token.");return token;}).ToArray();
                double gap=double.Parse(row[2],CultureInfo.InvariantCulture),total=double.Parse(row[3],CultureInfo.InvariantCulture),cooldown=double.Parse(row[4],CultureInfo.InvariantCulture);
                bool Valid(double v,double lo,double hi)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=lo&&v<=hi;
                if(tokens.Length<2||tokens.Length>6||!Valid(gap,.05,3)||!Valid(total,gap,8)||!Valid(cooldown,.25,10))throw new FormatException("Gesture timing is invalid.");
                result.Add(row[0],new SkillGesture{Id=row[0],Tokens=tokens,MaxGap=gap,TotalTime=total,Cooldown=cooldown});
            }
            return result;
        }
        public static string Display(SkillGesture gesture)=>string.Join(" → ",gesture.Tokens.Select(t=>new[]{"왼쪽","오른쪽","위","아래","앞으로","시계 방향","반시계 방향","멈춤"}[(int)t]));
    }
    public sealed class SkillGestureRecognizer
    {
        private SkillGesture gesture;
        private uint lastPacket;
        private bool hasPacket;
        private int index;
        private double started,lastAt,lastFired=double.NegativeInfinity;
        public int Completed {get;private set;}
        public void Select(SkillGesture value){if(gesture==value)return;gesture=value;index=0;}
        public void Clear(){index=0;hasPacket=false;lastFired=double.NegativeInfinity;}
        public bool Push(GestureToken token,uint packetSequence,double receivedAt)
        {
            if(gesture==null||packetSequence==0||!Enum.IsDefined(typeof(GestureToken),token)||double.IsNaN(receivedAt)||double.IsInfinity(receivedAt)||receivedAt<0||
                hasPacket&&(!Sequence32.IsNewer(packetSequence,lastPacket)||receivedAt<lastAt))return false;
            hasPacket=true;lastPacket=packetSequence;
            if(receivedAt-lastFired<gesture.Cooldown){lastAt=receivedAt;index=0;return false;}
            if(index>0&&(receivedAt-lastAt>gesture.MaxGap||receivedAt-started>gesture.TotalTime))index=0;
            if(token!=gesture.Tokens[index])index=0;
            if(token==gesture.Tokens[index]){if(index==0)started=receivedAt;index++;}
            lastAt=receivedAt;
            if(index<gesture.Tokens.Length)return false;
            index=0;lastFired=receivedAt;Completed++;return true;
        }
    }
    /// <summary>Decorates any normal device source. Decoded primitive tokens may arrive on a BLE worker; only Poll emits Skill.</summary>
    public sealed class GestureSkillInputSource : BufferedPlayerInputSource
    {
        private readonly struct TokenSample
        {public readonly GestureToken Token;public readonly uint Packet;public readonly double At;public TokenSample(GestureToken token,uint packet,double at){Token=token;Packet=packet;At=at;}}
        private readonly IPlayerInputSource fallback;
        private readonly Func<int> profile;
        private readonly Dictionary<string,SkillGesture> catalog;
        private readonly ConcurrentQueue<TokenSample> tokens=new ConcurrentQueue<TokenSample>();
        private readonly SkillGestureRecognizer recognizer=new SkillGestureRecognizer();
        private readonly Stopwatch clock=new Stopwatch();
        private double offset;
        private uint sequence;
        public GestureSkillInputSource(IPlayerInputSource source,Func<int> selectedProfile,Dictionary<string,SkillGesture> gestures):base("Racket gestures + "+source.Name)
        {fallback=source;profile=selectedProfile;catalog=gestures;offset=Time.realtimeSinceStartupAsDouble;clock.Start();}
        // Receipt time is derived locally; a device timestamp can never extend the game's hit window.
        public void PushToken(GestureToken token,uint packetSequence)
        {if(tokens.Count<64)tokens.Enqueue(new TokenSample(token,packetSequence,offset+clock.Elapsed.TotalSeconds));}
        private uint Next(){if(++sequence==0)sequence=1;return sequence;}
        public override void Poll(double now,float power,float aimX,float aimZ)
        {
            fallback.Poll(now,power,aimX,aimZ);
            while(fallback.TryRead(out var sample))Enqueue(new PlayerInputSample(sample.Action,Next(),sample.ReceivedAt,sample.Power01,sample.AimX,sample.AimZ,sample.PracticeOpponent,sample.Choice));
            int selected=profile();if(!CharacterCatalog.ValidIndex(selected)){while(tokens.TryDequeue(out _)){}return;}
            recognizer.Select(catalog[CharacterCatalog.Ids[selected]]);
            while(tokens.TryDequeue(out var sample))
                if(now-sample.At<=.5&&recognizer.Push(sample.Token,sample.Packet,sample.At))Enqueue(new PlayerInputSample(PlayerAction.Skill,Next(),now,power,aimX,aimZ));
        }
        public override void Clear()
        {base.Clear();fallback.Clear();while(tokens.TryDequeue(out _)){}recognizer.Clear();offset=Time.realtimeSinceStartupAsDouble;clock.Restart();}
    }
}
