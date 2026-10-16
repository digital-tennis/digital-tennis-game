using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalTennis
{
    [Serializable] public sealed class WireEnvelope<T>
    {
        public int v = 1;
        public string type, requestId;
        public T payload;
    }
    [Serializable] public sealed class RoomRequest
    {
        public string code, reconnectToken, mode;
        public uint epoch;
        public double clientTimeMs;
        public GameMessage message;
    }
    [Serializable] public sealed class ServerMessagePayload
    {
        public string code, playerId, reconnectToken, state, from, mode, reason;
        public uint epoch;
        public double clientTimeMs;
        public bool resume;
        public GameMessage message;
    }
    // Error messages have a string 'message'; parse them separately from the game message object.
    [Serializable] public sealed class ErrorPayload { public string code, message; }
    [Serializable] public sealed class EnvelopeHeader { public int v; public string type; }
    [Serializable] public sealed class GameMessage
    {
        public int v = 1;
        public string type, playerId;
        public uint epoch, inputSeq, tick;
        public GamePayload payload;
    }
    [Serializable] public sealed class BallState { public float[] pos, vel; }
    [Serializable] public sealed class OfficialsState
    {
        public uint callId;
        public int call, recovery, completed;
        public string text;
        public float[] runner;
        public bool IsValid => callId != 0 && Enum.IsDefined(typeof(UmpireCall), call) &&
            Enum.IsDefined(typeof(RecoveryPhase), recovery) && completed >= 0 && text != null && text.Length <= 256 &&
            GamePayload.VectorValid(runner);
        public static OfficialsState Capture(TennisMatchController c) => new OfficialsState {
            callId = c.Match.CallId, call = (int)c.Match.Call, text = c.Match.CallText,
            recovery = (int)c.Recovery.Phase, runner = GamePayload.Pack(c.Recovery.Position), completed = c.Recovery.Completed };
    }
    [Serializable] public sealed class PlayerState
    {
        public string id;
        public float[] pos;
        public uint swingId;
        public int stroke;
        public float swingAge;
        public bool swingContact,swingServe,hasContactPoint;
        public float[] contactPoint;
        public static PlayerState Capture(string id, RacketAvatar avatar) => new PlayerState {
            id = id, pos = GamePayload.Pack(avatar.transform.position), swingId = avatar.SwingId,
            stroke = (int)avatar.LastStroke, swingAge = avatar.SwingAge,swingContact=avatar.LastSwingContact,swingServe=avatar.IsServe,hasContactPoint=avatar.HasContactPoint,contactPoint=GamePayload.Pack(avatar.ContactPoint) };
        public bool SwingValid => BallSimulation.IsFinite(swingAge) && swingAge >= 0 && swingAge <= RacketAvatar.SwingDuration &&
            (swingId == 0 ? stroke >= 0 && stroke <= 6 : stroke >= 1 && stroke <= 6) &&
            (!hasContactPoint||(swingContact&&contactPoint!=null&&contactPoint.Length==3&&System.Array.TrueForAll(contactPoint,v=>BallSimulation.IsFinite(v)&&Mathf.Abs(v)<128)));
    }
    [Serializable] public sealed class MatchScoreState
    {
        public int version=1;
        public int[] rules,games,sets;
        public long[] totalPoints;
        public CompletedTennisSet[] completedSets;
        public bool tieBreak,gameComplete,setComplete;
        public int endChanges;
        public string gameServer;
        public bool EndsChanged => endChanges%2!=0;
        public static MatchScoreState Capture(TennisMatchScore s) => new MatchScoreState {
            rules=s.Rules.Values,games=new[]{s.NearGames,s.FarGames},sets=new[]{s.NearSets,s.FarSets},
            totalPoints=new[]{s.NearTotalPoints,s.FarTotalPoints},completedSets=s.CompletedSets,tieBreak=s.TieBreak,
            gameComplete=s.GameComplete,setComplete=s.SetComplete,endChanges=s.EndChanges,gameServer=s.GameServer==CourtSide.Near?"host":"guest" };
        private static bool Pair(int[] p) => p!=null&&p.Length==2&&p[0]>=0&&p[1]>=0&&p[0]<=1000000&&p[1]<=1000000;
        public bool ValidFor(GamePayload state)
        {
            if(version!=1||!Pair(games)||!Pair(sets)||!Pair(state.points)||endChanges<0||endChanges>1000000||
                totalPoints==null||totalPoints.Length!=2||totalPoints[0]<state.points[0]||totalPoints[1]<state.points[1]||
                totalPoints[0]>1000000000||totalPoints[1]>1000000000||completedSets==null||
                (gameServer!="host"&&gameServer!="guest"))return false;
            TennisMatchRules format;
            try {format=TennisMatchRules.FromValues(rules);}catch(FormatException){return false;}
            if(completedSets.Length>format.BestOfSets||sets[0]>format.SetsToWin||sets[1]>format.SetsToWin||
                games[0]>format.TieBreakAt+1||games[1]>format.TieBreakAt+1)return false;
            var counted=new int[2];
            foreach(var set in completedSets)
            {
                if(set==null||set.nearGames<0||set.farGames<0||set.nearTieBreak<0||set.farTieBreak<0||
                    Math.Max(set.nearTieBreak,set.farTieBreak)>1000000)return false;
                int high=Math.Max(set.nearGames,set.farGames),low=Math.Min(set.nearGames,set.farGames);
                bool tb=high==format.TieBreakAt+1&&low==format.TieBreakAt;
                if(tb)
                {
                    if(Math.Max(set.nearTieBreak,set.farTieBreak)<format.TieBreakPoints||Math.Abs((long)set.nearTieBreak-set.farTieBreak)<2||
                        (set.nearGames>set.farGames)!=(set.nearTieBreak>set.farTieBreak))return false;
                }
                else if(high>format.TieBreakAt+1||!((high==format.GamesPerSet&&low<=format.GamesPerSet-2)||(high==format.GamesPerSet+1&&low==format.GamesPerSet-1))||
                    set.nearTieBreak!=0||set.farTieBreak!=0)return false;
                counted[(int)set.Winner]++;
                if(Math.Max(counted[0],counted[1])==format.SetsToWin&&set!=completedSets[completedSets.Length-1])return false;
            }
            if(counted[0]!=sets[0]||counted[1]!=sets[1]||setComplete&&!gameComplete)return false;
            bool won=sets[0]==format.SetsToWin||sets[1]==format.SetsToWin;
            if(won!=(state.phase=="MatchEnd")||won&&!setComplete||
                (won?state.winner!=(sets[0]>sets[1]?"host":"guest"):!string.IsNullOrEmpty(state.winner)))return false;
            if(setComplete&&(completedSets.Length==0||completedSets[completedSets.Length-1].nearGames!=games[0]||completedSets[completedSets.Length-1].farGames!=games[1]))return false;
            bool pointWin=tieBreak?Math.Max(state.points[0],state.points[1])>=format.TieBreakPoints&&Math.Abs((long)state.points[0]-state.points[1])>=2:
                new TennisScore(state.points[0],state.points[1]).Winner.HasValue;
            if(gameComplete!=pointWin||gameComplete&&state.phase!="PointEnd"&&state.phase!="MatchEnd")return false;
            if(!gameComplete&&tieBreak!=(games[0]==format.TieBreakAt&&games[1]==format.TieBreakAt))return false;
            if(setComplete!=(gameComplete&&(tieBreak||(Math.Max(games[0],games[1])>=format.GamesPerSet&&Math.Abs(games[0]-games[1])>=2))))return false;
            if(tieBreak&&!(games[0]==format.TieBreakAt&&games[1]==format.TieBreakAt)&&!setComplete)return false;
            long total=(long)state.points[0]+state.points[1];
            bool alternate=tieBreak&&total>0&&((total+1)/2)%2==1;
            string server=alternate?(gameServer=="host"?"guest":"host"):gameServer;
            return state.serverId==server;
        }
        public string PointDisplay(int[] points,int player) => tieBreak?points[player].ToString():new TennisScore(points[0],points[1]).Display((CourtSide)player);
        public string Summary => "세트 "+sets[0]+"–"+sets[1]+"  |  "+string.Join(" / ",Array.ConvertAll(completedSets,s=>s.Display()));
    }
    [Serializable] public sealed class GamePayload
    {
        public string matchId, phase, serverId, serveSide, lastHitter, status, winner, reason, kind;
        public BallState ball;
        public PlayerState[] players;
        public OfficialsState officials;
        public CharacterState[] characters;
        public int courtIndex, choice;
        public bool[] rematchVotes=new[]{false,false};
        public uint balanceRevision;
        public string gameplayHash;
        public float[] gameplayValues;
        [NonSerialized] private GameplayBalance decodedBalance;
        public GameplayBalance HostBalance => decodedBalance;
        public int[] points;
        public MatchScoreState score;
        public uint rallyId, shotId, lastProcessedInputSeq, eventId;
        public int faultCount, bounceCount, hits, nearHits, farHits, forehandHits, backhandHits;
        public float power01, aimX, aimZ;
        public bool hitReady;
        public uint effectSequence;
        public TennisEffectEvent[] effects;
        public static float[] Pack(Vector3 p) => new[] { p.x, p.y, p.z };
        public static Vector3 Unpack(float[] p) => new Vector3(p[0], p[1], p[2]);

        public static GamePayload Capture(TennisMatchController c, string matchId, uint lastInput)
        {
            var m = c.Match;
            return new GamePayload {
                matchId = matchId, phase = m.Phase.ToString(), serverId = m.Server == CourtSide.Near ? "host" : "guest",
                serveSide = m.ServeSide == ServeSide.Left ? "left" : "right", lastHitter = m.LastHitter == CourtSide.Near ? "host" : "guest",
                status = m.Status, ball = new BallState { pos = Pack(c.Ball.transform.position), vel = Pack(c.Ball.Simulation.Velocity) },
                players = new[] { PlayerState.Capture("host", c.NearAvatar), PlayerState.Capture("guest", c.FarAvatar) },
                officials = OfficialsState.Capture(c),
                gameplayHash=c.Tuning.Hash,gameplayValues=c.Tuning.Values,
                rematchVotes=c.GetComponent<OnlineTennisSession>()?.RematchVotes??new[]{false,false},
                characters = c.Characters.Capture(), courtIndex = c.Characters.CourtIndex, balanceRevision = c.Characters.BalanceRevision,
                effectSequence=c.Effects?.Sequence??0,effects=c.Effects?.Capture(),
                points = new[] { m.Score.NearPoints, m.Score.FarPoints }, score=MatchScoreState.Capture(m.Progress),rallyId = m.RallyId, shotId = m.CurrentShot.ShotId,
                lastProcessedInputSeq = lastInput, faultCount = m.FaultCount, bounceCount = m.BounceCount,
                hits = c.Hits, nearHits = c.NearHits, farHits = c.FarHits, forehandHits = c.ForehandHits, backhandHits = c.BackhandHits,
                hitReady = c.CanReturn(CourtSide.Far), winner = m.Winner.HasValue ? (m.Winner == CourtSide.Near ? "host" : "guest") : null
            };
        }

        public bool IsSnapshotValid => (rematchVotes==null||rematchVotes.Length==2) && !string.IsNullOrEmpty(matchId) && matchId.Length <= 64 &&
            Enum.TryParse<MatchPhase>(phase, out var parsedPhase) && Enum.IsDefined(typeof(MatchPhase), parsedPhase) &&
            (serverId == "host" || serverId == "guest") && (lastHitter == "host" || lastHitter == "guest") &&
            (serveSide == "left" || serveSide == "right") && VectorValid(ball?.pos) && VectorValid(ball?.vel) &&
            players != null && players.Length == 2 && players[0]?.id == "host" && players[1]?.id == "guest" &&
            VectorValid(players[0].pos) && VectorValid(players[1].pos) && players[0].SwingValid && players[1].SwingValid && points != null && points.Length == 2 &&
            points[0] >= 0 && points[0] <= 1000000 && points[1] >= 0 && points[1] <= 1000000 &&
            score!=null&&score.ValidFor(this)&&
            rallyId != 0 && faultCount >= 0 && faultCount <= 1 && bounceCount >= 0 && bounceCount <= 100 &&
            officials != null && officials.IsValid && characters != null && characters.Length == 2 && characters[0] != null && characters[1] != null &&
            characters[0].IsValid && characters[1].IsValid && CharacterCatalog.ValidIndex(courtIndex) && balanceRevision > 0 && GameplayBalance.TryWire(gameplayValues,gameplayHash,out decodedBalance) && TennisEffectJournal.Valid(effectSequence,effects);
        public static bool VectorValid(float[] p) => p != null && p.Length == 3 &&
            BallSimulation.IsFinite(p[0]) && BallSimulation.IsFinite(p[1]) && BallSimulation.IsFinite(p[2]) &&
            Mathf.Abs(p[0]) <= 1000 && Mathf.Abs(p[1]) <= 1000 && Mathf.Abs(p[2]) <= 1000;
    }

    public static class Sequence32
    {
        public static bool IsNewer(uint candidate, uint previous) => candidate != previous && unchecked(candidate - previous) < 0x80000000u;
    }

    /// <summary>Guest presentation only; never advances BallSimulation or awards a point.</summary>
    public sealed class SnapshotBuffer
    {
        private readonly Queue<(double at, GamePayload state)> frames = new Queue<(double, GamePayload)>();
        private (double at, GamePayload state) left;
        private bool hasTick;
        private uint lastTick;
        public GamePayload Latest { get; private set; }
        public int Received { get; private set; }
        public GamePayload PresentedState { get; private set; }
        public Vector3 PresentedNearPosition { get; private set; }
        public Vector3 PresentedFarPosition { get; private set; }
        public Vector3 PresentedBallPosition { get; private set; }
        public Vector3 PresentedRunnerPosition { get; private set; }
        public void Clear() { frames.Clear(); left = default; Latest = null; PresentedState = null; hasTick = false; }
        public bool Push(GameMessage message, double now)
        {
            if (message?.type != "snapshot" || message.payload == null || !message.payload.IsSnapshotValid ||
                (hasTick && !Sequence32.IsNewer(message.tick, lastTick))) return false;
            if(Latest!=null&&Latest.matchId!=message.payload.matchId){frames.Clear();left=default;PresentedState=null;}
            hasTick = true; lastTick = message.tick; Latest = message.payload; Received++;
            frames.Enqueue((now, Latest));
            while (frames.Count > 8) left = frames.Dequeue();
            return true;
        }
        public void Present(double now, TennisMatchController controller)
        {
            double target = now - .1;
            while (frames.Count > 0 && frames.Peek().at <= target) left = frames.Dequeue();
            var a = left.state ?? (frames.Count > 0 ? frames.Peek().state : Latest);
            if (a == null) return;
            var b = frames.Count > 0 ? frames.Peek() : (now, a);
            // A point/shot boundary teleports to the next state instead of interpolating across a serve reset.
            float t = left.state == null || a.matchId != b.Item2.matchId || a.rallyId != b.Item2.rallyId || a.shotId != b.Item2.shotId ||
                a.phase != b.Item2.phase || a.score.endChanges!=b.Item2.score.endChanges || a.officials?.recovery != b.Item2.officials?.recovery ? 0f :
                Mathf.Clamp01((float)((target - left.at) / Math.Max(.001, b.Item1 - left.at)));
            PresentedState = a;
            controller.PresentEnds(a.score.EndsChanged);
            float age = left.state == null ? 0 : Mathf.Max(0, (float)(target - left.at));
            controller.NearAvatar.PresentRemoteSwing(a.players[0].swingId, (StrokeKind)a.players[0].stroke, a.players[0].swingAge + age,a.players[0].swingContact,a.players[0].swingServe,a.players[0].hasContactPoint?(Vector3?)GamePayload.Unpack(a.players[0].contactPoint):null);
            controller.FarAvatar.PresentRemoteSwing(a.players[1].swingId, (StrokeKind)a.players[1].stroke, a.players[1].swingAge + age,a.players[1].swingContact,a.players[1].swingServe,a.players[1].hasContactPoint?(Vector3?)GamePayload.Unpack(a.players[1].contactPoint):null);
            PresentedBallPosition = Vector3.Lerp(GamePayload.Unpack(a.ball.pos), GamePayload.Unpack(b.Item2.ball.pos), t);
            PresentedNearPosition = Vector3.Lerp(GamePayload.Unpack(a.players[0].pos), GamePayload.Unpack(b.Item2.players[0].pos), t);
            PresentedFarPosition = Vector3.Lerp(GamePayload.Unpack(a.players[1].pos), GamePayload.Unpack(b.Item2.players[1].pos), t);
            controller.PresentOnline(PresentedBallPosition, PresentedNearPosition, PresentedFarPosition);
            controller.PresentBalance(a.HostBalance,a.balanceRevision);
            controller.Characters.Present(a.characters, a.courtIndex, a.balanceRevision);
            controller.Effects?.PresentRemote(a.matchId,a.effectSequence,a.effects,age);
            if (a.officials != null)
            {
                var o = a.officials;
                PresentedRunnerPosition = Vector3.Lerp(GamePayload.Unpack(o.runner),
                    GamePayload.Unpack((b.Item2.officials ?? o).runner), t);
                controller.Officials.Present(o.callId, (UmpireCall)o.call, o.text, (RecoveryPhase)o.recovery,
                    PresentedRunnerPosition, o.completed);
            }
        }
    }
}
