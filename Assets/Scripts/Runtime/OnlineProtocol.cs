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
        public static PlayerState Capture(string id, RacketAvatar avatar) => new PlayerState {
            id = id, pos = GamePayload.Pack(avatar.transform.position), swingId = avatar.SwingId,
            stroke = (int)avatar.LastStroke, swingAge = avatar.SwingAge };
        public bool SwingValid => BallSimulation.IsFinite(swingAge) && swingAge >= 0 && swingAge <= RacketAvatar.SwingDuration &&
            (swingId == 0 ? stroke >= 0 && stroke <= 6 : stroke >= 1 && stroke <= 6);
    }
    [Serializable] public sealed class GamePayload
    {
        public string matchId, phase, serverId, serveSide, lastHitter, status, winner, reason, kind;
        public BallState ball;
        public PlayerState[] players;
        public OfficialsState officials;
        public CharacterState[] characters;
        public int courtIndex, choice;
        public uint balanceRevision;
        public int[] points;
        public uint rallyId, shotId, lastProcessedInputSeq, eventId;
        public int faultCount, bounceCount, hits, nearHits, farHits, forehandHits, backhandHits;
        public float power01, aimX, aimZ;
        public bool hitReady;
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
                characters = c.Characters.Capture(), courtIndex = c.Characters.CourtIndex, balanceRevision = c.Characters.BalanceRevision,
                points = new[] { m.Score.NearPoints, m.Score.FarPoints }, rallyId = m.RallyId, shotId = m.CurrentShot.ShotId,
                lastProcessedInputSeq = lastInput, faultCount = m.FaultCount, bounceCount = m.BounceCount,
                hits = c.Hits, nearHits = c.NearHits, farHits = c.FarHits, forehandHits = c.ForehandHits, backhandHits = c.BackhandHits,
                hitReady = c.CanReturn(CourtSide.Far), winner = m.Score.Winner.HasValue ? (m.Score.Winner == CourtSide.Near ? "host" : "guest") : null
            };
        }

        public bool IsSnapshotValid => !string.IsNullOrEmpty(matchId) && matchId.Length <= 64 &&
            Enum.TryParse<MatchPhase>(phase, out var parsedPhase) && Enum.IsDefined(typeof(MatchPhase), parsedPhase) &&
            (serverId == "host" || serverId == "guest") && (lastHitter == "host" || lastHitter == "guest") &&
            (serveSide == "left" || serveSide == "right") && VectorValid(ball?.pos) && VectorValid(ball?.vel) &&
            players != null && players.Length == 2 && players[0]?.id == "host" && players[1]?.id == "guest" &&
            VectorValid(players[0].pos) && VectorValid(players[1].pos) && players[0].SwingValid && players[1].SwingValid && points != null && points.Length == 2 &&
            points[0] >= 0 && points[0] <= 1000000 && points[1] >= 0 && points[1] <= 1000000 &&
            rallyId != 0 && faultCount >= 0 && faultCount <= 1 && bounceCount >= 0 && bounceCount <= 100 &&
            officials != null && officials.IsValid && characters != null && characters.Length == 2 && characters[0] != null && characters[1] != null &&
            characters[0].IsValid && characters[1].IsValid && CharacterCatalog.ValidIndex(courtIndex) && balanceRevision > 0;
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
            float t = left.state == null || a.rallyId != b.Item2.rallyId || a.shotId != b.Item2.shotId ||
                a.phase != b.Item2.phase || a.officials?.recovery != b.Item2.officials?.recovery ? 0f :
                Mathf.Clamp01((float)((target - left.at) / Math.Max(.001, b.Item1 - left.at)));
            PresentedState = a;
            float age = left.state == null ? 0 : Mathf.Max(0, (float)(target - left.at));
            controller.NearAvatar.PresentRemoteSwing(a.players[0].swingId, (StrokeKind)a.players[0].stroke, a.players[0].swingAge + age);
            controller.FarAvatar.PresentRemoteSwing(a.players[1].swingId, (StrokeKind)a.players[1].stroke, a.players[1].swingAge + age);
            PresentedBallPosition = Vector3.Lerp(GamePayload.Unpack(a.ball.pos), GamePayload.Unpack(b.Item2.ball.pos), t);
            PresentedNearPosition = Vector3.Lerp(GamePayload.Unpack(a.players[0].pos), GamePayload.Unpack(b.Item2.players[0].pos), t);
            PresentedFarPosition = Vector3.Lerp(GamePayload.Unpack(a.players[1].pos), GamePayload.Unpack(b.Item2.players[1].pos), t);
            controller.PresentOnline(PresentedBallPosition, PresentedNearPosition, PresentedFarPosition);
            controller.Characters.Present(a.characters, a.courtIndex, a.balanceRevision);
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
