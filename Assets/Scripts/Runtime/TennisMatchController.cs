using UnityEngine;
using DigitalTennis.AI;

namespace DigitalTennis
{
    public enum ServeVariant { Valid, Fault, Let, NetFault }

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
        private float power, aimX, aimZ;
        private bool continuousRally;
        private string swingStatus = "Waiting for racket input";
        private long presentedPoints;
        private uint aiSession = 100;
        private bool externallyPaused;

        public BalanceBundleStore Balance { get; private set; }
        public GameplayBalance Tuning => Balance.Current;
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
        public PlayerInputAdapter InputAdapter { get; private set; }
        public LocalCourtView View { get; private set; }
        public CourtOfficials Officials { get; private set; }
        public BallRecovery Recovery { get; private set; }
        public CharacterMatchSystem Characters { get; private set; }
        public TennisEffects Effects { get; private set; }
        public bool ReadyToServe => Match != null && Match.Phase == MatchPhase.WaitServe && !Recovery.Active && !IsPaused && !(Characters?.Cinematic ?? false);
        public bool IsPaused { get => externallyPaused; set { externallyPaused = value; ball.IsPaused = value || (Characters?.Cinematic ?? false); } }
        public bool AutoServe { get => autoServe; set => autoServe = value; }
        public bool DemoReturns { get => demoReturns; set => demoReturns = value; }

        private void Start()
        {
            View = new LocalCourtView(Camera.main);
            InputAdapter = GetComponent<PlayerInputAdapter>();
            if (InputAdapter == null) InputAdapter = gameObject.AddComponent<PlayerInputAdapter>();
            nearAvatar = nearPlayer.GetComponent<RacketAvatar>();
            farAvatar = farPlayer.GetComponent<RacketAvatar>();
            Officials = GetComponent<CourtOfficials>();
            Characters = GetComponent<CharacterMatchSystem>();
            Balance=BalanceBundleStore.Open();
            power=Tuning["shot.default_power"];InputAdapter.Power01=power;
            Characters.Initialize(this);
            Effects=GetComponent<TennisEffects>();Effects?.Initialize(this);
            markerMaterial = landingMarker.GetComponent<Renderer>().material;
            ball.EventRaised += HandleBallEvent;
            if (mode != GameMode.Practice) { autoServe = false; demoReturns = false; }
            ResetMatch();
            if (FrontEndController.Active != null) { InputAdapter.GameplayEnabled = false; IsPaused = true; }
        }

