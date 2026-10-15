using DigitalTennis.AI;
using UnityEngine;

namespace DigitalTennis
{
    public enum GameMode { Practice, SinglePlayer, Online }

    /// <summary>Maps scene snapshots to the independent AI package and decisions to ordinary racket input.</summary>
    public sealed class TennisAiOpponent
    {
        private readonly AiPolicy policy;
        private readonly uint sessionId;
        private long tick;
        private uint eventId;
        public AiDecision LastDecision { get; private set; }
        public int Attempts { get; private set; }
        public int AcceptedHits { get; private set; }
        public int RejectedHits { get; private set; }
        public AiPolicy Policy => policy;

        public TennisAiOpponent(Difficulty difficulty, uint seed, uint sessionId, GameplayBalance tuning = null)
        { tuning = tuning ?? GameplayBalance.Defaults; policy = new AiPolicy(tuning.Difficulty(difficulty), tuning.AiTuning(), seed); this.sessionId = sessionId; }

        private static Vec3 ToAi(Vector3 value) => new Vec3(value.x, value.y, value.z);
        public Vector3 MoveTarget => new Vector3(LastDecision.MoveTarget.X, LastDecision.MoveTarget.Y, LastDecision.MoveTarget.Z);

        public void Tick(TennisMatchController controller, float deltaTime, bool online = false)
        {
            var match = controller.Match;
            var ball = controller.Ball.Simulation;
            var phase = match.Phase == MatchPhase.InFlight ? AiPhase.ServeInFlight :
                match.Phase == MatchPhase.Rally ? AiPhase.Rally : match.Phase == MatchPhase.PointEnd ? AiPhase.PointEnd :
                match.Phase == MatchPhase.MatchEnd ? AiPhase.MatchEnd : AiPhase.WaitServe;
            var state = new AiState(++tick, deltaTime, 1, ToAi(controller.FarAvatar.transform.position),
                ToAi(controller.NearAvatar.transform.position), ToAi(ball.Position), ToAi(ball.Velocity), phase,
                ball.BounceCount, ((ulong)match.RallyId << 32) | match.CurrentShot.ShotId, (int)match.LastHitter,
                ball.HasShot, ball.IsResting, online, ball.Gravity, ball.Radius, ball.BounceRestitution, ball.GroundSpeedRetention, controller.Characters.Player(CourtSide.Far).Reach, controller.Characters.Player(CourtSide.Far).ShotMultiplier(StrokeKind.Forehand));
            LastDecision = policy.Decide(state);
            if (!LastDecision.ShouldSwing || !controller.Ball.IsAuthoritative || online) return;
            Attempts++;
            var input = new RacketInput(CourtSide.Far, (StrokeKind)LastDecision.Stroke, sessionId, ++eventId,
                LastDecision.Power01, LastDecision.AimX, LastDecision.AimZ, Time.realtimeSinceStartupAsDouble,
                match.RallyId, match.CurrentShot.ShotId);
            var result = controller.SubmitInput(input);
            if (result == SwingResult.Hit) AcceptedHits++; else RejectedHits++;
            var ability = controller.Characters.Player(CourtSide.Far);
            if (AiSkillPolicy.ShouldActivate(state, policy.Settings, ability.Meter.Stock, ability.Remaining, result == SwingResult.Hit))
                controller.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Skill, ++eventId, Time.realtimeSinceStartupAsDouble), sessionId, CourtSide.Far);
            Debug.Log($"TENNIS_AI {policy.Difficulty} {LastDecision.Stroke} {result} attempts={Attempts} accepted={AcceptedHits}");
        }
    }
}
