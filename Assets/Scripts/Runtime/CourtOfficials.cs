using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Visible officials. Rules and recovery decisions remain in the authoritative match.</summary>
    public sealed class CourtOfficials : MonoBehaviour
    {
        [SerializeField] private Transform umpireArm;
        [SerializeField] private Transform ballBoy;
        [SerializeField] private Transform runnerBody;
        private uint callId;
        private float gestureAge = 2f;
        private Vector3 previousRunner;
        public string CallText { get; private set; } = "Ready - first serve";
        public UmpireCall Call { get; private set; }
        public RecoveryPhase RecoveryPhase { get; private set; }
        public Vector3 RunnerPosition => ballBoy.position;
        public uint CallId => callId;
        public int Recoveries { get; private set; }

        public void Present(uint id, UmpireCall call, string text, RecoveryPhase recovery, Vector3 runner, int completed)
        {
            if (callId != id) { callId = id; gestureAge = 0f; }
            Call = call;
            CallText = text;
            RecoveryPhase = recovery;
            Recoveries = completed;
            Vector3 direction = runner - previousRunner;
            if (direction.sqrMagnitude > .00001f) ballBoy.rotation = Quaternion.LookRotation(direction);
            previousRunner = runner;
            ballBoy.position = runner;
        }

        private void Update()
        {
            var controller = GetComponent<TennisMatchController>();
            if (controller != null && controller.IsPaused) return;
            gestureAge += Time.deltaTime;
            bool signal = Call != UmpireCall.Play && Call != UmpireCall.Ready;
            umpireArm.localRotation = Quaternion.Euler(0, 0, signal && gestureAge < 1.5f ? -105 : -12);
            bool moving = RecoveryPhase == RecoveryPhase.Approaching || RecoveryPhase == RecoveryPhase.Returning;
            runnerBody.localPosition = new Vector3(0, .62f + (moving ? Mathf.Abs(Mathf.Sin(Time.time * 14)) * .08f : 0), 0);
            runnerBody.localRotation = Quaternion.Euler(RecoveryPhase == RecoveryPhase.PickingUp ? 55 : moving ? 12 : 0, 0, 0);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 19, wordWrap = true };
            float width = Mathf.Min(660, Screen.width - 32);
            var box = new Rect((Screen.width - width) / 2, Screen.height - 98, width, 82);
            GUI.Box(box, "CHAIR UMPIRE  |  One-game singles");
            GUI.Label(new Rect(box.x + 8, box.y + 21, width - 16, 30), CallText, style);
            string recovery = RecoveryPhase == RecoveryPhase.Idle ? "Court ready" :
                RecoveryPhase == RecoveryPhase.Approaching ? "Ball boy: collecting the dead ball" :
                RecoveryPhase == RecoveryPhase.PickingUp ? "Ball boy: picking up" : "Ball boy: returning to the sideline";
            GUI.Label(new Rect(box.x + 8, box.y + 53, width - 16, 22), recovery,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