        private void OnDestroy()
        {
            View?.Dispose();
            if (ball != null) ball.EventRaised -= HandleBallEvent;
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        public void Configure(GameMode selectedMode, Difficulty difficulty = Difficulty.Medium, uint seed = 42)
        {
            if (!ball.IsAuthoritative) return;
            if (!System.Enum.IsDefined(typeof(GameMode), selectedMode)) throw new System.ArgumentOutOfRangeException(nameof(selectedMode));
            Balance.RestoreLocal();
            Tuning.Difficulty(difficulty);
            View.SetPlayer(CourtSide.Near);
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
            Effects?.ResetMatch();
            Characters?.ResetGame();
            Recovery = new BallRecovery(Tuning);
            returns = 0;
            presentedPoints = 0;
            ForehandHits = BackhandHits = NearHits = FarHits = 0;
            LastHit = null;
            swingStatus = "Waiting for racket input";
            observedPhase = MatchPhase.MatchEnd;
            ApplyBalance();
            Synchronize();
        }

        internal void ApplyBalance()
        {
            ball.Stop();ball.Simulation.Configure(Tuning.Physics());
            ball.transform.localScale=Vector3.one*(2*Tuning["physics.radius"]);
            Recovery.Configure(Tuning);
            Opponent=mode==GameMode.SinglePlayer?new TennisAiOpponent(aiDifficulty,aiSeed,++aiSession,Tuning):null;
            power=Tuning["shot.default_power"];InputAdapter.Power01=power;
            predictedShot=0;
            Synchronize();
        }

        public void PresentBalance(GameplayBalance settings,uint revision)
        {
            if(ball.IsAuthoritative)return;
            bool changed=Tuning.Hash!=settings.Hash;Balance.Present(settings,revision);
            if(changed){ball.transform.localScale=Vector3.one*(2*settings["physics.radius"]);power=settings["shot.default_power"];InputAdapter.Power01=power;}
        }

        public Vector3 ServePosition => new Vector3((Match.ServeFromPositiveX ? 1 : -1)*Tuning["serve.offset"],
            Tuning["serve.height"], (Match.Server == CourtSide.Near ? -1 : 1)*Tuning["serve.depth"]);

        public bool Serve(ServeVariant variant = ServeVariant.Valid)
        {
            if (!ball.IsAuthoritative || !ReadyToServe || !Match.TryServe(Match.Server)) return false;
            Vector3 origin = ServePosition;
            float direction = Match.Server == CourtSide.Near ? 1f : -1f;
            Vector3 velocity;
            if (variant == ServeVariant.NetFault)
                velocity = VelocityTo(origin, new Vector3(0f, .45f, 0f), 1f);
            else if (variant == ServeVariant.Let)
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
                velocity = VelocityTo(origin, new Vector3(targetX, ball.Simulation.Radius, direction * Tuning["serve.target_depth"]), Tuning["serve.flight"] / Characters.Player(Match.Server).ServeSpeed);
            }
            returns = 0;
            Avatar(Match.Server).Swing(StrokeKind.Forehand);
            landingMarker.gameObject.SetActive(false);
            ball.Launch(origin, velocity, Match.CurrentShot);
            Effects?.Record(TennisEffectKind.Serve,Match.Server,origin,velocity,StrokeKind.Forehand);
            Synchronize();
            return true;
        }

        public bool CanReturn(CourtSide player) => Match != null && ball.IsAuthoritative &&
            !Characters.Cinematic && RacketInputGate.CanHit(player, Match, ball.Simulation, Avatar(player).transform.position, Characters.Player(player).Reach,Tuning) == SwingResult.Hit;

        private RacketAvatar Avatar(CourtSide player) => player == CourtSide.Near ? nearAvatar : farAvatar;

        public RacketInput CreateVirtualInput(CourtSide player, StrokeKind stroke, float? power01 = null,
            float horizontalAim = 0f, float verticalAim = 0f) => virtualInput.Create(player, stroke, power01 ?? Tuning["shot.default_power"],
                horizontalAim, verticalAim, Time.realtimeSinceStartupAsDouble, Match);

        public SwingResult VirtualSwing(CourtSide player, StrokeKind stroke)
        {
            if (mode != GameMode.Practice && player == CourtSide.Far) return SwingResult.WrongPhase;
            return SubmitInput(CreateVirtualInput(player, stroke, power, aimX, aimZ));
        }

