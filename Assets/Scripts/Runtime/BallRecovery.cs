using UnityEngine;

namespace DigitalTennis
{
    public enum RecoveryPhase { Idle, Approaching, PickingUp, Returning }

    /// <summary>Dead-ball choreography only. The host advances it; guests present snapshots.</summary>
    public sealed class BallRecovery
    {
        private GameplayBalance tuning;
        public static float RunSpeed => GameplayBalance.Defaults["recovery.speed"];
        public static float PickupSeconds => GameplayBalance.Defaults["recovery.pickup"];
        public BallRecovery(GameplayBalance tuning = null) { this.tuning=tuning ?? GameplayBalance.Defaults; }
        private static readonly Vector3 Home = new Vector3(6f, 0f, -.8f);
        private static readonly Vector3 FarGate = new Vector3(6f, 0f, .8f);
        private static readonly Vector3 CarryOffset = new Vector3(.35f, .65f, 0f);
        private Vector3 pickup;
        private bool approachViaPost, returnViaPost;
        private float pickupAge;
        public RecoveryPhase Phase { get; private set; }
        public bool Active => Phase != RecoveryPhase.Idle;
        public Vector3 Position { get; private set; } = new Vector3(6f, 0f, -.8f);
        public Vector3 BallPosition { get; private set; }
        public uint ShotId { get; private set; }
        public int Completed { get; private set; }

        public void Configure(GameplayBalance value) { if(Active) throw new System.InvalidOperationException("Recovery active."); tuning=value; }

        public bool Begin(uint shotId, Vector3 ballPosition)
        {
            if (Active || shotId == 0 || ShotId == shotId || !BallSimulation.IsFinite(ballPosition)) return false;
            ShotId = shotId;
            BallPosition = ballPosition;
            // For a far-side ball, go around the net post instead of teleporting or crossing the net.
            float z = ballPosition.z < 0f ? -1f : 1f;
            approachViaPost = returnViaPost = z > 0;
            pickup = new Vector3(ballPosition.x, 0f, z * Mathf.Max(.35f, Mathf.Abs(ballPosition.z)));
            pickupAge = 0;
            Phase = RecoveryPhase.Approaching;
            return true;
        }

        // Returns true once the runner and ball are safely outside the playing area.
        public bool Tick(float deltaTime)
        {
            if (!Active || !BallSimulation.IsFinite(deltaTime) || deltaTime <= 0) return false;
            if (Phase == RecoveryPhase.Approaching)
            {
                Vector3 target = approachViaPost ? FarGate : pickup;
                Position = Vector3.MoveTowards(Position, target, tuning["recovery.speed"] * deltaTime);
                if (Position == target)
                {
                    if (approachViaPost) approachViaPost = false;
                    else Phase = RecoveryPhase.PickingUp;
                }
            }
            else if (Phase == RecoveryPhase.PickingUp)
            {
                pickupAge += deltaTime;
                if (pickupAge >= tuning["recovery.pickup"]) Phase = RecoveryPhase.Returning;
            }
            else
            {
                Vector3 target = returnViaPost ? FarGate : Home;
                Position = Vector3.MoveTowards(Position, target, tuning["recovery.speed"] * deltaTime);
                BallPosition = Position + CarryOffset;
                if (Position == target && returnViaPost) returnViaPost = false;
                else if (Position == Home)
                {
                    Phase = RecoveryPhase.Idle;
                    Completed++;
                    return true;
                }
            }
            if (Phase == RecoveryPhase.Returning) BallPosition = Position + CarryOffset;
            return false;
        }
    }
}
