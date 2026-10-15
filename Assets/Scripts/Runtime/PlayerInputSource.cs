using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalTennis
{
    public enum PlayerAction { Serve, Forehand, Backhand, Restart, FaultServe, LetServe }

    /// <summary>Device-independent intent. Receipt time is the local PC clock, never an IMU clock.</summary>
    public readonly struct PlayerInputSample
    {
        public readonly PlayerAction Action;
        public readonly uint EventId;
        public readonly double ReceivedAt;
        public readonly float Power01, AimX, AimZ;
        public readonly bool PracticeOpponent;

        public PlayerInputSample(PlayerAction action, uint eventId, double receivedAt, float power01 = .35f,
            float aimX = 0, float aimZ = 0, bool practiceOpponent = false)
        {
            Action = action; EventId = eventId; ReceivedAt = receivedAt; Power01 = power01;
            AimX = aimX; AimZ = aimZ; PracticeOpponent = practiceOpponent;
        }

        public bool IsValid => Enum.IsDefined(typeof(PlayerAction), Action) && EventId != 0 &&
            !double.IsNaN(ReceivedAt) && !double.IsInfinity(ReceivedAt) && ReceivedAt >= 0 &&
            BallSimulation.IsFinite(Power01) && Power01 >= 0 && Power01 <= 1 &&
            BallSimulation.IsFinite(AimX) && Mathf.Abs(AimX) <= 1 &&
            BallSimulation.IsFinite(AimZ) && Mathf.Abs(AimZ) <= 1;
    }

    public interface IPlayerInputSource
    {
        string Name { get; }
        void Poll(double now, float power01, float aimX, float aimZ);
        bool TryRead(out PlayerInputSample sample);
        void Clear();
    }

    /// <summary>Bounded ingress for a decoded board packet or another input source. No BLE SDK dependency.</summary>
    public class BufferedPlayerInputSource : IPlayerInputSource
    {
        private readonly Queue<PlayerInputSample> pending = new Queue<PlayerInputSample>();
        public string Name { get; }
        public BufferedPlayerInputSource(string name) { Name = name ?? throw new ArgumentNullException(nameof(name)); }
        public void Enqueue(PlayerInputSample sample)
        {
            lock (pending)
            {
                // Drop the oldest motion rather than executing a delayed backlog of swings.
                if (pending.Count >= 32) pending.Dequeue();
                pending.Enqueue(sample);
            }
        }
        public virtual void Poll(double now, float power01, float aimX, float aimZ) { }
        public bool TryRead(out PlayerInputSample sample)
        {
            lock (pending)
            {
                if (pending.Count == 0) { sample = default; return false; }
                sample = pending.Dequeue(); return true;
            }
        }
        public void Clear() { lock (pending) pending.Clear(); }
    }

    public sealed class KeyboardPlayerInputSource : BufferedPlayerInputSource
    {
        private uint sequence;
        private readonly Func<KeyCode, bool> keyDown;
        public KeyboardPlayerInputSource(Func<KeyCode, bool> keyDown = null) : base("Keyboard")
        { this.keyDown = keyDown ?? Input.GetKeyDown; }
        public override void Poll(double now, float power01, float aimX, float aimZ)
        {
            Key(KeyCode.Space, PlayerAction.Serve); Key(KeyCode.Q, PlayerAction.Forehand);
            Key(KeyCode.W, PlayerAction.Backhand); Key(KeyCode.E, PlayerAction.Forehand, true);
            Key(KeyCode.D, PlayerAction.Backhand, true); Key(KeyCode.R, PlayerAction.Restart);
            Key(KeyCode.F, PlayerAction.FaultServe); Key(KeyCode.L, PlayerAction.LetServe);
            void Key(KeyCode key, PlayerAction action, bool opponent = false)
            {
                if (!keyDown(key)) return;
                if (++sequence == 0) sequence = 1;
                Enqueue(new PlayerInputSample(action, sequence, now, power01, aimX, aimZ, opponent));
            }
        }
    }

    /// <summary>One input source at a time. Switching drops queued input and creates a fresh event namespace.</summary>
    public sealed class PlayerInputRouter
    {
        private readonly HashSet<uint> seen = new HashSet<uint>();
        private readonly Queue<uint> order = new Queue<uint>();
        public IPlayerInputSource Source { get; private set; }
        public uint SessionId { get; private set; } = 1000;
        public int Rejected { get; private set; }

        public void SetSource(IPlayerInputSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Source?.Clear(); source.Clear(); Source = source;
            if (++SessionId == 0) SessionId = 1001;
            seen.Clear(); order.Clear();
        }

        public void Drain(double now, Action<PlayerInputSample, uint> deliver)
        {
            if (Source == null) return;
            for (int i = 0; i < 32 && Source.TryRead(out var item); i++)
            {
                if (!item.IsValid || double.IsNaN(now) || double.IsInfinity(now) ||
                    now < item.ReceivedAt || now - item.ReceivedAt > RacketInputGate.MaxAge || !seen.Add(item.EventId))
                { Rejected++; continue; }
                order.Enqueue(item.EventId);
                if (order.Count > 64) seen.Remove(order.Dequeue());
                deliver(item, SessionId);
            }
        }
    }
}
