using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Opt-in standalone verification only. It feeds the real adapter; it cannot change physics or score.</summary>
    public sealed class OnlineValidationDriver : MonoBehaviour
    {
        private OnlineTennisSession session;
        private TennisMatchController controller;
        private string role, address, codeFile, reportFile, viewImage;
        private bool imageRequested, recoveryImageRequested, exerciseOfficials;
        private int recoveryFrames, recoveryStages, officialsCalls, officialsChecks, officialsErrors, completedRecoveries;
        private bool enabledByArguments, started, installedSource, finished;
        private double startedAt, endedAt;
        private int runtimeErrors;
        private float deadlineSeconds = 150;
        private readonly OnlineViewValidation viewProof = new OnlineViewValidation();
        private void LateUpdate()
        {
            if (!enabledByArguments || finished) return;
            viewProof.Sample(controller, session);
            var state = session.IsHost ? (session.Playing ? GamePayload.Capture(controller, "image", 0) : null) : session.PresentedState;
            if (state?.officials != null)
            {
                var o = state.officials;
                completedRecoveries = Mathf.Max(completedRecoveries, o.completed);
                if (o.recovery > 0) { recoveryFrames++; recoveryStages |= 1 << (o.recovery - 1); }
                if (o.call == (int)UmpireCall.Fault) officialsCalls |= 1;
                if (o.call == (int)UmpireCall.Let) officialsCalls |= 2;
                if (o.call == (int)UmpireCall.DoubleFault) officialsCalls |= 4;
                if (!session.IsHost)
                { officialsChecks++; if (!session.OfficialsPresentationAligned) officialsErrors++; }
                if (exerciseOfficials && !recoveryImageRequested && !string.IsNullOrEmpty(viewImage) && o.recovery == (int)RecoveryPhase.PickingUp)
                { recoveryImageRequested = true; StartCoroutine(CaptureView(viewImage.Replace(".png", "-recovery.png"), .15f)); }
            }
            if (!imageRequested && !string.IsNullOrEmpty(viewImage) && state != null && state.rallyId == 2 && state.phase == "Rally" && state.hits >= 2)
            { imageRequested = true; StartCoroutine(CaptureView(viewImage)); }
        }
        private IEnumerator CaptureView(string path, float delay = 0)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
        }
        private void Start()
        {
            string Arg(string name) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
            role = Arg("-dt-online-role"); enabledByArguments = role == "host" || role == "guest";
            exerciseOfficials = Array.IndexOf(Environment.GetCommandLineArgs(), "-dt-officials") >= 0;
            if (!enabledByArguments) { enabled = false; return; }
            address = Arg("-dt-server") ?? "ws://127.0.0.1:8080/ws";
            codeFile = Arg("-dt-code-file"); reportFile = Arg("-dt-report"); viewImage = Arg("-dt-view-image");
            if (float.TryParse(Arg("-dt-timeout"), out var timeout)) deadlineSeconds = timeout;
            session = GetComponent<OnlineTennisSession>(); controller = GetComponent<TennisMatchController>();
            startedAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            Application.logMessageReceived += CountError;
        }
        private void CountError(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++; }
        private void OnDestroy() { Application.logMessageReceived -= CountError; }
        private void Update()
        {
            if (!enabledByArguments || finished || controller.InputAdapter == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!installedSource)
            {
                controller.InputAdapter.UseSource(new ValidationSource(session, controller, exerciseOfficials)); installedSource = true;
            }
            if (!started)
            {
                if (role == "host") { session.CreateRoom(address); started = true; }
                else if (!string.IsNullOrEmpty(codeFile) && File.Exists(codeFile))
                {
                    string code = File.ReadAllText(codeFile).Trim();
                    if (code.Length == 6) { session.JoinRoom(address, code); started = true; }
                }
            }
            if (role == "host" && session.InRoom && !string.IsNullOrEmpty(codeFile) && !File.Exists(codeFile)) PublishCode();
            if (session.ResultWinner != null && endedAt == 0) endedAt = now;
            if (endedAt != 0 && now - endedAt >= (role == "host" ? 6 : 4)) Finish(false);
            else if (now - startedAt >= deadlineSeconds) Finish(true);
        }
        private void PublishCode()
        {
            // Publish atomically so the other process cannot consume an empty/partial room code.
            string temporary = codeFile + ".pending";
            File.WriteAllText(temporary, session.RoomCode); File.Move(temporary, codeFile);
        }
        private void Finish(bool timedOut)
        {
            finished = true;
            var state = session.IsHost ? GamePayload.Capture(controller, "validation", 0) : session.RemoteState;
            var report = new ValidationReport {
                role = role, mode = "relay", epoch = session.Epoch, roomCode = session.RoomCode,
                timedOut = timedOut, winner = session.ResultWinner, points = state?.points, phase = state?.phase,
                nearHits = state?.nearHits ?? 0, farHits = state?.farHits ?? 0,
                forehandHits = state?.forehandHits ?? 0, backhandHits = state?.backhandHits ?? 0,
                maxRallyHits = session.MaxRallyHits, nextServes = session.NextServes, completedPoints = session.CompletedPoints,
                sentInputs = session.SentInputs, processedInputs = session.ProcessedInputs,
                sentSnapshots = session.SentSnapshots, receivedSnapshots = session.ReceivedSnapshots,
                physicsTicks = controller.Ball.PhysicsTicks, protocolErrors = session.ProtocolErrors, runtimeErrors = runtimeErrors,
                inputSource = controller.InputAdapter.SourceName, inputDelivered = controller.InputAdapter.Delivered,
                aiActive = controller.Opponent != null, status = session.Status, view = viewProof,
                exerciseOfficials = exerciseOfficials, recoveryFrames = recoveryFrames, recoveryStages = recoveryStages,
                officialsCalls = officialsCalls, officialsChecks = officialsChecks, officialsErrors = officialsErrors, completedRecoveries = completedRecoveries
            };
            if (!string.IsNullOrEmpty(reportFile)) File.WriteAllText(reportFile, JsonUtility.ToJson(report, true));
            bool ok = !timedOut && report.winner != null && report.completedPoints >= 4 && report.nextServes >= 4 &&
                report.nearHits > 0 && report.farHits > 0 && report.forehandHits > 0 && report.backhandHits > 0 &&
                report.maxRallyHits >= 4 && report.protocolErrors == 0 && report.runtimeErrors == 0 && !report.aiActive && viewProof.Passed(session.IsHost) &&
                (role == "host" ? report.physicsTicks > 0 && report.processedInputs > 0 : report.physicsTicks == 0 && report.receivedSnapshots > 0) &&
                (!exerciseOfficials || (completedRecoveries >= 3 && recoveryStages == 7 && officialsCalls == 7 && officialsErrors == 0));
            Debug.Log("ONLINE_VALIDATION " + JsonUtility.ToJson(report));
            Application.Quit(ok ? 0 : 1);
        }
        [Serializable] private sealed class ValidationReport
        {
            public string role, mode, roomCode, winner, phase, inputSource, status;
            public uint epoch;
            public int[] points;
            public int nearHits, farHits, forehandHits, backhandHits, maxRallyHits, nextServes, completedPoints,
                sentInputs, processedInputs, sentSnapshots, receivedSnapshots, protocolErrors, runtimeErrors, inputDelivered;
            public OnlineViewValidation view;
            public ulong physicsTicks;
            public bool timedOut, aiActive;
            public bool exerciseOfficials;
            public int recoveryFrames, recoveryStages, officialsCalls, officialsChecks, officialsErrors, completedRecoveries;
        }
        private sealed class ValidationSource : BufferedPlayerInputSource
        {
            private readonly OnlineTennisSession session;
            private readonly TennisMatchController controller;
            private uint sequence;
            private double lastCommand;
            private readonly bool exerciseOfficials;
            private int officialsStage;
            public ValidationSource(OnlineTennisSession session, TennisMatchController controller, bool exerciseOfficials) : base("Validation input (CLI only)")
            { this.session = session; this.controller = controller; this.exerciseOfficials = exerciseOfficials; }
            public override void Poll(double now, float power01, float aimX, float aimZ)
            {
                if (!session.Playing || now - lastCommand < .1) return;
                var state = session.IsHost ? GamePayload.Capture(controller, "validation", 0) : session.RemoteState;
                if (state == null) return;
                PlayerAction action;
                if (session.IsHost && controller.ReadyToServe)
                {
                    if (exerciseOfficials && officialsStage < 3)
                    {
                        if (controller.Recovery.Completed < officialsStage) return;
                        action = officialsStage == 1 ? PlayerAction.LetServe : PlayerAction.NetServe;
                        officialsStage++;
                    }
                    else action = PlayerAction.Serve;
                }
                else
                {
                    if (state.phase != "Rally" || state.hits >= 6 || state.bounceCount != 1 || state.ball.pos[1] < .95f ||
                        !(session.IsHost ? controller.CanReturn(CourtSide.Near) : state.hitReady)) return;
                    action = (state.hits / 2) % 2 == 0 ? PlayerAction.Forehand : PlayerAction.Backhand;
                }
                lastCommand = now;
                float facing = session.IsHost ? 1 : -1;
                float horizontal = Mathf.Clamp(Mathf.Atan2(-state.ball.pos[0] * 1.6f * facing, 17) * Mathf.Rad2Deg / 25, -1, 1);
                Enqueue(new PlayerInputSample(action, ++sequence, now, .35f, horizontal));
            }
        }
    }
}
