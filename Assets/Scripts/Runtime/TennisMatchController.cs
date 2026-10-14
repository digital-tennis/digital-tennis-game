using UnityEngine;
using DigitalTennis.AI;

namespace DigitalTennis
{
    public enum ServeVariant { Valid, Fault, Let }

    /// <summary>Unity adapter and virtual inputs for the one-game rules prototype.</summary>
    public sealed class TennisMatchController : MonoBehaviour
    {
        [SerializeField] private BallFlight ball;
        [SerializeField] private Transform landingMarker;
        [SerializeField] private Transform nearPlayer;
        [SerializeField] private Transform farPlayer;
        [SerializeField] private GameMode mode = GameMode.Practice;
        [SerializeField] private Difficulty aiDifficulty = Difficulty.Medium;
        [SerializeField] private uint aiSeed = 42;
        [SerializeField] private bool autoServe = true;
        [SerializeField] private bool demoReturns = true;
        private Material markerMaterial;
        private MatchPhase observedPhase;
        private float phaseAge;
        private int returns;
        private readonly VirtualRacketInput virtualInput = new VirtualRacketInput();
        private readonly RacketInputGate inputGate = new RacketInputGate();
        private RacketAvatar nearAvatar, farAvatar;
        private Vector3 nearTarget, farTarget;
        private uint predictedShot;
        private float power = 0.35f, aimX, aimZ;
        private bool continuousRally;
        private string swingStatus = "Waiting for racket input";
        private long presentedPoints;
        private uint aiSession = 100;

        public TennisMatch Match { get; private set; }
        public GameMode Mode => mode;
        public Difficulty AiDifficulty => aiDifficulty;
        public TennisAiOpponent Opponent { get; private set; }
        public int DemoReturnLimit { get; set; } = 4;
        public int Hits => returns;
        public int ForehandHits { get; private set; }
        public int BackhandHits { get; private set; }
        public int NearHits { get; private set; }
        public int FarHits { get; private set; }
        public RacketInput? LastHit { get; private set; }
        public RacketAvatar NearAvatar => nearAvatar;
        public RacketAvatar FarAvatar => farAvatar;
        public BallFlight Ball => ball;
        public bool AutoServe { get => autoServe; set => autoServe = value; }
        public bool DemoReturns { get => demoReturns; set => demoReturns = value; }

        private void Start()
        {
            nearAvatar = nearPlayer.GetComponent<RacketAvatar>();
            farAvatar = farPlayer.GetComponent<RacketAvatar>();
            markerMaterial = landingMarker.GetComponent<Renderer>().material;
            ball.EventRaised += HandleBallEvent;
            if (mode != GameMode.Practice) { autoServe = false; demoReturns = false; }
            ResetMatch();
        }