        public void ApplyPlayerInput(PlayerInputSample sample, uint session, CourtSide localPlayer,
            uint? rallyId = null, uint? incomingShotId = null)
        {
            if (sample.Action == PlayerAction.Characters) { Characters.ToggleMenu(); return; }
            if (Match == null || !sample.IsValid || IsPaused || !ball.IsAuthoritative) return;
            if (sample.PracticeOpponent && mode != GameMode.Practice) return;
            CourtSide player = sample.PracticeOpponent ? CourtSide.Far : localPlayer;
            if (sample.Action == PlayerAction.Skill) { Characters.Activate(player); return; }
            if (sample.Action == PlayerAction.SelectCharacter) { Characters.Select(player, sample.Choice); return; }
            if (sample.Action == PlayerAction.SelectCourt) { if (player == CourtSide.Near) Characters.SelectCourt(sample.Choice); return; }
            if (sample.Action == PlayerAction.ReloadBalance) { Characters.Reload(); return; }
            if (sample.Action == PlayerAction.Forehand || sample.Action == PlayerAction.Backhand || sample.Action == PlayerAction.Smash ||
                sample.Action == PlayerAction.Volley || sample.Action == PlayerAction.Slice || sample.Action == PlayerAction.Lob)
            {
                var stroke = sample.Action == PlayerAction.Backhand ? StrokeKind.Backhand : sample.Action == PlayerAction.Smash ? StrokeKind.Smash :
                    sample.Action == PlayerAction.Volley ? StrokeKind.Volley : sample.Action == PlayerAction.Slice ? StrokeKind.Slice : sample.Action == PlayerAction.Lob ? StrokeKind.Lob : StrokeKind.Forehand;
                SubmitInput(new RacketInput(player,
                    stroke,
                    session, sample.EventId, sample.Power01, sample.AimX, sample.AimZ, sample.ReceivedAt,
                    rallyId ?? Match.RallyId, incomingShotId ?? Match.CurrentShot.ShotId));
            }
            else if (sample.Action == PlayerAction.Restart)
            {
                if (mode != GameMode.Online || player == CourtSide.Near) ResetMatch();
            }
            else if (player == Match.Server)
                Serve(sample.Action == PlayerAction.FaultServe ? ServeVariant.Fault :
                    sample.Action == PlayerAction.LetServe ? ServeVariant.Let :
                    sample.Action == PlayerAction.NetServe ? ServeVariant.NetFault : ServeVariant.Valid);
        }

        public void EnterOnline(bool host)
        {
            View.SetPlayer(host ? CourtSide.Near : CourtSide.Far);
            mode = GameMode.Online; Opponent = null; autoServe = demoReturns = false;
            ball.IsAuthoritative = host;
            IsPaused = true;
            if (host) ResetMatch();
        }

        public void PresentOnline(Vector3 ballPosition, Vector3 nearPosition, Vector3 farPosition)
        {
            if (ball.IsAuthoritative) return;
            ball.PresentRemote(ballPosition);
            nearPlayer.position = nearPosition; farPlayer.position = farPosition;
            landingMarker.gameObject.SetActive(false);
        }

        // Normalized BLE or network adapters can call this boundary without changing scoring or shot math.
        public SwingResult SubmitInput(RacketInput input)
        {
            if (Match == null || IsPaused || Characters.Cinematic) return SwingResult.WrongPhase;
            if (!input.IsValid) return SwingResult.Invalid;
            var ability = Characters.Player(input.Player);
            Vector3 playerPosition = input.Player == CourtSide.Near ? nearPlayer.position : farPlayer.position;
            SwingResult result = inputGate.Evaluate(input, Time.realtimeSinceStartupAsDouble, Match,
                ball.Simulation, playerPosition, ball.IsAuthoritative, ability.Reach,Tuning);
            if (result == SwingResult.Hit && Match.TryReturn(input.Player))
            {
                int stockBefore=ability.Meter.Stock;
                float incomingSpeed = ball.Simulation.Velocity.magnitude;
                var velocity = RacketShot.CharacterVelocity(input, ball.Simulation.Position, ball.Simulation.Gravity, ball.Simulation.Radius, ability.ShotMultiplier(input.Stroke),Tuning);
                ball.Launch(ball.Simulation.Position, velocity, Match.CurrentShot);
                ability.Returned(incomingSpeed);
                Effects?.Record(TennisEffectKind.Hit,input.Player,ball.Simulation.Position,velocity,input.Stroke);
                Effects?.Charge(input.Player,stockBefore);
                returns++;
                if (input.Stroke == StrokeKind.Backhand) BackhandHits++; else if (input.Stroke == StrokeKind.Forehand) ForehandHits++;
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
                nearTarget = AutomaticCourtMovement.Home(CourtSide.Near, nearPlayer.position.y,Tuning);
                farTarget = AutomaticCourtMovement.Home(CourtSide.Far, farPlayer.position.y,Tuning);
                if (Match.LastHitter == CourtSide.Near)
                    farTarget = AutomaticCourtMovement.PredictTarget(ball.Simulation, CourtSide.Far, farPlayer.position.y,Tuning);
                else nearTarget = AutomaticCourtMovement.PredictTarget(ball.Simulation, CourtSide.Near, nearPlayer.position.y,Tuning);
            }
            Vector3 beforeNear = nearPlayer.position, beforeFar = farPlayer.position;
            nearAvatar.MoveTo(nearTarget, CourtSide.Near, Time.deltaTime, Characters.Player(CourtSide.Near).MoveSpeed,Tuning);
            farAvatar.MoveTo(Opponent != null ? Opponent.MoveTarget : farTarget, CourtSide.Far, Time.deltaTime, Characters.Player(CourtSide.Far).MoveSpeed,Tuning);
            Characters.Player(CourtSide.Near).Moved(Vector3.Distance(beforeNear, nearPlayer.position));
            Characters.Player(CourtSide.Far).Moved(Vector3.Distance(beforeFar, farPlayer.position));
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
            float horizontal = Mathf.Atan2(-ball.Simulation.Position.x * 1.6f * facing, 17f) * Mathf.Rad2Deg / Tuning["shot.yaw"];
            SubmitInput(CreateVirtualInput(player, stroke, Tuning["shot.default_power"], Mathf.Clamp(horizontal, -1f, 1f), 0f));
        }

