using UnityEngine;

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
        [SerializeField] private bool autoServe = true;
        [SerializeField] private bool demoReturns = true;
        private Material markerMaterial;
        private MatchPhase observedPhase;
        private float phaseAge;
        private int returns;
        private long presentedPoints;

        public TennisMatch Match { get; private set; }
        public BallFlight Ball => ball;
        public bool AutoServe { get => autoServe; set => autoServe = value; }
        public bool DemoReturns { get => demoReturns; set => demoReturns = value; }

        private void Start()
        {
            markerMaterial = landingMarker.GetComponent<Renderer>().material;
            ball.EventRaised += HandleBallEvent;
            ResetMatch();
        }

        private void OnDestroy()
        {
            if (ball != null) ball.EventRaised -= HandleBallEvent;
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        public void ResetMatch()
        {
            if (!ball.IsAuthoritative) return;
            Match = new TennisMatch();
            returns = 0;
            presentedPoints = 0;
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

        public bool CanReturn(CourtSide player)
        {
            if (Match == null || !ball.IsAuthoritative || !Match.CanReturn(player)) return false;
            float z = ball.Simulation.Position.z;
            return (player == CourtSide.Near ? z <= 0f : z >= 0f) && !ball.Simulation.IsResting;
        }

        public bool Return(CourtSide player)
        {
            if (!CanReturn(player) || !Match.TryReturn(player)) return false;
            Vector3 origin = ball.Simulation.Position;
            Vector3 target = new Vector3(Mathf.Clamp(-origin.x * 0.6f, -3f, 3f), ball.Simulation.Radius,
                player == CourtSide.Near ? 8f : -8f);
            ball.Launch(origin, VelocityTo(origin, target, 1.2f), Match.CurrentShot);
            returns++;
            Synchronize();
            return true;
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
            else if (Match.Phase == MatchPhase.Rally && demoReturns && returns < 2 &&
                ball.Simulation.BounceCount == 1 && ball.Simulation.Position.y >= 0.35f && ball.Simulation.Velocity.y > 0f)
                Return(TennisMatch.Opponent(Match.LastHitter));

            if ((Match.Phase == MatchPhase.InFlight || Match.Phase == MatchPhase.Rally) && ball.Simulation.IsResting)
                if (Match.ResolveStoppedBall(ball.Simulation.Shot.ShotId)) Synchronize();

            if (Input.GetKeyDown(KeyCode.Space)) Serve();
            if (Input.GetKeyDown(KeyCode.Q)) Return(CourtSide.Near);
            if (Input.GetKeyDown(KeyCode.E)) Return(CourtSide.Far);
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
                nearPlayer.position = new Vector3(ServePosition.x, 0.85f, -12.6f);
                farPlayer.position = new Vector3(-ServePosition.x, 0.85f, 9f);
            }
            else if (Match.Phase == MatchPhase.PointEnd || Match.Phase == MatchPhase.MatchEnd) ball.Stop();
        }

        private void OnGUI()
        {
            if (Match == null) return;
            GUI.Box(new Rect(16, 16, 360, 370), "Digital Tennis - Serve, rally & score");
            GUI.Label(new Rect(32, 45, 325, 25), $"NEAR   {Match.Score.Display(CourtSide.Near)}  :  {Match.Score.Display(CourtSide.Far)}   FAR");
            string phase = Match.Phase == MatchPhase.WaitServe ? "Ready to serve" :
                Match.Phase == MatchPhase.InFlight ? "Serve in flight" : Match.Phase == MatchPhase.Rally ? "Rally" :
                Match.Phase == MatchPhase.PointEnd ? "Point finished - preparing next serve" : "Game finished";
            GUI.Label(new Rect(32, 74, 325, 24), phase);
            GUI.Label(new Rect(32, 102, 325, 24), $"Server: {Match.Server} | {Match.ServeSide} | Faults: {Match.FaultCount}");
            string result = Match.Score.Winner.HasValue ? Match.Score.Winner + " wins the game" :
                Match.Score.IsDeuce ? "DEUCE" : Match.Score.Advantage.HasValue ? "ADVANTAGE " + Match.Score.Advantage : Match.Status;
            GUI.Label(new Rect(32, 130, 325, 38), result);
            bool previous = GUI.enabled;
            GUI.enabled = previous && Match.Phase == MatchPhase.WaitServe && ball.IsAuthoritative;
            if (GUI.Button(new Rect(32, 174, 100, 28), "Serve [Space]")) Serve();
            if (GUI.Button(new Rect(140, 174, 100, 28), "Fault serve [F]")) Serve(ServeVariant.Fault);
            if (GUI.Button(new Rect(248, 174, 100, 28), "Let serve [L]")) Serve(ServeVariant.Let);
            GUI.enabled = previous && CanReturn(CourtSide.Near);
            if (GUI.Button(new Rect(32, 211, 154, 28), "Return Near [Q]")) Return(CourtSide.Near);
            GUI.enabled = previous && CanReturn(CourtSide.Far);
            if (GUI.Button(new Rect(194, 211, 154, 28), "Return Far [E]")) Return(CourtSide.Far);
            GUI.enabled = previous && ball.IsAuthoritative;
            autoServe = GUI.Toggle(new Rect(32, 251, 310, 22), autoServe, "Demo: auto serve");
            demoReturns = GUI.Toggle(new Rect(32, 279, 310, 22), demoReturns, "Demo: return twice, then allow a point");
            if (GUI.Button(new Rect(32, 314, 316, 28), "Restart one-game match [R]")) ResetMatch();
            GUI.enabled = previous;
            GUI.Label(new Rect(32, 351, 320, 22), "One game | First server stays | Virtual inputs");
        }
    }
}
