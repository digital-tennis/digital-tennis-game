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
        private bool endsChanged;
        private float serveAge;
        private readonly FullMatchPolicyAdapter learned;
        public string BackendName { get; }
        public int LearnedDecisions => learned?.Decisions ?? 0;
        public int LearnedAction => learned?.LastAction ?? -1;
        private readonly int[] learnedActions=new int[6];
        public int[] LearnedActionCounts=>(int[])learnedActions.Clone();
        public int Serves { get; private set; }
        public AiDecision LastDecision { get; private set; }
        public int Attempts { get; private set; }
        public int AcceptedHits { get; private set; }
        public int RejectedHits { get; private set; }
        public AiPolicy Policy => policy;

        public TennisAiOpponent(Difficulty difficulty, uint seed, uint sessionId, GameplayBalance tuning = null)
        {
            tuning = tuning ?? GameplayBalance.Defaults;
            learned = FullMatchPolicyAdapter.Load(out string backend); BackendName = backend;
            policy = new AiPolicy(tuning.Difficulty(difficulty), tuning.AiTuning(), seed, learned); this.sessionId = sessionId;
            Debug.Log($"TENNIS_AI_MODEL backend={BackendName} loaded={learned != null}");
        }

        private static Vec3 ToAi(Vector3 value) => new Vec3(value.x, value.y, value.z);
        private Vector3 Turn(Vector3 p) => endsChanged?new Vector3(-p.x,p.y,-p.z):p;
        public Vector3 MoveTarget => Turn(new Vector3(LastDecision.MoveTarget.X, LastDecision.MoveTarget.Y, LastDecision.MoveTarget.Z));

        public void Tick(TennisMatchController controller, float deltaTime, bool online = false)
        {
            if (online || controller.IsPaused || controller.Characters.Cinematic || !controller.Ball.IsAuthoritative || controller.Mode != GameMode.SinglePlayer) return;
            var match = controller.Match;
            var ball = controller.Ball.Simulation;
            endsChanged=match.Progress.EndsChanged;
            if(!online&&controller.Ball.IsAuthoritative&&controller.ReadyToServe&&match.Server==CourtSide.Far)
            {
                serveAge+=deltaTime;
                if(serveAge>=controller.Tuning["match.auto_serve_delay"])
                {
                    controller.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Serve,++eventId,Time.realtimeSinceStartupAsDouble),sessionId,CourtSide.Far);
                    if(match.Phase==MatchPhase.InFlight)Serves++;
                    serveAge=0;
                }
            }
            else serveAge=0;
            var phase = match.Phase == MatchPhase.InFlight ? AiPhase.ServeInFlight :
                match.Phase == MatchPhase.Rally ? AiPhase.Rally : match.Phase == MatchPhase.PointEnd ? AiPhase.PointEnd :
                match.Phase == MatchPhase.MatchEnd ? AiPhase.MatchEnd : AiPhase.WaitServe;
            var state = new AiState(++tick, deltaTime, 1, ToAi(Turn(controller.FarAvatar.transform.position)),
                ToAi(Turn(controller.NearAvatar.transform.position)), ToAi(Turn(ball.Position)), ToAi(Turn(ball.Velocity)), phase,
                ball.BounceCount, ((ulong)match.RallyId << 32) | match.CurrentShot.ShotId, (int)match.LastHitter,
                ball.HasShot, ball.IsResting, online, ball.Gravity, ball.Radius, ball.BounceRestitution, ball.GroundSpeedRetention, controller.Characters.Player(CourtSide.Far).Reach, controller.Characters.Player(CourtSide.Far).ShotMultiplier(StrokeKind.Forehand));
            int beforeDecisions=LearnedDecisions;
            learned?.Observe(controller);
            LastDecision = policy.Decide(state);
            if(LearnedDecisions>beforeDecisions&&LearnedAction>=0&&LearnedAction<learnedActions.Length)learnedActions[LearnedAction]++;
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
            Debug.Log($"TENNIS_AI {policy.Difficulty} {LastDecision.Stroke} {result} attempts={Attempts} accepted={AcceptedHits} learnedDecisions={LearnedDecisions} learnedAction={LearnedAction}");
        }
    }
}