        private Vector3 VelocityTo(Vector3 origin, Vector3 target, float time)
        {
            return (target - origin) / time + Vector3.up * (0.5f * ball.Simulation.Gravity * time);
        }

        private void Update()
        {
            if (Match == null || !ball.IsAuthoritative || IsPaused) return;
            bool wasCinematic = Characters.Cinematic;
            Characters.Tick(Time.deltaTime);
            if (wasCinematic || Characters.Cinematic) return;
            phaseAge += Time.deltaTime;
            if (Recovery.Active)
            {
                bool complete = Recovery.Tick(Time.deltaTime);
                ball.ResetAt(Recovery.BallPosition);
                PresentOfficials();
                if (complete)
                {
                    phaseAge = 0f;
                    Synchronize();
                    Debug.Log($"TENNIS_RECOVERY_COMPLETE shot={Recovery.ShotId} count={Recovery.Completed}");
                }
                return;
            }
            if (Match.Phase == MatchPhase.PointEnd && phaseAge >= Tuning["match.next_point_delay"])
            {
                Match.AdvanceToNextPoint();
                Synchronize();
            }
            else if (Match.Phase == MatchPhase.WaitServe && autoServe && phaseAge >= Tuning["match.auto_serve_delay"]) Serve();
            Opponent?.Tick(this, Time.deltaTime);
            MovePlayers();
            DemoSwing();

            if ((Match.Phase == MatchPhase.InFlight || Match.Phase == MatchPhase.Rally) && ball.Simulation.IsResting)
                if (Match.ResolveStoppedBall(ball.Simulation.Shot.ShotId)) Synchronize();


        }

