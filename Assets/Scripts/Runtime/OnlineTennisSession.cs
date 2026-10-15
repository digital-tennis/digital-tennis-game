using System;
using UnityEngine;

namespace DigitalTennis
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerInputAdapter))]
    public sealed class OnlineTennisSession : MonoBehaviour
    {
        private TennisMatchController controller;
        private PlayerInputAdapter input;
        private MatchWebSocketClient socket;
        private readonly SnapshotBuffer snapshots = new SnapshotBuffer();
        private string address = "ws://127.0.0.1:8080/ws", enteredCode = "", token, pendingRequest, matchId;
        private double heartbeatAt, lastServerAt, snapshotAt, reconnectAt, disconnectedAt;
        private uint requestSequence, epoch, tick, inputSequence, lastInput;
        private bool hasInput, resuming, resumeAllowed, closed;
        public bool IsHost { get; private set; }
        public bool InRoom => !string.IsNullOrEmpty(RoomCode);
        public bool Playing { get; private set; }
        public bool IsOnline => controller != null && controller.Mode == GameMode.Online;
        public string RoomCode { get; private set; }
        public string Status { get; private set; } = "Create a room or enter a six-character code.";
        public uint Epoch => epoch;
        public GamePayload RemoteState => snapshots.Latest;
        public GamePayload PresentedState => snapshots.PresentedState;
        public bool OfficialsPresentationAligned => PresentedState?.officials != null &&
            controller.Officials.CallId == PresentedState.officials.callId &&
            controller.Officials.CallText == PresentedState.officials.text &&
            (int)controller.Officials.RecoveryPhase == PresentedState.officials.recovery &&
            (controller.Officials.RunnerPosition - snapshots.PresentedRunnerPosition).sqrMagnitude < .000001f;
        public bool PresentationAligned => PresentedState != null &&
            (controller.NearAvatar.transform.position - snapshots.PresentedNearPosition).sqrMagnitude < .000001f &&
            (controller.FarAvatar.transform.position - snapshots.PresentedFarPosition).sqrMagnitude < .000001f &&
            (controller.Ball.transform.position - snapshots.PresentedBallPosition).sqrMagnitude < .000001f;
        public string ResultWinner { get; private set; }
        public int SentSnapshots { get; private set; }
        public int ReceivedSnapshots => snapshots.Received;
        public int SentInputs { get; private set; }
        public int ProcessedInputs { get; private set; }
        public int ProtocolErrors { get; private set; }
        public string LastProtocolError { get; private set; }
        public int MaxRallyHits { get; private set; }
        public int CompletedPoints { get; private set; }
        public int NextServes { get; private set; }
        private uint lastServeRally;

        private void Start()
        {
            controller = GetComponent<TennisMatchController>(); input = GetComponent<PlayerInputAdapter>();
            if (!Application.isEditor) gameObject.AddComponent<OnlineValidationDriver>();
            if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-dt-roster-preview") >= 0) gameObject.AddComponent<CharacterShowcaseDriver>();
        }
        public void CreateRoom(string serverAddress) => Connect(serverAddress, null);
        public void JoinRoom(string serverAddress, string code) => Connect(serverAddress, code);
        private void Connect(string serverAddress, string code)
        {
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo))
            { Status = "Enter a ws:// or wss:// server address."; return; }
            string normalized = code?.Trim().ToUpperInvariant();
            if (normalized != null && (normalized.Length != 6 || Array.Exists(normalized.ToCharArray(), c => !"ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(c))))
            { Status = "The room code must contain six valid characters."; return; }
            Leave(false);
            address = serverAddress; enteredCode = normalized ?? ""; IsHost = code == null;
            matchId = Guid.NewGuid().ToString("N"); epoch = tick = inputSequence = lastInput = 0; hasInput = false;
            SentSnapshots = SentInputs = ProcessedInputs = ProtocolErrors = MaxRallyHits = CompletedPoints = NextServes = 0;
            LastProtocolError = null;
            ResultWinner = null; lastServeRally = 0;
            snapshots.Clear(); RoomCode = null; token = null; resuming = false; closed = false;
            controller.EnterOnline(IsHost); input.OnlineRoute = RouteInput; input.InputEnabled = false;
            pendingRequest = IsHost ? "room.create" : "room.join";
            Status = "Connecting to the room server..."; OpenSocket();
        }
        private void OpenSocket()
        {
            socket?.Dispose(); socket = new MatchWebSocketClient(new Uri(address));
            lastServerAt = heartbeatAt = Time.realtimeSinceStartupAsDouble;
        }
        public void Leave(bool returnToSinglePlayer = true)
        {
            if (socket != null)
            {
                // Drain the explicit leave request before the close handshake; unexpected loss uses disconnect grace.
                if (socket.Connected && InRoom) Send("room.leave", new RoomRequest());
                socket.CloseGracefully(); socket = null;
            }
            Playing = false; RoomCode = null; token = null; resumeAllowed = false; closed = true;
            snapshots.Clear();
            if (input != null) { input.OnlineRoute = null; input.InputEnabled = true; input.Typing = false; }
            if (controller != null && returnToSinglePlayer)
            {
                controller.Ball.IsAuthoritative = true; controller.IsPaused = false; controller.Configure(GameMode.SinglePlayer, controller.AiDifficulty);
            }
            Status = "Create a room or enter a six-character code.";
        }
        private void OnDestroy() { socket?.Dispose(); }
        private bool Send(string type, RoomRequest payload)
        {
            if (socket == null) return false;
            var message = new WireEnvelope<RoomRequest> { type = type, requestId = "u" + ++requestSequence, payload = payload };
            if (socket.Send(JsonUtility.ToJson(message))) return true;
            Pause("Network send queue unavailable."); return false;
        }
        private void Pause(string reason)
        {
            Playing = false; controller.IsPaused = true; input.InputEnabled = false; Status = reason;
        }
        private void FixedUpdate() { if (Playing && IsHost) tick++; }
        private void Update()
        {
            if (controller == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (socket != null)
            {
                for (int n = 0; n < 128 && socket != null && socket.TryRead(out var notice); n++)
                {
                    if (notice.Kind == "connected")
                    {
                        Send(pendingRequest, new RoomRequest { code = enteredCode, reconnectToken = resuming ? token : null });
                        lastServerAt = now;
                    }
                    else if (notice.Kind == "message")
                    {
                        lastServerAt = now;
                        try { Receive(notice.Data, now); }
                        catch (Exception e) { ProtocolErrors++; LastProtocolError = e.GetType().Name; Pause("Invalid server message: " + e.GetType().Name); }
                    }
                    else if (notice.Kind == "error") Status = notice.Data;
                    else if (notice.Kind == "disconnected")
                    {
                        socket.Dispose(); socket = null;
                        Pause("Connection lost. Waiting to reconnect...");
                        if (!resuming) disconnectedAt = now;
                        resumeAllowed = !closed && InRoom && !string.IsNullOrEmpty(token);
                        reconnectAt = now + 1;
                    }
                }
                if (socket != null && socket.Connected)
                {
                    if (now - heartbeatAt >= 5) { heartbeatAt = now; Send("heartbeat", new RoomRequest { clientTimeMs = now * 1000 }); }
                    if (now - lastServerAt >= 15) { Pause("Server heartbeat timed out."); socket.Dispose(); }
                }
            }
            if (socket == null && resumeAllowed && now >= reconnectAt)
            {
                if (now - disconnectedAt >= 14) { resumeAllowed = false; Pause("Reconnect expired. Leave and create a new room."); }
                else { resuming = true; pendingRequest = "session.resume"; enteredCode = RoomCode; OpenSocket(); }
            }
            if (!Playing) return;
            if (IsHost)
            {
                var state = GamePayload.Capture(controller, matchId, lastInput);
                Observe(state);
                if (now >= snapshotAt)
                {
                    snapshotAt = now + 1d / 30;
                    SendGame(new GameMessage { type = "snapshot", epoch = epoch, tick = tick, payload = state }); SentSnapshots++;
                }
                if (controller.Match.Score.Winner.HasValue && ResultWinner == null)
                {
                    ResultWinner = state.winner;
                    SendGame(new GameMessage { type = "result", epoch = epoch, payload = new GamePayload { matchId = matchId, winner = ResultWinner } });
                    Debug.Log($"ONLINE_RESULT host winner={ResultWinner} points={state.points[0]}-{state.points[1]}");
                }
                if (controller.Match.Phase != MatchPhase.MatchEnd) ResultWinner = null;
            }
            else snapshots.Present(now, controller);
        }
        private void Receive(string json, double now)
        {
            var header = JsonUtility.FromJson<EnvelopeHeader>(json);
            if (header == null || header.v != 1) throw new ArgumentException("Envelope");
            if (header.type == "error")
            {
                var error = JsonUtility.FromJson<WireEnvelope<ErrorPayload>>(json).payload;
                ProtocolErrors++; LastProtocolError = error.code; Status = "Server: " + error.code;
                if (resuming) { resumeAllowed = false; closed = true; Pause(Status); }
                return;
            }
            var p = JsonUtility.FromJson<WireEnvelope<ServerMessagePayload>>(json).payload;
            switch (header.type)
            {
                case "room.created": case "room.joined": case "session.resumed":
                    if (p.playerId != (IsHost ? "host" : "guest")) throw new ArgumentException("Role");
                    RoomCode = p.code; enteredCode = p.code; token = p.reconnectToken; resumeAllowed = false;
                    Status = IsHost ? "Room " + RoomCode + " - waiting for guest" : "Joined " + RoomCode + " - agreeing relay transport";
                    Debug.Log($"ONLINE_ROOM role={(IsHost ? "host" : "guest")} code={RoomCode}");
                    break;
                case "room.ready":
                    Pause("Both players connected. Agreeing WebSocket relay...");
                    if (IsHost) Send("transport.propose", new RoomRequest { mode = "relay", epoch = p.epoch + 1 });
                    break;
                case "transport.propose":
                    if (!IsHost && p.mode == "relay" && p.epoch == epoch + 1)
                        Send("transport.ack", new RoomRequest { mode = p.mode, epoch = p.epoch });
                    break;
                case "transport.ack":
                    if (IsHost && p.mode == "relay" && p.epoch == epoch + 1)
                        Send("transport.commit", new RoomRequest { mode = p.mode, epoch = p.epoch });
                    break;
                case "transport.committed":
                    if (p.mode != "relay" || p.epoch != epoch + 1) throw new ArgumentException("Transport epoch");
                    epoch = p.epoch; hasInput = false; snapshots.Clear();
                    controller.IsPaused = false; Playing = true; input.InputEnabled = IsHost; resuming = false; resumeAllowed = false;
                    Status = "Online - connected via relay. You are at the bottom of the court.";
                    snapshotAt = 0;
                    Debug.Log($"ONLINE_PLAY role={(IsHost ? "host" : "guest")} mode=relay epoch={epoch}");
                    break;
                case "relay.message":
                    if (!Playing || p.epoch != epoch || p.message == null || p.message.epoch != epoch) return;
                    if (IsHost && p.from == "guest" && p.message.type == "input") ProcessRemoteInput(p.message, now);
                    else if (!IsHost && p.from == "host")
                    {
                        var message = p.message;
                        if (message.type == "snapshot" && snapshots.Push(message, now))
                        {
                            input.InputEnabled = true; Observe(message.payload);
                        }
                        else if (message.type == "result" && message.payload?.matchId == RemoteState?.matchId)
                        { ResultWinner = message.payload.winner; Debug.Log("ONLINE_RESULT guest winner=" + ResultWinner); }
                        else if (message.type == "pause") Pause(message.payload.reason);
                    }
                    break;
                case "peer.disconnected": Pause("Peer disconnected. Match paused for reconnect (15 seconds)."); break;
                case "peer.left": case "room.closed": case "room.expired":
                    closed = true; resumeAllowed = false; token = null; Pause("Room closed. Leave and create a new room."); break;
            }
        }
        private void Observe(GamePayload state)
        {
            MaxRallyHits = Math.Max(MaxRallyHits, state.hits);
            CompletedPoints = Math.Max(CompletedPoints, state.points[0] + state.points[1]);
            if (state.rallyId != lastServeRally)
            { lastServeRally = state.rallyId; NextServes++; }
        }
        private void SendGame(GameMessage message) => Send("game.relay", new RoomRequest { epoch = epoch, message = message });
        private void RouteInput(PlayerInputSample sample, uint session)
        {
            if (sample.Action == PlayerAction.Characters) { controller.Characters.ToggleMenu(); return; }
            if (!Playing || sample.PracticeOpponent) return;
            if (IsHost)
            {
                if (sample.Action == PlayerAction.Restart) matchId = Guid.NewGuid().ToString("N");
                controller.ApplyPlayerInput(sample, session, CourtSide.Near);
            }
            else if (RemoteState != null && InputKind(sample.Action) != null)
            {
                if (++inputSequence == 0) inputSequence = 1;
                SendGame(new GameMessage { type = "input", epoch = epoch, inputSeq = inputSequence, playerId = "guest",
                    payload = new GamePayload { eventId = inputSequence, kind = InputKind(sample.Action), choice = sample.Choice,
                        power01 = sample.Power01, aimX = sample.AimX, aimZ = sample.AimZ, rallyId = RemoteState.rallyId, shotId = RemoteState.shotId } });
                SentInputs++;
            }
        }
        private void ProcessRemoteInput(GameMessage message, double now)
        {
            if (message.playerId != "guest" || (hasInput && !Sequence32.IsNewer(message.inputSeq, lastInput))) return;
            var p = message.payload;
            if (p == null || p.eventId == 0 || !TryAction(p.kind, out var action)) return;
            hasInput = true; lastInput = message.inputSeq; ProcessedInputs++;
            // Host receipt time is local. Client clocks and IMU timestamps never decide a hit.
            var sample = new PlayerInputSample(action, p.eventId, now, p.power01, p.aimX, p.aimZ, false, p.choice);
            if (action == PlayerAction.Skill && (p.rallyId != controller.Match.RallyId || p.shotId != controller.Match.CurrentShot.ShotId)) return;
            controller.ApplyPlayerInput(sample, unchecked(0x80000000u + epoch), CourtSide.Far, p.rallyId, p.shotId);
        }
        private static string InputKind(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Forehand: return "forehand"; case PlayerAction.Backhand: return "backhand";
                case PlayerAction.Smash: return "smash"; case PlayerAction.Volley: return "volley"; case PlayerAction.Slice: return "slice";
                case PlayerAction.Lob: return "lob"; case PlayerAction.Skill: return "skill"; case PlayerAction.SelectCharacter: return "character";
                default: return null;
            }
        }
        private static bool TryAction(string kind, out PlayerAction action)
        {
            foreach (PlayerAction candidate in new[] { PlayerAction.Forehand, PlayerAction.Backhand, PlayerAction.Smash, PlayerAction.Volley,
                PlayerAction.Slice, PlayerAction.Lob, PlayerAction.Skill, PlayerAction.SelectCharacter })
                if (InputKind(candidate) == kind) { action = candidate; return true; }
            action = default; return false;
        }
        private void OnGUI()
        {
            if (FrontEndController.HideLegacyHud) return;
            if (controller == null || input == null) return;
            if (IsOnline) DrawOnlineMatch();
            float x = Screen.width - 376, y = IsOnline ? 16 : 280;
            var pointer = Event.current;
            if (pointer.type == EventType.MouseDown &&
                !new Rect(x + 12, y + 54, 336, 25).Contains(pointer.mousePosition) &&
                !new Rect(x + 12, y + 87, 150, 27).Contains(pointer.mousePosition)) GUI.FocusControl(null);
            GUI.Box(new Rect(x, y, 360, 260), "Online two-player rooms");
            GUI.Label(new Rect(x + 12, y + 30, 330, 22), "Server address");
            bool editable = GUI.enabled; GUI.enabled = editable && !IsOnline;
            GUI.SetNextControlName("online-address"); address = GUI.TextField(new Rect(x + 12, y + 54, 336, 25), address, 256);
            GUI.SetNextControlName("online-code"); enteredCode = GUI.TextField(new Rect(x + 12, y + 87, 150, 27), enteredCode, 6);
            GUI.enabled = editable;
            bool previous = GUI.enabled;
            GUI.enabled = previous && !IsOnline;
            if (GUI.Button(new Rect(x + 174, y + 87, 174, 27), "Join room")) { JoinRoom(address, enteredCode); GUI.FocusControl(null); }
            if (GUI.Button(new Rect(x + 12, y + 122, 164, 28), "Create room")) { CreateRoom(address); GUI.FocusControl(null); }
            GUI.enabled = previous && IsOnline;
            if (GUI.Button(new Rect(x + 184, y + 122, 164, 28), "Leave online")) Leave();
            GUI.enabled = previous;
            GUI.Label(new Rect(x + 12, y + 160, 336, 48), Status);
            GUI.Label(new Rect(x + 12, y + 211, 336, 40), $"Input: {input.SourceName} | Space: host serve\nQ/W: racket | N: host net fault / ball boy");
            input.Typing = GUI.GetNameOfFocusedControl() == "online-address" || GUI.GetNameOfFocusedControl() == "online-code";
        }
        private void DrawOnlineMatch()
        {
            var s = IsHost ? GamePayload.Capture(controller, matchId, lastInput) : RemoteState;
            GUI.Box(new Rect(16, 16, 405, 345), "Digital Tennis - Online / WebSocket relay");
            if (s == null) { GUI.Label(new Rect(32, 48, 370, 30), "Waiting for the host snapshot..."); return; }
            var score = new TennisScore(s.points[0], s.points[1]);
            CourtSide local = controller.View.LocalPlayer, opponent = controller.View.Opponent;
            GUI.Label(new Rect(32, 48, 370, 28), $"YOU  {score.Display(local)} : {score.Display(opponent)}  OPPONENT");
            GUI.Label(new Rect(32, 80, 370, 24), $"{s.phase} | rally {s.rallyId} | hits {s.hits}");
            GUI.Label(new Rect(32, 108, 370, 30), s.phase == "MatchEnd" ? (s.winner == (IsHost ? "host" : "guest") ? "You win the game" : "Opponent wins the game") :
                s.officials != null && s.officials.recovery != 0 ? "Court paused - ball boy collecting" :
                s.phase == "WaitServe" ? $"Ready - serve {s.faultCount + 1}" : (s.lastHitter == (IsHost ? "host" : "guest") ? "You hit the ball" : "Opponent hit the ball"));
            GUI.Label(new Rect(32, 140, 370, 24), $"You: {CharacterCatalog.Ids[s.characters[(int)local].index]} | Opponent: {CharacterCatalog.Ids[s.characters[(int)opponent].index]} | Serve: {(IsHost ? "you" : "opponent")}");
            bool previous = GUI.enabled;
            GUI.enabled = previous && Playing && IsHost && controller.ReadyToServe;
            if (GUI.Button(new Rect(32, 174, 178, 28), "Serve [Space]")) input.Press(PlayerAction.Serve);
            GUI.enabled = previous && Playing;
            if (GUI.Button(new Rect(32, 210, 178, 28), "Your forehand [Q]")) input.Press(PlayerAction.Forehand);
            if (GUI.Button(new Rect(218, 210, 178, 28), "Your backhand [W]")) input.Press(PlayerAction.Backhand);
            GUI.enabled = previous && Playing && IsHost;
            if (GUI.Button(new Rect(218, 174, 178, 28), "Restart game [R]")) input.Press(PlayerAction.Restart);
            GUI.enabled = previous;
            GUI.Label(new Rect(32, 247, 365, 22), $"Power {input.Power01:F2}");
            input.Power01 = GUI.HorizontalSlider(new Rect(150, 254, 245, 16), input.Power01, 0, 1);
            GUI.Label(new Rect(32, 275, 365, 22), $"Aim {input.AimX:F2}");
            input.AimX = GUI.HorizontalSlider(new Rect(150, 282, 245, 16), input.AimX, -1, 1);
            GUI.Label(new Rect(32, 309, 365, 24), $"Your hits {(IsHost ? s.nearHits : s.farHits)} | Opponent hits {(IsHost ? s.farHits : s.nearHits)}");
        }
    }
}
