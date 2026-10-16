using System;
using System.Collections;
using System.Collections.Generic;
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
        private bool imageRequested, recoveryImageRequested, carryImageRequested, exerciseOfficials;
        private bool exerciseCharacters, skillImageRequested;
        private bool exerciseEffects;
        private readonly bool[] effectImages=new bool[5];
        private readonly HashSet<string> scoreImages=new HashSet<string>();
        private bool menuCourtSelected;
        private bool menuInitialized, menuPlayerPicked, menuCourtPicked;
        private double nextMenuInputAt;
        private int menuClickFailures;
        private string menuPointerProblem;
        private readonly List<string> menuClicks = new List<string>();
        private readonly List<string> menuStages = new List<string>();
        private int cinematicFrames, characterChecks, characterErrors;
        private readonly int[] maxStocks = new int[2];
        private int capturesRequested;
        private int recoveryFrames, recoveryStages, officialsCalls, officialsChecks, officialsErrors, completedRecoveries;
        private bool enabledByArguments, started, installedSource, finished;
        private double startedAt, endedAt;
        private int runtimeErrors;
        private float deadlineSeconds = 150;
        private readonly OnlineViewValidation viewProof = new OnlineViewValidation();
        private void LateUpdate()
        {
            if (!enabledByArguments || finished) return;
            SampleMenu();
            if(exerciseEffects&&!string.IsNullOrEmpty(viewImage))
                for(int i=0;i<5;i++)
                    if(!effectImages[i]&&controller.Effects.HasActive((TennisEffectKind)i,i==4?.55f:.04f,i==4?.9f:.16f))
                    {effectImages[i]=true;StartCoroutine(CaptureView(viewImage.Replace(".png","-fx-"+((TennisEffectKind)i).ToString().ToLowerInvariant()+".png")));}
            viewProof.Sample(controller, session);
            var state = session.IsHost ? (session.Playing ? GamePayload.Capture(controller, "image", 0) : null) : session.PresentedState;
            if(state?.score!=null&&!string.IsNullOrEmpty(viewImage))
            {
                var score=state.score;
                string milestone=state.phase=="MatchEnd"?"match-result":
                    score.tieBreak&&state.points[0]==6&&state.points[1]==6?"tiebreak-deuce":
                    score.tieBreak&&state.points[0]==0&&state.points[1]==0?"tiebreak-start":
                    score.setComplete?"set-result":score.sets[0]==1&&score.games[0]==0&&score.games[1]==0?"next-set":
                    score.games[0]==1&&score.games[1]==0&&state.phase=="WaitServe"?"next-game":null;
                if(milestone!=null&&scoreImages.Add(milestone))StartCoroutine(CaptureView(viewImage.Replace(".png","-"+milestone+".png")));
            }
            if (state?.characters != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var p = state.characters[i]; characterChecks++; maxStocks[i] = Math.Max(maxStocks[i], p.stock);
                    var actual = controller.Characters.Player((CourtSide)i).Capture();
                    if (!p.IsValid || actual.index != p.index || actual.stock != p.stock || actual.activationId != p.activationId || actual.balance.serve != p.balance.serve) characterErrors++;
                    if (p.cinematicAge < CharacterAbility.CinematicSeconds)
                    {
                        cinematicFrames++;
                        if (exerciseCharacters && !skillImageRequested && !string.IsNullOrEmpty(viewImage) && p.cinematicAge >= .42f)
                        { skillImageRequested = true; StartCoroutine(CaptureView(viewImage.Replace(".png", "-skill.png"))); }
                    }
                }
            }
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
                if (exerciseOfficials && !carryImageRequested && !string.IsNullOrEmpty(viewImage) && o.recovery == (int)RecoveryPhase.Returning)
                { carryImageRequested = true; StartCoroutine(CaptureView(viewImage.Replace(".png", "-carry.png"), .3f)); }
            }
            if (!imageRequested && !string.IsNullOrEmpty(viewImage) && state != null && state.rallyId == 2 && state.phase == "Rally" && state.hits >= 2)
            { imageRequested = true; StartCoroutine(CaptureView(viewImage)); }
        }
        private IEnumerator CaptureView(string path, float delay = 0)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            yield return new WaitForEndOfFrame();
            SaveGameFrame(path);
            var record = new CaptureRecord { source = Application.isBatchMode ? (FrontEndController.Active != null ? "game-cameras-with-canvas" : "game-camera-without-imgui") : "game-frame-with-imgui",
                role = role, frame = Time.frameCount, cameraPosition = GamePayload.Pack(Camera.main.transform.position),
                state = session.IsHost ? GamePayload.Capture(controller, "capture", 0) : session.PresentedState,
                effects=controller.Effects.Metrics(),liveParticles=controller.Effects.LiveParticles,activeEffects=controller.Effects.ActiveEffects };
            File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(record, true));
        }
        private void SaveGameFrame(string path)
        {
            if (Application.isBatchMode)
            {
                var front = FrontEndController.Active; front?.RenderUiNow();
                GameCameraCapture.Save(Camera.main, path, front != null ? 1600 : 1280, front != null ? 900 : 800, front?.UiCamera);
            }
            else ScreenCapture.CaptureScreenshot(path);
            capturesRequested++;
        }
        private void SampleMenu()
        {
            var f=FrontEndController.Active;
            if(f==null || !f.Ready) return;
            var stage=f.Model.Stage;
            // Keep the selected profile in the proof, rather than the initial default profile.
            if((stage==FrontEndStage.Player&&!menuPlayerPicked) || (stage==FrontEndStage.Court&&!menuCourtPicked))return;
            if(menuStages.Contains(stage.ToString()))return;
            menuStages.Add(stage.ToString());
            Debug.Log("ONLINE_MENU_STAGE "+role+" "+stage);
            if(!string.IsNullOrEmpty(viewImage))StartCoroutine(CaptureMenu(stage));
        }
        private IEnumerator CaptureMenu(FrontEndStage stage)
        {
            yield return new WaitForEndOfFrame();
            var f=FrontEndController.Active;
            if(f==null || f.Model.Stage!=stage)yield break;
            string path=viewImage.Replace(".png","-menu-"+stage.ToString().ToLowerInvariant()+".png");
            SaveGameFrame(path);
            File.WriteAllText(Path.ChangeExtension(path,".json"),JsonUtility.ToJson(new MenuFrame {
                role=role,stage=stage.ToString(),frame=Time.frameCount,focus=f.Model.Focus,player=f.Model.Player,court=f.Model.Court,
                pointerActivations=f.View.PointerActivations,callbackActivations=f.View.CallbackActivations,
                source=Application.isBatchMode?"game-cameras-with-canvas":"game-frame-with-canvas" },true));
        }
        [Serializable] private sealed class MenuFrame
        {public string role,stage,source;public int frame,focus,player,court,pointerActivations,callbackActivations;}
        [Serializable] private sealed class CaptureRecord
        {
            public string source, role;
            public int frame;
            public float[] cameraPosition;
            public GamePayload state;
            public TennisEffectMetrics effects;
            public int liveParticles,activeEffects;
        }
        private void Start()
        {
            string Arg(string name) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
            role = Arg("-dt-online-role"); enabledByArguments = role == "host" || role == "guest";
            exerciseOfficials = Array.IndexOf(Environment.GetCommandLineArgs(), "-dt-officials") >= 0;
            exerciseCharacters = Array.IndexOf(Environment.GetCommandLineArgs(), "-dt-characters") >= 0;
            exerciseEffects = Array.IndexOf(Environment.GetCommandLineArgs(), "-dt-effects") >= 0;
            if (!enabledByArguments) { enabled = false; return; }
            address = Arg("-dt-server") ?? "ws://127.0.0.1:8080/ws";
            codeFile = Arg("-dt-code-file"); reportFile = Arg("-dt-report"); viewImage = Arg("-dt-view-image");
            if (float.TryParse(Arg("-dt-timeout"), out var timeout)) deadlineSeconds = timeout;
            session = GetComponent<OnlineTennisSession>(); controller = GetComponent<TennisMatchController>();
            startedAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
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
                controller.InputAdapter.UseSource(new ValidationSource(session, controller, exerciseOfficials, exerciseCharacters)); installedSource = true;
            }
            if (!started)
            {
                if (FrontEndController.Active != null) DriveMenu();
                else if (role == "host") { session.CreateRoom(address); started = true; }
                else if (!string.IsNullOrEmpty(codeFile) && File.Exists(codeFile))
                {
                    string code = File.ReadAllText(codeFile).Trim();
                    if (code.Length == 6) { session.JoinRoom(address, code); started = true; }
                }
            }
            if (role == "host" && session.InRoom && !string.IsNullOrEmpty(codeFile) && !File.Exists(codeFile)) PublishCode();
            if (session.ResultWinner != null && endedAt == 0) endedAt = now;
            // Stop the snapshot producer first. Closing the guest first leaves host snapshots
            // in flight against a paused room, which is a shutdown race rather than gameplay.
            if (endedAt != 0 && now - endedAt >= (role == "host" ? 4 : 6)) Finish(false);
            else if (now - startedAt >= deadlineSeconds) Finish(true);
        }
        private void DriveMenu()
        {
            var f = FrontEndController.Active; if (!f.Ready) return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(!menuInitialized) {menuInitialized=true;nextMenuInputAt=now+.35;return;}
            if(now<nextMenuInputAt)return;
            switch (f.Model.Stage)
            {
                case FrontEndStage.Logo: f.Confirm();nextMenuInputAt=now+.35;break;
                case FrontEndStage.Title: ClickMenu("continue");break;
                case FrontEndStage.Menu: ClickMenu("menu.online"); break;
                case FrontEndStage.Player:
                    // Pick rebuilds the page. A new button has no raycast depth until the canvas
                    // has drawn it, so never pick and confirm in the same frame or re-pick forever.
                    if(!menuPlayerPicked)menuPlayerPicked=ClickMenu("country."+(role=="host"?"KOR":exerciseCharacters?"ESP":"JPN"));
                    else ClickMenu("confirm");
                    break;
                case FrontEndStage.Court:
                    if(!menuCourtPicked)menuCourtPicked=ClickMenu("country.ESP");
                    else ClickMenu("confirm");
                    break;
                case FrontEndStage.Lobby:
                    f.ServerAddress = address;
                    if (role == "host")
                    {
                        if (exerciseCharacters && !menuCourtSelected) { menuCourtSelected=ClickMenu("lobby.court");return; }
                        if(ClickMenu("lobby.create"))started=session.IsOnline;
                    }
                    else if (!string.IsNullOrEmpty(codeFile) && File.Exists(codeFile))
                    {
                        var code = File.ReadAllText(codeFile).Trim(); if (code.Length != 6) return;
                        f.View.RoomCode.text = code;if(ClickMenu("lobby.join"))started=session.IsOnline;
                    }
                    break;
            }
        }
        private bool ClickMenu(string id)
        {
            var f=FrontEndController.Active;
            // One action per rendered page, with enough time to inspect/capture normal windows.
            nextMenuInputAt=Time.realtimeSinceStartupAsDouble+.35;
            if(f.View.Invoke(id)) {menuClicks.Add(id);Debug.Log("ONLINE_MENU_CLICK "+role+" "+id);return true;}
            menuClickFailures++;
            if(menuPointerProblem!=f.View.PointerProblem)
            {menuPointerProblem=f.View.PointerProblem;Debug.LogWarning("ONLINE_MENU_POINTER "+role+" "+menuPointerProblem);}
            return false;
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
                timedOut = timedOut, winner = session.ResultWinner, points = state?.points, score=state?.score,phase = state?.phase,
                nearHits = state?.nearHits ?? 0, farHits = state?.farHits ?? 0,
                forehandHits = state?.forehandHits ?? 0, backhandHits = state?.backhandHits ?? 0,
                maxRallyHits = session.MaxRallyHits, nextServes = session.NextServes, completedPoints = session.CompletedPoints,
                sentInputs = session.SentInputs, processedInputs = session.ProcessedInputs,
                sentSnapshots = session.SentSnapshots, receivedSnapshots = session.ReceivedSnapshots,
                lastProtocolError = session.LastProtocolError,
                physicsTicks = controller.Ball.PhysicsTicks, protocolErrors = session.ProtocolErrors, runtimeErrors = runtimeErrors,
                inputSource = controller.InputAdapter.SourceName, inputDelivered = controller.InputAdapter.Delivered,
                aiActive = controller.Opponent != null, status = session.Status, view = viewProof,
                exerciseOfficials = exerciseOfficials, recoveryFrames = recoveryFrames, recoveryStages = recoveryStages,
                captureMode = string.IsNullOrEmpty(viewImage) ? "none" : Application.isBatchMode ? (FrontEndController.Active != null ? "camera-canvas" : "camera") : "screen", capturesRequested = capturesRequested,
                menuTransitions = FrontEndController.Active?.Transitions ?? 0,
                menuStages=menuStages.ToArray(),menuClicks=menuClicks.ToArray(),menuClickFailures=menuClickFailures,
                menuPointerActivations=FrontEndController.Active?.View.PointerActivations??0,
                menuCallbackActivations=FrontEndController.Active?.View.CallbackActivations??0,lastMenuPointerProblem=menuPointerProblem,
                officialsCalls = officialsCalls, officialsChecks = officialsChecks, officialsErrors = officialsErrors, completedRecoveries = completedRecoveries,
                exerciseCharacters = exerciseCharacters, characters = state?.characters, courtIndex = state?.courtIndex ?? -1,
                gameplayHash=state?.gameplayHash, moveSpeed=controller.Tuning["move.speed"],
                balanceRevision = state?.balanceRevision ?? 0, maxStocks = maxStocks, cinematicFrames = cinematicFrames,
                characterChecks = characterChecks, characterErrors = characterErrors,
                effects=controller.Effects.Metrics(),exerciseEffects=exerciseEffects,transientPeerPauses=session.TransientPeerPauses
            };
            if (!string.IsNullOrEmpty(reportFile)) File.WriteAllText(reportFile, JsonUtility.ToJson(report, true));
            bool ok = !timedOut && report.winner == "host" && report.score.sets[0]==2 && report.score.sets[1]==0 &&
                report.score.completedSets.Length==2&&report.score.completedSets[0].nearGames==7&&report.score.completedSets[0].farGames==6&&
                report.score.completedSets[0].nearTieBreak==8&&report.score.completedSets[0].farTieBreak==6&&
                report.score.completedSets[1].nearGames==6&&report.score.completedSets[1].farGames==0&&report.completedPoints >= 86 && report.nextServes >= 86 &&
                report.nearHits > 0 && report.farHits > 0 && report.forehandHits > 0 && report.backhandHits > 0 &&
                report.maxRallyHits >= 4 && report.protocolErrors == 0 && report.runtimeErrors == 0 && !report.aiActive && viewProof.Passed(session.IsHost) &&
                (role == "host" ? report.physicsTicks > 0 && report.processedInputs > 0 : report.physicsTicks == 0 && report.receivedSnapshots > 0) &&
                (!exerciseOfficials || (completedRecoveries >= 3 && recoveryStages == 7 && officialsCalls == 7 && officialsErrors == 0)) &&
                (!exerciseCharacters || (report.characters[0].index == 0 && report.characters[1].index == 8 && report.courtIndex == 8 &&
                    report.characters[0].activationId > 0 && report.characters[1].activationId > 0 && report.characters[0].balance.serve == 77 &&
                    maxStocks[0] == 3 && maxStocks[1] == 3 && report.balanceRevision >= 3 && cinematicFrames > 0 && characterChecks > 30 && characterErrors == 0)) &&
                (FrontEndController.Active == null || report.menuTransitions >= 6) &&
                (!exerciseEffects || (Array.TrueForAll(report.effects.kinds,n=>n>0)&&report.effects.duplicateErrors==0&&report.effects.hitCount==26&&report.effects.csvRevision>=3&&
                    (session.IsHost||!exerciseEffects||report.effects.remoteBaselines>=2)&&
                    (string.IsNullOrEmpty(viewImage)||(report.effects.rendered>0&&report.effects.peakParticles>0&&Array.TrueForAll(effectImages,b=>b)))));
            Debug.Log("ONLINE_VALIDATION " + JsonUtility.ToJson(report));
            Application.Quit(ok ? 0 : 1);
        }
        [Serializable] private sealed class ValidationReport
        {
            public string role, mode, roomCode, winner, phase, inputSource, status, lastProtocolError;
            public uint epoch;
            public int[] points;
            public MatchScoreState score;
            public int nearHits, farHits, forehandHits, backhandHits, maxRallyHits, nextServes, completedPoints,
                sentInputs, processedInputs, sentSnapshots, receivedSnapshots, protocolErrors, runtimeErrors, inputDelivered;
            public OnlineViewValidation view;
            public ulong physicsTicks;
            public bool timedOut, aiActive;
            public bool exerciseOfficials;
            public string captureMode;
            public int capturesRequested;
            public int recoveryFrames, recoveryStages, officialsCalls, officialsChecks, officialsErrors, completedRecoveries;
            public bool exerciseCharacters;
            public CharacterState[] characters;
            public int courtIndex, cinematicFrames, characterChecks, characterErrors;
            public int[] maxStocks;
            public string gameplayHash;
            public float moveSpeed;
            public uint balanceRevision;
            public int menuTransitions;
            public string[] menuStages,menuClicks;
            public int menuClickFailures,menuPointerActivations,menuCallbackActivations;
            public string lastMenuPointerProblem;
            public bool exerciseEffects;
            public TennisEffectMetrics effects;
            public int transientPeerPauses;
        }
        private sealed class ValidationSource : BufferedPlayerInputSource
        {
            private readonly OnlineTennisSession session;
            private readonly TennisMatchController controller;
            private uint sequence;
            private double lastCommand;
            private readonly bool exerciseOfficials;
            private readonly bool exerciseCharacters;
            private readonly DateTime initialWrite;
            private bool courtSelected, balanceReloaded;
            private int officialsStage;
            public ValidationSource(OnlineTennisSession session, TennisMatchController controller, bool exerciseOfficials, bool exerciseCharacters) : base("Validation input (CLI only)")
            {
                this.session = session; this.controller = controller; this.exerciseOfficials = exerciseOfficials; this.exerciseCharacters = exerciseCharacters;
                initialWrite = File.GetLastWriteTimeUtc(controller.Characters.Store.Path);
            }
            public override void Poll(double now, float power01, float aimX, float aimZ)
            {
                if (!session.Playing || !controller.InputAdapter.GameplayEnabled || now - lastCommand < .1) return;
                var state = session.IsHost ? GamePayload.Capture(controller, "validation", 0) : session.RemoteState;
                if (state == null) return;
                if (exerciseCharacters)
                {
                    if (state.shotId == 0)
                    {
                        if (session.IsHost && !courtSelected) { Send(PlayerAction.SelectCourt, 8); courtSelected = true; return; }
                        if (!session.IsHost && state.characters[1].index != 8) { Send(PlayerAction.SelectCharacter, 8); return; }
                        if (session.IsHost && state.characters[1].index != 8) return;
                    }
                    if (session.IsHost && !balanceReloaded && File.GetLastWriteTimeUtc(controller.Characters.Store.Path) != initialWrite)
                    {
                        if (controller.Characters.CanReload) { Send(PlayerAction.ReloadBalance); balanceReloaded = true; return; }
                        if (controller.ReadyToServe) return; // Wait for effects to expire, then reload at a safe serve boundary.
                    }
                    var own = state.characters[session.IsHost ? 0 : 1];
                    if (own.stock == SkillMeter.Maximum && own.activationId == 0 && own.remaining == 0 && state.phase == "Rally" &&
                        state.characters[0].cinematicAge >= CharacterAbility.CinematicSeconds && state.characters[1].cinematicAge >= CharacterAbility.CinematicSeconds)
                    { Send(PlayerAction.Skill); return; }
                }
                PlayerAction action;
                bool ready=session.IsHost?controller.ReadyToServe:state.phase=="WaitServe"&&state.officials.recovery==0&&!controller.Characters.Cinematic;
                if (ready)
                {
                    if(state.serverId!=(session.IsHost?"host":"guest"))return;
                    if (session.IsHost&&exerciseOfficials && officialsStage < 3)
                    {
                        if (controller.Recovery.Completed < officialsStage) return;
                        action = officialsStage == 1 ? PlayerAction.LetServe : PlayerAction.NetServe;
                        officialsStage++;
                    }
                    else
                    {
                        // Full real scoring: 6–6 then an 8–6 tie-break, followed by 6–0.
                        // Deliberate serves/misses/faults are ordinary input, never score writes.
                        bool nearWins=state.score.completedSets.Length>0||
                            (state.score.tieBreak?state.points[0]+state.points[1]>=12||(state.points[0]+state.points[1])%2==0:state.score.games[0]==state.score.games[1]);
                        bool serverWins=(state.serverId=="host")==nearWins;
                        action=serverWins?PlayerAction.Serve:PlayerAction.FaultServe;
                    }
                }
                else
                {
                    // Use the real legal window; an extra visual-height threshold can miss a
                    // whole guest return between snapshots and leave skill-charge proof incomplete.
                    if (state.phase != "Rally" || state.score.totalPoints[0]+state.score.totalPoints[1]>=5 || state.hits >= 6 || state.bounceCount != 1 ||
                        !(session.IsHost ? controller.CanReturn(CourtSide.Near) : state.hitReady)) return;
                    action = (state.hits / 2) % 2 == 0 ? PlayerAction.Forehand : PlayerAction.Backhand;
                }
                lastCommand = now;
                float facing = (session.IsHost != state.score.EndsChanged) ? 1 : -1;
                float horizontal = Mathf.Clamp(Mathf.Atan2(-state.ball.pos[0] * 1.6f * facing, 17) * Mathf.Rad2Deg / 25, -1, 1);
                Enqueue(new PlayerInputSample(action, ++sequence, now, .35f, horizontal));
                void Send(PlayerAction command, int choice = -1)
                {
                    lastCommand = now;
                    Enqueue(new PlayerInputSample(command, ++sequence, now, .35f, 0, 0, false, choice));
                }
            }
        }
    }
}