        private void HandleBallEvent(BallEvent item)
        {
            if (item.Kind == BallEventKind.Bounce)
            {
                landingMarker.position = item.Position + Vector3.up * 0.025f;
                landingMarker.gameObject.SetActive(true);
                markerMaterial.color = item.IsInBounds ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.25f, 0.2f);
            }
            var phase = Match.Phase;
            if (Match.HandleBallEvent(item))
            {
                if(item.Kind==BallEventKind.Bounce)Effects?.Record(TennisEffectKind.Bounce,Match.LastHitter,item.Position,Vector3.up);
                if (phase == MatchPhase.InFlight && Match.Phase == MatchPhase.Rally)
                {int before=Characters.Player(Match.Server).Meter.Stock;Characters.Player(Match.Server).ValidServe();Effects?.Charge(Match.Server,before);}
                Synchronize();
            }
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
            bool dead = Match.Phase == MatchPhase.WaitServe || Match.Phase == MatchPhase.PointEnd || Match.Phase == MatchPhase.MatchEnd;
            if (dead && Match.NetTouched && ball.Simulation.HasShot &&
                Recovery.Begin(Match.CurrentShot.ShotId, ball.Simulation.Position))
            {
                ball.Stop();
                Debug.Log($"TENNIS_RECOVERY_BEGIN shot={Recovery.ShotId} call={Match.Call}");
            }
            PresentOfficials();
            if (Recovery.Active) return;
            if (Match.Phase == MatchPhase.WaitServe)
            {
                ball.ResetAt(ServePosition);
                landingMarker.gameObject.SetActive(false);
                nearAvatar.ResetPose(new Vector3(ServePosition.x, 0.85f, -Tuning["serve.depth"]));
                farAvatar.ResetPose(new Vector3(-ServePosition.x, 0.85f, Tuning["serve.receiver_depth"]));
                predictedShot = 0;
            }
            else if (Match.Phase == MatchPhase.PointEnd || Match.Phase == MatchPhase.MatchEnd) ball.Stop();
        }

        private void PresentOfficials() => Officials.Present(Match.CallId, Match.Call, Match.CallText,
            Recovery.Phase, Recovery.Position, Recovery.Completed);

        private void OnGUI()
        {
            if (FrontEndController.HideLegacyHud) return;
            if (Match == null || mode == GameMode.Online) return;
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
            GUI.enabled = previous && ReadyToServe && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 164, 116, 28), "Serve [Space]")) InputAdapter.Press(PlayerAction.Serve);
            if (GUI.Button(new Rect(156, 164, 116, 28), "Fault serve [F]")) InputAdapter.Press(PlayerAction.FaultServe);
            if (GUI.Button(new Rect(280, 164, 116, 28), "Let serve [L]")) InputAdapter.Press(PlayerAction.LetServe);
            GUI.enabled = previous && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 200, 178, 28), "Near forehand [Q]")) InputAdapter.Press(PlayerAction.Forehand);
            if (GUI.Button(new Rect(218, 200, 178, 28), "Near backhand [W]")) InputAdapter.Press(PlayerAction.Backhand);
            GUI.enabled = previous && ball.IsAuthoritative && mode == GameMode.Practice;
            if (GUI.Button(new Rect(32, 236, 178, 28), "Far forehand [E]")) InputAdapter.Press(PlayerAction.Forehand, true);
            if (GUI.Button(new Rect(218, 236, 178, 28), "Far backhand [D]")) InputAdapter.Press(PlayerAction.Backhand, true);
            GUI.enabled = previous && ball.IsAuthoritative;
            GUI.Label(new Rect(32, 270, 375, 24), $"Near: {(CanReturn(CourtSide.Near) ? "HIT READY" : "tracking")}   Far: {(CanReturn(CourtSide.Far) ? "HIT READY" : "tracking")}");
            GUI.Label(new Rect(32, 296, 375, 24), swingStatus);
            GUI.Label(new Rect(32, 322, 375, 22), $"Power {power:F2} ({Tuning["shot.base_speed"] + Tuning["shot.power_speed"] * power:F1} m/s)");
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
            if (GUI.Button(new Rect(32, 462, 364, 28), "Restart one-game match [R]")) InputAdapter.Press(PlayerAction.Restart);
            GUI.enabled = previous;
            GUI.enabled = previous && ReadyToServe && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 502, 364, 28), "Net fault / ball boy [N]")) InputAdapter.Press(PlayerAction.NetServe);
            GUI.enabled = previous;
            InputAdapter.Power01 = power; InputAdapter.AimX = aimX; InputAdapter.AimZ = aimZ;
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