        private void OnDestroy()
        {
            if (ball != null) ball.EventRaised -= HandleBallEvent;
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        public void Configure(GameMode selectedMode, Difficulty difficulty = Difficulty.Medium, uint seed = 42)
        {
            if (!ball.IsAuthoritative) return;
            if (!System.Enum.IsDefined(typeof(GameMode), selectedMode)) throw new System.ArgumentOutOfRangeException(nameof(selectedMode));
            DifficultySettings.For(difficulty);
            mode = selectedMode; aiDifficulty = difficulty; aiSeed = seed;
            autoServe = demoReturns = mode == GameMode.Practice;
            continuousRally = false;
            ResetMatch();
            Debug.Log($"TENNIS_MODE {mode} difficulty={aiDifficulty} seed={aiSeed}");
        }

        public void ResetMatch()
        {
            if (!ball.IsAuthoritative) return;
            Match = new TennisMatch();
            Opponent = mode == GameMode.SinglePlayer ? new TennisAiOpponent(aiDifficulty, aiSeed, ++aiSession) : null;
            returns = 0;
            presentedPoints = 0;
            ForehandHits = BackhandHits = NearHits = FarHits = 0;
            LastHit = null;
            swingStatus = "Waiting for racket input";
            observedPhase = MatchPhase.MatchEnd;
            Synchronize();
        }

        public Vector3 ServePosition => new Vector3(Match.ServeFromPositiveX ? 1.5f : -1.5f,
            2.2f, Match.Server == CourtSide.Near ? -12.6f : 12.6f);

        public bool Serve(ServeVariant variant = ServeVariant.Valid)
        {
            if (!ball.IsAuthoritative || !Match.TryServe(Match.Server)) return false;
            Vector3 origin = ServePosition;
            float direction = Match.Server == CourtSide.Near ? 1f : -1f;
            Vector3 velocity;
            if (variant == ServeVariant.Let)
            {
                // A real glancing top contact: clear the front face, clip the tape,
                // keep moving forward and land inside the correct service box.
                float timeToNet = 1f;
                velocity = new Vector3(-2f * origin.x / timeToNet,
                    (ball.Simulation.NetHeight + ball.Simulation.Radius - origin.y +
                     0.5f * ball.Simulation.Gravity * timeToNet * timeToNet) / timeToNet,
                    -origin.z / timeToNet);
            }
            else
            {
                float targetX = variant == ServeVariant.Fault ? origin.x : -origin.x;
                velocity = VelocityTo(origin, new Vector3(targetX, ball.Simulation.Radius, direction * 4f), 1.2f);
            }
            returns = 0;
            landingMarker.gameObject.SetActive(false);
            ball.Launch(origin, velocity, Match.CurrentShot);
            Synchronize();
            return true;
        }

        public bool CanReturn(CourtSide player) => Match != null && ball.IsAuthoritative &&
            RacketInputGate.CanHit(player, Match, ball.Simulation, Avatar(player).transform.position) == SwingResult.Hit;

        private RacketAvatar Avatar(CourtSide player) => player == CourtSide.Near ? nearAvatar : farAvatar;

        public RacketInput CreateVirtualInput(CourtSide player, StrokeKind stroke, float power01 = 0.35f,
            float horizontalAim = 0f, float verticalAim = 0f) => virtualInput.Create(player, stroke, power01,
                horizontalAim, verticalAim, Time.realtimeSinceStartupAsDouble, Match);

        public SwingResult VirtualSwing(CourtSide player, StrokeKind stroke)
        {
            if (mode != GameMode.Practice && player == CourtSide.Far) return SwingResult.WrongPhase;
            return SubmitInput(CreateVirtualInput(player, stroke, power, aimX, aimZ));
        }

        // Normalized BLE or network adapters can call this boundary without changing scoring or shot math.
        public SwingResult SubmitInput(RacketInput input)
        {
            if (Match == null) return SwingResult.WrongPhase;
            Vector3 playerPosition = input.Player == CourtSide.Near ? nearPlayer.position : farPlayer.position;
            SwingResult result = inputGate.Evaluate(input, Time.realtimeSinceStartupAsDouble, Match,
                ball.Simulation, playerPosition, ball.IsAuthoritative);
            if (result == SwingResult.Hit && Match.TryReturn(input.Player))
            {
                ball.Launch(ball.Simulation.Position, RacketShot.Velocity(input), Match.CurrentShot);
                returns++;
                if (input.Stroke == StrokeKind.Forehand) ForehandHits++; else BackhandHits++;
                if (input.Player == CourtSide.Near) NearHits++; else FarHits++;
                LastHit = input;
                Avatar(input.Player).Swing(input.Stroke);
                Synchronize();
            }
            swingStatus = $"{input.Player} {input.Stroke}: {result}";
            Debug.Log($"TENNIS_SWING {swingStatus} event={input.EventId} incoming={input.IncomingShotId} hits={returns}");
            return result;
        }

        private void MovePlayers()
        {
            if (Match.Phase != MatchPhase.InFlight && Match.Phase != MatchPhase.Rally) return;
            if (predictedShot != Match.CurrentShot.ShotId)
            {
                predictedShot = Match.CurrentShot.ShotId;
                nearTarget = AutomaticCourtMovement.Home(CourtSide.Near, nearPlayer.position.y);
                farTarget = AutomaticCourtMovement.Home(CourtSide.Far, farPlayer.position.y);
                if (Match.LastHitter == CourtSide.Near)
                    farTarget = AutomaticCourtMovement.PredictTarget(ball.Simulation, CourtSide.Far, farPlayer.position.y);
                else nearTarget = AutomaticCourtMovement.PredictTarget(ball.Simulation, CourtSide.Near, nearPlayer.position.y);
            }
            nearAvatar.MoveTo(nearTarget, CourtSide.Near, Time.deltaTime);
            farAvatar.MoveTo(Opponent != null ? Opponent.MoveTarget : farTarget, CourtSide.Far, Time.deltaTime);
        }

        private void DemoSwing()
        {
            if (mode != GameMode.Practice || !demoReturns || Match.Phase != MatchPhase.Rally ||
                (!continuousRally && DemoReturnLimit > 0 && returns >= DemoReturnLimit) ||
                ball.Simulation.BounceCount != 1 || ball.Simulation.Position.y < 0.95f) return;
            CourtSide player = TennisMatch.Opponent(Match.LastHitter);
            if (!CanReturn(player)) return;
            // A repeatable virtual source, not AI: alternate both stroke types and aim gently across court.
            StrokeKind stroke = (returns % 4 == 0 || returns % 4 == 3) ? StrokeKind.Forehand : StrokeKind.Backhand;
            float facing = player == CourtSide.Near ? 1f : -1f;
            float horizontal = Mathf.Atan2(-ball.Simulation.Position.x * 1.6f * facing, 17f) * Mathf.Rad2Deg / 25f;
            SubmitInput(CreateVirtualInput(player, stroke, 0.35f, Mathf.Clamp(horizontal, -1f, 1f), 0f));
        }

        private Vector3 VelocityTo(Vector3 origin, Vector3 target, float time)
        {
            return (target - origin) / time + Vector3.up * (0.5f * ball.Simulation.Gravity * time);
        }

        private void Update()
        {
            if (Match == null || !ball.IsAuthoritative) return;
            phaseAge += Time.deltaTime;
            if (Match.Phase == MatchPhase.PointEnd && phaseAge >= 1.25f)
            {
                Match.AdvanceToNextPoint();
                Synchronize();
            }
            else if (Match.Phase == MatchPhase.WaitServe && autoServe && phaseAge >= 0.75f) Serve();
            Opponent?.Tick(this, Time.deltaTime);
            MovePlayers();
            DemoSwing();

            if ((Match.Phase == MatchPhase.InFlight || Match.Phase == MatchPhase.Rally) && ball.Simulation.IsResting)
                if (Match.ResolveStoppedBall(ball.Simulation.Shot.ShotId)) Synchronize();

            if (Input.GetKeyDown(KeyCode.Space)) Serve();
            if (Input.GetKeyDown(KeyCode.Q)) VirtualSwing(CourtSide.Near, StrokeKind.Forehand);
            if (Input.GetKeyDown(KeyCode.W)) VirtualSwing(CourtSide.Near, StrokeKind.Backhand);
            if (Input.GetKeyDown(KeyCode.E)) VirtualSwing(CourtSide.Far, StrokeKind.Forehand);
            if (Input.GetKeyDown(KeyCode.D)) VirtualSwing(CourtSide.Far, StrokeKind.Backhand);
            if (Input.GetKeyDown(KeyCode.F)) Serve(ServeVariant.Fault);
            if (Input.GetKeyDown(KeyCode.L)) Serve(ServeVariant.Let);
            if (Input.GetKeyDown(KeyCode.R)) ResetMatch();
        }

        private void HandleBallEvent(BallEvent item)
        {
            if (item.Kind == BallEventKind.Bounce)
            {
                landingMarker.position = item.Position + Vector3.up * 0.025f;
                landingMarker.gameObject.SetActive(true);
                markerMaterial.color = item.IsInBounds ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.25f, 0.2f);
            }
            if (Match.HandleBallEvent(item)) Synchronize();
        }

