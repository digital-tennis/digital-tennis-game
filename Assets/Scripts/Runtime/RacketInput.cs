using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalTennis
{
    public enum StrokeKind { Forehand = 1, Backhand = 2, Smash = 3, Volley = 4, Slice = 5, Lob = 6 }
    public enum SwingResult { Hit, Duplicate, Invalid, Stale, WrongPhase, OutOfReach, WrongSide, Height, NoBall, NotAuthority }

    /// <summary>Normalized game input. A source stamps receipt time on the PC clock and captures the incoming shot.</summary>
    public readonly struct RacketInput
    {
        public readonly CourtSide Player;
        public readonly StrokeKind Stroke;
        public readonly uint SessionId, EventId, RallyId, IncomingShotId;
        public readonly float Power01, AimX, AimZ;
        public readonly double ReceivedAt;

        public RacketInput(CourtSide player, StrokeKind stroke, uint sessionId, uint eventId,
            float power01, float aimX, float aimZ, double receivedAt, uint rallyId, uint incomingShotId)
        {
            Player = player; Stroke = stroke; SessionId = sessionId; EventId = eventId;
            Power01 = power01; AimX = aimX; AimZ = aimZ; ReceivedAt = receivedAt;
            RallyId = rallyId; IncomingShotId = incomingShotId;
        }

        public bool IsValid => (Player == CourtSide.Near || Player == CourtSide.Far) &&
            System.Enum.IsDefined(typeof(StrokeKind), Stroke) &&
            SessionId != 0 && EventId != 0 && RallyId != 0 && IncomingShotId != 0 &&
            BallSimulation.IsFinite(Power01) && Power01 >= 0f && Power01 <= 1f &&
            BallSimulation.IsFinite(AimX) && Mathf.Abs(AimX) <= 1f &&
            BallSimulation.IsFinite(AimZ) && Mathf.Abs(AimZ) <= 1f &&
            !double.IsNaN(ReceivedAt) && !double.IsInfinity(ReceivedAt) && ReceivedAt >= 0;
    }

    /// <summary>Keyboard, buttons and the demonstration use the same entry point as a future BLE adapter.</summary>
    public sealed class VirtualRacketInput
    {
        private uint eventId;
        public RacketInput Create(CourtSide player, StrokeKind stroke, float power, float aimX, float aimZ,
            double receivedAt, TennisMatch match)
        {
            if (++eventId == 0) eventId = 1;
            return new RacketInput(player, stroke, 1, eventId, power, aimX, aimZ, receivedAt,
                match.RallyId, match.CurrentShot.ShotId);
        }
    }

    public static class RacketShot
    {
        public static Vector3 CharacterVelocity(RacketInput input, Vector3 origin, float gravity, float radius, float multiplier, GameplayBalance settings = null)
        {
            var c = settings ?? GameplayBalance.Defaults;
            if (input.Stroke == StrokeKind.Smash)
            {
                float time = c["smash.flight"] / multiplier;
                var target = new Vector3(Mathf.Clamp(origin.x + input.AimX * c["smash.aim_width"], -c["smash.target_width"], c["smash.target_width"]), radius,
                    (input.Player == CourtSide.Near ? 1 : -1) * (c["smash.target_depth"] + input.AimZ * c["smash.aim_depth"]));
                return (target - origin) / time + Vector3.up * (.5f * gravity * time);
            }
            Vector3 velocity = Velocity(input,c);
            if (input.Stroke == StrokeKind.Lob) velocity = new Vector3(velocity.x * c["lob.horizontal"], c["lob.lift"] + input.AimZ * c["lob.aim_lift"], velocity.z * c["lob.forward"]);
            if (input.Stroke == StrokeKind.Slice) velocity = new Vector3(velocity.x, velocity.y * c["slice.lift"], velocity.z * c["slice.forward"]);
            if (input.Stroke == StrokeKind.Volley) velocity = new Vector3(velocity.x, velocity.y * c["volley.lift"], velocity.z * c["volley.forward"]);
            return velocity * multiplier;
        }
        public static Vector3 Velocity(RacketInput input, GameplayBalance settings = null)
        {
            if (!input.IsValid) throw new ArgumentException("Invalid normalized racket input.", nameof(input));
            var c = settings ?? GameplayBalance.Defaults;
            float speed = c["shot.base_speed"] + c["shot.power_speed"] * input.Power01;
            float yaw = input.AimX * c["shot.yaw"] * Mathf.Deg2Rad;
            float pitch = (c["shot.pitch"] + c["shot.pitch_aim"] * input.AimZ) * Mathf.Deg2Rad;
            // Rotate the player's local right/forward into court space exactly once.
            float facing = input.Player == CourtSide.Near ? 1f : -1f;
            return new Vector3(facing * Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch),
                facing * Mathf.Cos(yaw) * Mathf.Cos(pitch)) * speed;
        }
    }

    public sealed class RacketInputGate
    {
        public static float Reach => GameplayBalance.Defaults["hit.reach"];
        public static float MinHeight => GameplayBalance.Defaults["hit.min_height"];
        public static float MaxHeight => GameplayBalance.Defaults["hit.max_height"];
        public const double MaxAge = 0.25;
        private readonly HashSet<(CourtSide, uint, uint)> seen = new HashSet<(CourtSide, uint, uint)>();
        private readonly Queue<(CourtSide, uint, uint)> order = new Queue<(CourtSide, uint, uint)>();

        public SwingResult Evaluate(RacketInput input, double now, TennisMatch match, BallSimulation ball,
            Vector3 playerPosition, bool authoritative, float reach = float.NaN, GameplayBalance settings = null)
        {
            var c = settings ?? GameplayBalance.Defaults;
            if (!authoritative) return SwingResult.NotAuthority;
            if (!input.IsValid || double.IsNaN(now) || double.IsInfinity(now)) return SwingResult.Invalid;
            var key = (input.Player, input.SessionId, input.EventId);
            if (!seen.Add(key)) return SwingResult.Duplicate;
            order.Enqueue(key);
            if (order.Count > 64) seen.Remove(order.Dequeue());
            // Consume misses too: a retransmission must not become a later successful swing.
            if (now < input.ReceivedAt || now - input.ReceivedAt > MaxAge) return SwingResult.Stale;
            if (match == null || input.RallyId != match.RallyId || input.IncomingShotId != match.CurrentShot.ShotId)
                return SwingResult.Stale;
            if (input.Stroke == StrokeKind.Volley && ball.BounceCount != 0) return SwingResult.WrongPhase;
            if (input.Stroke == StrokeKind.Smash && ball.Position.y < c["hit.smash_height"]) return SwingResult.Height;
            return CanHit(input.Player, match, ball, playerPosition, reach, c);
        }

        public static SwingResult CanHit(CourtSide player, TennisMatch match, BallSimulation ball, Vector3 playerPosition, float reach = float.NaN, GameplayBalance settings = null)
        {
            var c = settings ?? GameplayBalance.Defaults;
            if (float.IsNaN(reach)) reach=c["hit.reach"];
            if (ball == null || !ball.HasShot || ball.IsResting) return SwingResult.NoBall;
            if (match == null || !match.CanReturn(player)) return SwingResult.WrongPhase;
            Vector3 position = ball.Position;
            if (player == CourtSide.Near ? position.z >= -c["hit.net_gap"] : position.z <= c["hit.net_gap"]) return SwingResult.WrongSide;
            if (position.y < c["hit.min_height"] || position.y > c["hit.max_height"]) return SwingResult.Height;
            // Reach is measured across the court floor; height is checked separately.
            Vector2 offset = new Vector2(position.x - playerPosition.x, position.z - playerPosition.z);
            return offset.sqrMagnitude <= reach * reach ? SwingResult.Hit : SwingResult.OutOfReach;
        }
    }
}
