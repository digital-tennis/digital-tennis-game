using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Development-only shot presets. Rally scoring and controller input come later.</summary>
    public sealed class BallBounceDemo : MonoBehaviour
    {
        [SerializeField] private BallFlight ball;
        [SerializeField] private Transform landingMarker;
        [SerializeField] private bool autoReplay = true;
        private static readonly string[] Labels = { "In court", "On line", "Out", "Net", "Serve in", "Serve out" };
        private int preset;
        private uint rallyId;
        private float age;
        private string firstLanding = "Waiting for landing";
        private int netContacts;
        private Vector3 lastLanding;
        private Material markerMaterial;
        public bool AutoReplay { get => autoReplay; set => autoReplay = value; }

        private void Start()
        {
            markerMaterial = landingMarker.GetComponent<Renderer>().material;
            ball.EventRaised += HandleEvent;
            LaunchPreset(0);
        }

        private void OnDestroy()
        {
            if (ball != null) ball.EventRaised -= HandleEvent;
            if (markerMaterial != null) Destroy(markerMaterial);
        }

        public void LaunchPreset(int selection)
        {
            preset = Mathf.Clamp(selection, 0, Labels.Length - 1);
            float x = preset == 1 ? CourtBounds.SinglesHalfWidth : preset == 2 ? 4.6f :
                preset == 4 ? 1.5f : preset == 5 ? -1.5f : 0f;
            bool serve = preset >= 4;
            Vector3 start = new Vector3(x, preset == 3 ? 1f : 1.8f, preset == 3 ? -4f : -8f);
            Vector3 speed = preset == 3 ? new Vector3(0f, 0f, 18f) : new Vector3(0f, serve ? 4.5f : 6f, 10f);
            ball.Launch(start, speed, new ShotContext(++rallyId, CourtSide.Near,
                serve ? LandingArea.ServiceBox : LandingArea.SinglesCourt, ServiceBoxSide.PositiveX));
            age = 0f;
            firstLanding = "Waiting for landing";
            netContacts = 0;
            lastLanding = Vector3.zero;
            landingMarker.gameObject.SetActive(false);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (autoReplay && age >= 6f) LaunchPreset(preset);
            if (Input.GetKeyDown(KeyCode.R)) LaunchPreset(preset);
            for (int i = 0; i < Labels.Length; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) LaunchPreset(i);
        }

        private void HandleEvent(BallEvent item)
        {
            if (item.Kind == BallEventKind.Net) netContacts++;
            if (item.Kind == BallEventKind.Bounce)
            {
                lastLanding = item.Position;
                landingMarker.position = item.Position + Vector3.up * 0.025f;
                landingMarker.gameObject.SetActive(true);
                markerMaterial.color = item.IsInBounds ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.25f, 0.2f);
                if (item.IsFirstBounce) firstLanding = item.IsInBounds ? "First landing: IN" : "First landing: OUT";
            }
            Debug.Log($"BALL_EVENT rally={item.RallyId} event={item.EventId} kind={item.Kind} " +
                $"bounce={item.BounceCount} in={item.IsInBounds} x={item.Position.x:F4} z={item.Position.z:F4}");
        }

        private void OnGUI()
        {
            if (ball == null || ball.Simulation == null) return;
            GUI.Box(new Rect(16, 16, 310, 290), "Digital Tennis - Ball & bounce lab");
            GUI.Label(new Rect(30, 45, 280, 24), "Select a shot (keys 1-6). R: replay");
            for (int i = 0; i < Labels.Length; i++)
                if (GUI.Button(new Rect(30 + i % 3 * 94, 76 + i / 3 * 34, 88, 28), Labels[i])) LaunchPreset(i);
            autoReplay = GUI.Toggle(new Rect(30, 147, 260, 22), autoReplay, "Replay every 6 seconds");
            GUI.Label(new Rect(30, 176, 280, 24), "Shot: " + Labels[preset] + "   " + firstLanding);
            GUI.Label(new Rect(30, 205, 280, 24), $"Bounces: {ball.Simulation.BounceCount}   Net hits: {netContacts}");
            GUI.Label(new Rect(30, 233, 280, 24), $"Last bounce x={lastLanding.x:F3}, z={lastLanding.z:F3}");
            GUI.Label(new Rect(30, 257, 280, 22), "Green: in   Red: out   Scoring comes next");
        }
    }
}