        private void Synchronize()
        {
            if (Match.Phase != observedPhase)
            {
                observedPhase = Match.Phase;
                phaseAge = 0f;
                Debug.Log($"TENNIS_PHASE {Match.Phase} rally={Match.RallyId} faults={Match.FaultCount} side={Match.ServeSide}");
            }
            if (Match.Score.TotalPoints != presentedPoints)
            {
                presentedPoints = Match.Score.TotalPoints;
                Debug.Log($"TENNIS_POINT rally={Match.RallyId} score={Match.Score.Display(CourtSide.Near)}-{Match.Score.Display(CourtSide.Far)} {Match.Status}");
            }
            if (Match.Phase == MatchPhase.WaitServe)
            {
                ball.ResetAt(ServePosition);
                landingMarker.gameObject.SetActive(false);
                nearAvatar.ResetPose(new Vector3(ServePosition.x, 0.85f, -12.6f));
                farAvatar.ResetPose(new Vector3(-ServePosition.x, 0.85f, 9f));
                predictedShot = 0;
            }
            else if (Match.Phase == MatchPhase.PointEnd || Match.Phase == MatchPhase.MatchEnd) ball.Stop();
        }

        private void OnGUI()
        {
            if (Match == null) return;
            GUI.Box(new Rect(16, 16, 405, 530), mode == GameMode.SinglePlayer ? "Digital Tennis - You vs AI" : "Digital Tennis - Virtual rackets");
            GUI.Label(new Rect(32, 45, 375, 25), $"NEAR   {Match.Score.Display(CourtSide.Near)}  :  {Match.Score.Display(CourtSide.Far)}   FAR");
            string phase = Match.Phase == MatchPhase.WaitServe ? "Ready to serve" :
                Match.Phase == MatchPhase.InFlight ? "Serve in flight" : Match.Phase == MatchPhase.Rally ? "Rally" :
                Match.Phase == MatchPhase.PointEnd ? "Point finished - preparing next serve" : "Game finished";
            GUI.Label(new Rect(32, 74, 375, 24), $"{phase} | Hits: {returns}");
            GUI.Label(new Rect(32, 102, 375, 24), $"Server: {Match.Server} | {Match.ServeSide} | Faults: {Match.FaultCount}");
            string result = Match.Score.Winner.HasValue ? Match.Score.Winner + " wins the game" :
                Match.Score.IsDeuce ? "DEUCE" : Match.Score.Advantage.HasValue ? "ADVANTAGE " + Match.Score.Advantage : Match.Status;
            GUI.Label(new Rect(32, 130, 375, 30), result);
            bool previous = GUI.enabled;
            GUI.enabled = previous && Match.Phase == MatchPhase.WaitServe && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 164, 116, 28), "Serve [Space]")) Serve();
            if (GUI.Button(new Rect(156, 164, 116, 28), "Fault serve [F]")) Serve(ServeVariant.Fault);
            if (GUI.Button(new Rect(280, 164, 116, 28), "Let serve [L]")) Serve(ServeVariant.Let);
            GUI.enabled = previous && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 200, 178, 28), "Near forehand [Q]")) VirtualSwing(CourtSide.Near, StrokeKind.Forehand);
            if (GUI.Button(new Rect(218, 200, 178, 28), "Near backhand [W]")) VirtualSwing(CourtSide.Near, StrokeKind.Backhand);
            GUI.enabled = previous && ball.IsAuthoritative && mode == GameMode.Practice;
            if (GUI.Button(new Rect(32, 236, 178, 28), "Far forehand [E]")) VirtualSwing(CourtSide.Far, StrokeKind.Forehand);
            if (GUI.Button(new Rect(218, 236, 178, 28), "Far backhand [D]")) VirtualSwing(CourtSide.Far, StrokeKind.Backhand);
            GUI.enabled = previous && ball.IsAuthoritative;
            GUI.Label(new Rect(32, 270, 375, 24), $"Near: {(CanReturn(CourtSide.Near) ? "HIT READY" : "tracking")}   Far: {(CanReturn(CourtSide.Far) ? "HIT READY" : "tracking")}");
            GUI.Label(new Rect(32, 296, 375, 24), swingStatus);
            GUI.Label(new Rect(32, 322, 375, 22), $"Power {power:F2} ({10f + 12f * power:F1} m/s)");
            power = GUI.HorizontalSlider(new Rect(230, 329, 162, 18), power, 0f, 1f);
            GUI.Label(new Rect(32, 348, 190, 22), $"Aim left/right {aimX:F2}");
            aimX = GUI.HorizontalSlider(new Rect(230, 355, 162, 18), aimX, -1f, 1f);
            GUI.Label(new Rect(32, 374, 190, 22), $"Aim low/high {aimZ:F2}");
            aimZ = GUI.HorizontalSlider(new Rect(230, 381, 162, 18), aimZ, -1f, 1f);
            GUI.enabled = previous && ball.IsAuthoritative && mode == GameMode.Practice;
            autoServe = GUI.Toggle(new Rect(32, 402, 180, 22), autoServe, "Demo: auto serve");
            demoReturns = GUI.Toggle(new Rect(218, 402, 180, 22), demoReturns, "Demo: racket swings");
            continuousRally = GUI.Toggle(new Rect(32, 430, 360, 22), continuousRally, "Continuous rally (otherwise 4 swings, then point)");
            GUI.enabled = previous && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 462, 364, 28), "Restart one-game match [R]")) ResetMatch();
            GUI.enabled = previous;
            GUI.Label(new Rect(32, 502, 375, 22), $"Auto move 5 m/s | Reach 1.5 m | FH {ForehandHits} / BH {BackhandHits}");
            DrawModeSelection();
        }

        private void DrawModeSelection()
        {
            float x = Screen.width - 296f;
            GUI.Box(new Rect(x, 16, 280, 245), "Play mode");
            GUI.Label(new Rect(x + 16, 46, 250, 24), mode == GameMode.SinglePlayer ? "YOU (Near) vs AI (Far)" : "Local practice - virtual rackets");
            bool previous = GUI.enabled;
            GUI.enabled = previous && ball.IsAuthoritative;
            if (GUI.Button(new Rect(x + 16, 76, 118, 28), "Single player")) Configure(GameMode.SinglePlayer, aiDifficulty, aiSeed);
            if (GUI.Button(new Rect(x + 144, 76, 118, 28), "Practice")) Configure(GameMode.Practice, aiDifficulty, aiSeed);
            GUI.Label(new Rect(x + 16, 114, 250, 22), $"AI difficulty: {aiDifficulty}");
            if (GUI.Button(new Rect(x + 16, 142, 76, 28), "Easy")) Configure(GameMode.SinglePlayer, Difficulty.Easy, aiSeed);
            if (GUI.Button(new Rect(x + 101, 142, 76, 28), "Medium")) Configure(GameMode.SinglePlayer, Difficulty.Medium, aiSeed);
            if (GUI.Button(new Rect(x + 186, 142, 76, 28), "Hard")) Configure(GameMode.SinglePlayer, Difficulty.Hard, aiSeed);
            GUI.enabled = previous;
            GUI.Label(new Rect(x + 16, 182, 250, 22), "Space: serve | Q/W: your racket");
            GUI.Label(new Rect(x + 16, 211, 250, 22), Opponent == null ? "Select a difficulty to start a new game" :
                $"You hits: {NearHits} | AI hits: {Opponent.AcceptedHits}");
        }
    }
}
