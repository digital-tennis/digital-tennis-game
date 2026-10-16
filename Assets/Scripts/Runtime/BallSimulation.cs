using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalTennis
{
    public enum CourtSide { Near, Far }
    public enum LandingArea { SinglesCourt, ServiceBox }
    public enum ServiceBoxSide { NegativeX, PositiveX }
    public enum BallEventKind { Bounce, Out, Net }

    public readonly struct ShotContext
    {
        public readonly uint RallyId;
        public readonly CourtSide LastHitter;
        public readonly CourtSide HitterEnd;
        public readonly LandingArea Area;
        public readonly ServiceBoxSide ServiceBox;
        public readonly uint ShotId;

        public ShotContext(uint rallyId, CourtSide lastHitter, LandingArea area = LandingArea.SinglesCourt,
            ServiceBoxSide serviceBox = ServiceBoxSide.PositiveX, uint shotId = 0, CourtSide? hitterEnd = null)
        {
            RallyId = rallyId;
            LastHitter = lastHitter;
            HitterEnd = hitterEnd??lastHitter;
            Area = area;
            ServiceBox = serviceBox;
            ShotId = shotId;
        }
    }

    public readonly struct BallEvent
    {
        public readonly uint RallyId;
        public readonly ulong EventId;
        public readonly uint ShotId;
        public readonly BallEventKind Kind;
        public readonly CourtSide LastHitter;
        public readonly Vector3 Position;
        public readonly int BounceCount;
        public readonly bool IsInBounds;
        public bool IsFirstBounce => Kind == BallEventKind.Bounce && BounceCount == 1;

        public BallEvent(ShotContext shot, ulong eventId, BallEventKind kind, Vector3 position,
            int bounceCount, bool isInBounds)
        {
            RallyId = shot.RallyId;
            EventId = eventId;
            ShotId = shot.ShotId;
            Kind = kind;
            LastHitter = shot.LastHitter;
            Position = position;
            BounceCount = bounceCount;
            IsInBounds = isInBounds;
        }
    }

    public static class CourtBounds
    {
        public const float SinglesHalfWidth = 4.115f;
        public const float HalfLength = 11.885f;
        public const float ServiceLength = 6.4f;

        // Rules use the landing centre, not the visible ball's outside edge. Lines are in.
        public static bool Contains(Vector3 landing, ShotContext shot)
        {
            if (!BallSimulation.IsFinite(landing) || Mathf.Abs(landing.x) > SinglesHalfWidth)
                return false;
            float opponentZ = shot.HitterEnd == CourtSide.Near ? landing.z : -landing.z;
            float length = shot.Area == LandingArea.ServiceBox ? ServiceLength : HalfLength;
            if (opponentZ < 0f || opponentZ > length) return false;
            return shot.Area != LandingArea.ServiceBox ||
                (shot.ServiceBox == ServiceBoxSide.PositiveX ? landing.x >= 0f : landing.x <= 0f);
        }
    }

    [Serializable]
    public sealed class BallPhysicsSettings
    {
        public float Gravity = GameplayBalance.Defaults["physics.gravity"];
        public float Radius = GameplayBalance.Defaults["physics.radius"];
        public float BounceRestitution = GameplayBalance.Defaults["physics.bounce"];
        public float GroundSpeedRetention = GameplayBalance.Defaults["physics.ground_retention"];
        public float NetRestitution = GameplayBalance.Defaults["physics.net_bounce"];
        public float RestSpeed = GameplayBalance.Defaults["physics.rest_speed"];
        public float NetHeight = 0.97f;
        public float NetHalfWidth = 4.65f;
        public float NetHalfThickness = 0.03f;

        internal BallPhysicsSettings ValidatedCopy()
        {
            foreach (float value in new[] { Gravity, Radius, RestSpeed, NetHeight, NetHalfWidth, NetHalfThickness })
                if (!BallSimulation.IsFinite(value) || value <= 0f)
                    throw new ArgumentOutOfRangeException(nameof(value), "Physics dimensions and gravity must be finite and positive.");
            foreach (float value in new[] { BounceRestitution, GroundSpeedRetention, NetRestitution })
                if (!BallSimulation.IsFinite(value) || value < 0f || value > 1f)
                    throw new ArgumentOutOfRangeException(nameof(value), "Restitution and speed retention must be in [0, 1].");
            return (BallPhysicsSettings)MemberwiseClone();
        }
    }

    /// <summary>60Hz ballistic simulation. No Rigidbody callbacks or scoring dependencies.</summary>
    public sealed class BallSimulation
    {
        public const float FixedStep = 1f / 60f;
        private const float ContactOffset = 0.0001f;
        private BallPhysicsSettings settings;
        private readonly List<BallEvent> events = new List<BallEvent>(4);
        private ulong nextEventId;
        private bool hasShot;

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public ShotContext Shot { get; private set; }
        public int BounceCount { get; private set; }
        public uint Tick { get; private set; }
        public bool IsResting { get; private set; } = true;
        public bool HasShot => hasShot;
        public float Radius => settings.Radius;
        public float Gravity => settings.Gravity;
        public float NetHeight => settings.NetHeight;
        public float BounceRestitution => settings.BounceRestitution;
        public float GroundSpeedRetention => settings.GroundSpeedRetention;

        public BallSimulation(BallPhysicsSettings settings = null)
        {
            this.settings = (settings ?? new BallPhysicsSettings()).ValidatedCopy();
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        // Prediction cannot publish events or mutate the authoritative ball.
        public BallSimulation CreatePrediction() => new BallSimulation(settings)
        {
            Position = Position, Velocity = Velocity, Shot = Shot, BounceCount = BounceCount,
            Tick = Tick, IsResting = IsResting, hasShot = hasShot, nextEventId = nextEventId
        };

        // At a dead-ball boundary only. Keep monotonically increasing contact IDs and listeners.
        public void Configure(BallPhysicsSettings value)
        {
            if (!IsResting) throw new InvalidOperationException("Stop the ball before changing physics.");
            settings = value.ValidatedCopy();
        }
        public void Stop()
        {
            Velocity = Vector3.zero;
            IsResting = true;
        }

        public void ResetAt(Vector3 position)
        {
            if (!IsFinite(position) || position.y < settings.Radius)
                throw new ArgumentException("Reset needs a finite position above the ground.");
            Stop();
            Position = position;
            Tick = 0;
            BounceCount = 0;
            hasShot = false;
            events.Clear();
        }

        public void Launch(Vector3 position, Vector3 velocity, ShotContext shot)
        {
            if (!IsFinite(position) || !IsFinite(velocity) || !IsFinite(velocity.sqrMagnitude) ||
                position.y < settings.Radius)
                throw new ArgumentException("Launch needs finite values and a ball above the ground.");
            if (shot.RallyId == 0 || !Enum.IsDefined(typeof(CourtSide), shot.LastHitter) || !Enum.IsDefined(typeof(CourtSide), shot.HitterEnd) ||
                !Enum.IsDefined(typeof(LandingArea), shot.Area) || !Enum.IsDefined(typeof(ServiceBoxSide), shot.ServiceBox))
                throw new ArgumentException("A valid shot context and a nonzero rally ID are required.");
            if (Mathf.Abs(position.z) < settings.NetHalfThickness + settings.Radius &&
                Mathf.Abs(position.x) < settings.NetHalfWidth + settings.Radius &&
                position.y < settings.NetHeight + settings.Radius)
                throw new ArgumentException("A shot cannot start inside the net.");
            Position = position;
            Velocity = velocity;
            Shot = shot;
            BounceCount = 0;
            Tick = 0;
            hasShot = true;
            IsResting = position.y == settings.Radius && velocity.y == 0f;
            if (IsResting) Velocity = Vector3.zero;
            events.Clear();
        }

        // The returned buffer is valid until the next Step or Launch. Consumers copy retained events.
        public IReadOnlyList<BallEvent> Step()
        {
            events.Clear();
            if (!hasShot || IsResting) return events;
            Tick++;
            float remaining = FixedStep;
            for (int contact = 0; contact < 8 && remaining > 0f && !IsResting; contact++)
            {
                float time = DescendingTime(settings.Radius);
                bool ground = time <= remaining;
                if (!ground) time = remaining;
                Vector3 normal = Vector3.up;
                bool net = FindNetContact(remaining, ref time, ref normal);
                if (net) ground = false;
                Move(time);
                remaining = Mathf.Max(0f, remaining - time);
                if (!ground && !net) return events;
                if (ground)
                {
                    Position = new Vector3(Position.x, settings.Radius, Position.z);
                    BounceCount++;
                    Vector3 landing = new Vector3(Position.x, 0f, Position.z);
                    bool isIn = CourtBounds.Contains(landing, Shot);
                    AddEvent(BallEventKind.Bounce, landing, isIn);
                    if (BounceCount == 1 && !isIn) AddEvent(BallEventKind.Out, landing, false);
                    float reboundSpeed = Mathf.Max(0f, -Velocity.y * settings.BounceRestitution);
                    IsResting = reboundSpeed < settings.RestSpeed;
                    Velocity = IsResting ? Vector3.zero : new Vector3(
                        Velocity.x * settings.GroundSpeedRetention, reboundSpeed, Velocity.z * settings.GroundSpeedRetention);
                }
                else
                {
                    Position += normal * ContactOffset;
                    Velocity = Vector3.Reflect(Velocity, normal) * settings.NetRestitution;
                    if (normal == Vector3.up && Velocity.y < settings.RestSpeed)
                    {
                        IsResting = true;
                        Velocity = Vector3.zero;
                    }
                    AddEvent(BallEventKind.Net, Position, false);
                }
            }
            if (!IsResting && remaining > 0f)
                throw new InvalidOperationException("Too many ball contacts in one physics tick.");
            return events;
        }

        private void AddEvent(BallEventKind kind, Vector3 position, bool isIn)
        {
            events.Add(new BallEvent(Shot, ++nextEventId, kind, position, BounceCount, isIn));
        }

        private Vector3 PositionAt(float time) => Position + Velocity * time +
            Vector3.down * (0.5f * settings.Gravity * time * time);

        private void Move(float time)
        {
            Position = PositionAt(time);
            Velocity += Vector3.down * (settings.Gravity * time);
        }

        private float DescendingTime(float height)
        {
            float distance = Position.y - height;
            if (distance < 0f) return float.PositiveInfinity;
            return (Velocity.y + Mathf.Sqrt(Velocity.y * Velocity.y + 2f * settings.Gravity * distance)) / settings.Gravity;
        }

        // Sweep to the net face/top inside the tick; test the ballistic position at impact.
        private bool FindNetContact(float remaining, ref float earliest, ref Vector3 normal)
        {
            bool found = false;
            float extent = settings.NetHalfThickness + settings.Radius;
            if (Mathf.Abs(Velocity.z) > 0.00001f)
            {
                float face = Velocity.z > 0f ? -extent : extent;
                float candidate = (face - Position.z) / Velocity.z;
                if (candidate >= 0f && candidate <= remaining && candidate <= earliest)
                {
                    Vector3 at = PositionAt(candidate);
                    if (Mathf.Abs(at.x) <= settings.NetHalfWidth + settings.Radius &&
                        at.y >= settings.Radius && at.y <= settings.NetHeight + settings.Radius)
                    {
                        earliest = candidate;
                        normal = Velocity.z > 0f ? Vector3.back : Vector3.forward;
                        found = true;
                    }
                }
            }
            float top = DescendingTime(settings.NetHeight + settings.Radius);
            if (Mathf.Abs(Velocity.x) > 0.00001f)
            {
                float side = settings.NetHalfWidth + settings.Radius;
                float face = Velocity.x > 0f ? -side : side;
                float candidate = (face - Position.x) / Velocity.x;
                if (candidate >= 0f && candidate <= remaining && candidate <= earliest)
                {
                    Vector3 at = PositionAt(candidate);
                    if (Mathf.Abs(at.z) <= extent && at.y >= settings.Radius && at.y <= settings.NetHeight + settings.Radius)
                    {
                        earliest = candidate;
                        normal = Velocity.x > 0f ? Vector3.left : Vector3.right;
                        found = true;
                    }
                }
            }
            if (top >= 0f && top <= remaining && top <= earliest)
            {
                Vector3 at = PositionAt(top);
                if (Mathf.Abs(at.z) <= extent && Mathf.Abs(at.x) <= settings.NetHalfWidth + settings.Radius)
                {
                    earliest = top;
                    normal = Vector3.up;
                    found = true;
                }
            }
            return found;
        }
    }
}
