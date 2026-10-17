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
        private OnlinePeerTransport peer;
        private readonly OnlineTransportNegotiation negotiation=new OnlineTransportNegotiation();
        private OnlineConnectionSettings connectionSettings;
        private bool forceRelay,peerFailureSent;
        private string acknowledgedMode;
        public string TransportMode {get;private set;}="relay";
        public int DirectSent {get;private set;}
        public int DirectReceived {get;private set;}
        public int PeerFallbacks {get;private set;}
        public bool DirectChannelOpen=>peer!=null&&peer.Ready;
        public readonly System.Collections.Generic.List<string> TransportHistory=new System.Collections.Generic.List<string>();
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
        public float PingMs {get;private set;}=-1;
        public string FriendlyStatus
        {
            get
            {
                switch(LastProtocolError)
                {
                    case "ROOM_NOT_FOUND":return "방을 찾을 수 없습니다. 코드를 다시 확인하거나 새 방을 만드세요.";
                    case "ROOM_FULL":return "이미 두 명이 참가한 방입니다. 다른 방을 만들어 주세요.";
                    case "RESUME_EXPIRED":return "재접속 시간이 지났습니다. 새 방에서 다시 시작하세요.";
                    case "RATE_LIMITED":return "요청이 너무 빠릅니다. 잠시 기다린 뒤 다시 시도하세요.";
                    case "INVALID_CODE":return "방 코드는 영문·숫자 6자리입니다.";
                }
                if(Playing)return (TransportMode=="p2p"?"P2P 직접 연결됨":"WebSocket 릴레이 연결됨")+(PingMs>=0?" · 서버 왕복 "+PingMs.ToString("0")+" ms":"");
                if(Status.StartsWith("Enter"))return "서버 주소를 ws:// 또는 wss:// 형식으로 입력하세요.";
                if(Status.StartsWith("The room"))return "방 코드는 영문·숫자 6자리입니다.";
                if(Status.Contains("Reconnect expired")||Status.Contains("Room closed"))return "연결이 종료됐습니다. 메뉴로 돌아가 새 방을 만드세요.";
                if(InRoom&&(Status.Contains("disconnected")||Status.Contains("Connection lost")||Status.Contains("Peer unavailable")))return "연결이 끊겨 경기를 멈췄습니다. 최대 15초 동안 재접속을 기다립니다.";
                return null;
            }
        }
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
        public int TransientPeerPauses { get; private set; }
        public string LastProtocolError { get; private set; }
        public int MaxRallyHits { get; private set; }
        public int CompletedPoints { get; private set; }
        public int NextServes { get; private set; }
        private uint lastServeRally;
        private readonly OnlineRematchConsent consent=new OnlineRematchConsent();
        private bool guestVote,observedMatch;
        public int RematchesStarted {get;private set;}
        public string MatchId=>matchId;
        public bool OwnRematchRequested=>IsHost?consent.Host:guestVote;
        public bool PeerRematchRequested=>IsHost?consent.Guest:RemoteState?.rematchVotes?.Length==2&&RemoteState.rematchVotes[0];
        public bool[] RematchVotes=>IsHost?consent.Capture():new[]{PeerRematchRequested,guestVote};
        public bool CanRematch=>Playing&&IsOnline&&(IsHost?controller.Match.Winner.HasValue:!string.IsNullOrEmpty(RemoteState?.winner));
        public string RematchStatus=>!Playing?"연결 복구 후 다시 요청하세요.":OwnRematchRequested?"상대의 재경기 동의를 기다리고 있습니다.":PeerRematchRequested?"상대가 재경기를 요청했습니다.":"두 선수가 동의하면 같은 방에서 다시 시작합니다.";
        public bool RequestRematch()
        {
            if(!CanRematch)return false;
            if(IsHost){consent.Vote(CourtSide.Near,matchId,!consent.Host,true);TryBeginRematch();}
            else
            {
                guestVote=!guestVote;if(++inputSequence==0)inputSequence=1;
                SendGame(new GameMessage{type="input",epoch=epoch,inputSeq=inputSequence,playerId="guest",payload=new GamePayload{matchId=RemoteState.matchId,kind="rematch",choice=guestVote?1:0,eventId=inputSequence,rallyId=RemoteState.rallyId,shotId=RemoteState.shotId,power01=0,aimX=0,aimZ=0}});
                SentInputs++;
            }
            return true;
        }
        private void TryBeginRematch()
        {
            if(!IsHost||!CanRematch||!consent.Consume())return;
            matchId=Guid.NewGuid().ToString("N");consent.Reset(matchId);ResultWinner=null;RematchesStarted++;
            controller.ResetMatch();snapshotAt=0;Status="같은 방에서 새 경기를 시작합니다.";
        }

        private void Start()
        {
            controller = GetComponent<TennisMatchController>(); input = GetComponent<PlayerInputAdapter>();
            if (!Application.isEditor) gameObject.AddComponent<OnlineValidationDriver>();
            if(!Application.isEditor&&FrontEndController.HasArgument("-dt-motion-verify"))gameObject.AddComponent<MotionValidationDriver>();
            if (!Application.isEditor && FrontEndController.HasArgument("-dt-balance-survey")) gameObject.AddComponent<BalanceSurveyDriver>();
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
            connectionSettings=OnlineConnectionSettings.Load();forceRelay=connectionSettings.forceRelay||FrontEndController.HasArgument("-dt-force-relay")||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null;DirectSent=DirectReceived=PeerFallbacks=0;TransportHistory.Clear();
            matchId = Guid.NewGuid().ToString("N"); epoch = tick = inputSequence = lastInput = 0; hasInput = false;
            SentSnapshots = SentInputs = ProcessedInputs = ProtocolErrors = MaxRallyHits = CompletedPoints = NextServes = 0;
            TransientPeerPauses=0;
            LastProtocolError = null;
            PingMs=-1;ResultWinner = null; lastServeRally = 0;consent.Reset(matchId);guestVote=observedMatch=false;RematchesStarted=0;
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
            peer?.Close();peerFailureSent=false;
            if (socket != null)
            {
                // Drain the explicit leave request before the close handshake; unexpected loss uses disconnect grace.
                if (socket.Connected && InRoom) Send("room.leave", new RoomRequest());
                socket.CloseGracefully(); socket = null;
            }
            consent.ClearVotes();guestVote=false;
            Playing = false; RoomCode = null; token = null; resumeAllowed = false; closed = true;
            snapshots.Clear();
            controller?.Effects?.BeginRemoteEpoch();
            if (input != null) { input.OnlineRoute = null; input.InputEnabled = true; input.Typing = false; }
            if (controller != null && returnToSinglePlayer)
            {
                controller.Ball.IsAuthoritative = true; controller.IsPaused = false; controller.Configure(GameMode.SinglePlayer, controller.AiDifficulty);
            }
            Status = "Create a room or enter a six-character code.";
        }
        private void OnDestroy() { peer?.Close();socket?.Dispose(); }
        private bool Send(string type, RoomRequest payload)
        {
            if (socket == null) return false;
            var message = new WireEnvelope<RoomRequest> { type = type, requestId = "u" + ++requestSequence, payload = payload };
            if (socket.Send(JsonUtility.ToJson(message))) return true;
            Pause("Network send queue unavailable."); return false;
        }
        private void Pause(string reason)
        {
            consent.ClearVotes();guestVote=false;
            controller.Effects?.ClearVisuals();
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
                        Pause("Connection lost. Waiting to reconnect...");peer?.Close();
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
            if(peer!=null)for(int i=0;i<64&&peer.TryRead(out var direct);i++)
            {
                if(!Playing||TransportMode!="p2p")continue;
                try{var message=JsonUtility.FromJson<GameMessage>(direct);if(message==null||message.v!=1||message.epoch!=epoch)continue;DirectReceived++;ReceiveGame(IsHost?"guest":"host",message,now);}
                catch(Exception e) when(e is ArgumentException||e is FormatException||e is OverflowException){ProtocolErrors++;LastProtocolError="INVALID_DIRECT_MESSAGE";PeerFailed();}
            }
            if(InRoom&&!Playing&&negotiation.Epoch!=0&&!negotiation.Committed)ProposeTransport(now);
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
                if (controller.Match.Winner.HasValue && ResultWinner == null)
                {
                    ResultWinner = state.winner;
                    SendGame(new GameMessage { type = "result", epoch = epoch, payload = new GamePayload { matchId = matchId, winner = ResultWinner } });
                    Debug.Log($"ONLINE_RESULT host winner={ResultWinner} {state.score.Summary}");
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
                // A snapshot already in flight may reach the server after peer.disconnected.
                // This is the same recoverable transport state, not malformed game data.
                if(error.code=="PEER_UNAVAILABLE"&&InRoom&&!resuming)
                {TransientPeerPauses++;Pause("Peer unavailable. Match paused for reconnect.");return;}
                ProtocolErrors++; LastProtocolError = error.code; Status = "Server: " + error.code;
                if (resuming) { resumeAllowed = false; closed = true; Pause(Status); }
                return;
            }
            var p = JsonUtility.FromJson<WireEnvelope<ServerMessagePayload>>(json).payload;
            switch (header.type)
            {
                case "heartbeat.ack":
                    double elapsed=now*1000-p.clientTimeMs;if(p.clientTimeMs>0&&!double.IsNaN(elapsed)&&!double.IsInfinity(elapsed)&&elapsed>=0&&elapsed<=30000)PingMs=(float)elapsed;
                    break;
                case "room.created": case "room.joined": case "session.resumed":
                    if (p.playerId != (IsHost ? "host" : "guest")) throw new ArgumentException("Role");
                    RoomCode = p.code; enteredCode = p.code; token = p.reconnectToken; resumeAllowed = false;
                    Status = IsHost ? "Room " + RoomCode + " - waiting for guest" : "Joined " + RoomCode + " - agreeing relay transport";
                    Debug.Log($"ONLINE_ROOM role={(IsHost ? "host" : "guest")} code={RoomCode}");
                    break;
                case "room.ready":
                    if(p.epoch==uint.MaxValue)throw new ArgumentException("Transport lifetime exhausted");
                    if(p.preferRelay){PeerFallbacks++;forceRelay=true;}
                    acknowledgedMode=null;
                    Pause("Both players connected. Trying direct transport...");
                    peer?.Close();peerFailureSent=false;
                    negotiation.Begin(IsHost,p.epoch,now,forceRelay||p.preferRelay);
                    if(!forceRelay&&!p.preferRelay)
                    {
                        if(peer==null)peer=gameObject.AddComponent<OnlinePeerTransport>();
                        uint attempt=negotiation.Epoch;
                        peer.Begin(IsHost,(kind,data)=>{if(negotiation.Epoch==attempt&&!negotiation.Committed)Send("peer.signal",new RoomRequest{epoch=attempt,kind=kind,data=data});},
                            ()=>{if(negotiation.Epoch!=attempt||negotiation.LocalReady)return;negotiation.LocalReady=true;Send("peer.ready",new RoomRequest{epoch=attempt});},PeerFailed,connectionSettings.stunUrls);
                    }
                    ProposeTransport(now);
                    break;
                case "peer.signal":
                    if(p.epoch==negotiation.Epoch&&!negotiation.Committed&&p.from==(IsHost?"guest":"host"))peer?.Signal(p.kind,p.data);
                    break;
                case "peer.ready":
                    if(p.epoch==negotiation.Epoch&&!negotiation.Committed&&p.from==(IsHost?"guest":"host"))negotiation.PeerReady=true;
                    ProposeTransport(now);
                    break;
                case "transport.propose":
                    if(!IsHost&&negotiation.CanAcknowledge(p.mode,p.epoch)){acknowledgedMode=p.mode;Send("transport.ack",new RoomRequest{mode=p.mode,epoch=p.epoch});}
                    break;
                case "transport.ack":
                    if(IsHost&&negotiation.Proposed==p.mode&&negotiation.CanAcknowledge(p.mode,p.epoch))Send("transport.commit",new RoomRequest{mode=p.mode,epoch=p.epoch});
                    break;
                case "transport.committed":
                    if(!negotiation.Commit(p.mode,p.epoch))throw new ArgumentException("Transport epoch/readiness");
                    epoch=p.epoch;TransportMode=p.mode;TransportHistory.Add(p.mode+":"+epoch);hasInput=false;snapshots.Clear();
                    if(p.mode=="relay")peer?.Close();
                    controller.Effects?.BeginRemoteEpoch();controller.IsPaused=false;Playing=true;input.InputEnabled=IsHost;resuming=false;resumeAllowed=false;
                    Status="Online - connected via "+p.mode+". You are at the bottom of the court.";snapshotAt=0;
                    Debug.Log($"ONLINE_PLAY role={(IsHost ? "host" : "guest")} mode={TransportMode} epoch={epoch}");
                    break;
                case "relay.message":
                    if(Playing&&TransportMode=="relay"&&p.epoch==epoch&&p.message!=null&&p.message.epoch==epoch)ReceiveGame(p.from,p.message,now);
                    break;
                case "peer.disconnected": Pause("Peer disconnected. Match paused for reconnect (15 seconds)."); break;
                case "peer.left": case "room.closed": case "room.expired":
                    closed = true; resumeAllowed = false; token = null; Pause("Room closed. Leave and create a new room."); break;
            }
        }
        private void Observe(GamePayload state)
        {
            MaxRallyHits = Math.Max(MaxRallyHits, state.hits);
            CompletedPoints = Math.Max(CompletedPoints, checked((int)(state.score.totalPoints[0] + state.score.totalPoints[1])));
            if (state.rallyId != lastServeRally)
            { lastServeRally = state.rallyId; NextServes++; }
        }
        private void ProposeTransport(double now)
        {
            string mode=negotiation.Decide(now);if(mode!=null)Send("transport.propose",new RoomRequest{mode=mode,epoch=negotiation.Epoch});
        }
        private void PeerFailed()
        {
            negotiation.LocalReady=false;
            if(Playing&&TransportMode=="p2p"&&!peerFailureSent){peerFailureSent=true;Pause("Direct connection lost. Agreeing relay...");Send("peer.failed",new RoomRequest{epoch=epoch});}
            else if(!negotiation.Committed&&!peerFailureSent&&(negotiation.Proposed=="p2p"||acknowledgedMode=="p2p")){peerFailureSent=true;Send("peer.failed",new RoomRequest{epoch=negotiation.Epoch});}
        }
        public bool DropPeerForValidation()
        {
            if(!Debug.isDebugBuild||!FrontEndController.HasArgument("-dt-peer-failure")||!Playing||TransportMode!="p2p"||peer==null)return false;
            PeerFailed();peer.Close();return true;
        }
        private void ReceiveGame(string from,GameMessage message,double now)
        {
            if(message==null||message.v!=1||message.epoch!=epoch)return;
            if(IsHost&&from=="guest"&&message.type=="input")ProcessRemoteInput(message,now);
            else if(!IsHost&&from=="host")
            {
                if(message.type=="snapshot"&&snapshots.Push(message,now))
                {
                    if(observedMatch&&matchId!=message.payload.matchId){RematchesStarted++;guestVote=false;ResultWinner=null;input.ClearFeedback();controller.Effects?.BeginRemoteEpoch();}
                    matchId=message.payload.matchId;observedMatch=true;input.InputEnabled=true;Observe(message.payload);
                    if(message.payload.phase=="MatchEnd"&&!string.IsNullOrEmpty(message.payload.winner))ResultWinner=message.payload.winner;
                }
                else if(message.type=="result"&&RemoteState?.phase=="MatchEnd"&&message.payload?.matchId==RemoteState?.matchId&&message.payload.winner==RemoteState.winner){ResultWinner=message.payload.winner;Debug.Log("ONLINE_RESULT guest winner="+ResultWinner);}
                else if(message.type=="pause"&&message.payload!=null)Pause(message.payload.reason);
            }
        }
        private void SendGame(GameMessage message)
        {
            if(TransportMode=="p2p")
            {
                if(Playing&&peer!=null&&peer.Send(JsonUtility.ToJson(message)))DirectSent++;else PeerFailed();
            }
            else Send("game.relay",new RoomRequest{epoch=epoch,message=message});
        }
        private SwingResult? RouteInput(PlayerInputSample sample, uint session)
        {
            if (sample.Action == PlayerAction.Characters) { controller.Characters.ToggleMenu(); return null; }
            if (!Playing || sample.PracticeOpponent) return null;
            if (IsHost)
            {
                if (sample.Action == PlayerAction.Restart){RequestRematch();return null;}
                return controller.ApplyPlayerInput(sample, session, CourtSide.Near);
            }
            else if (RemoteState != null && InputKind(sample.Action) != null)
            {
                if (++inputSequence == 0) inputSequence = 1;
                SendGame(new GameMessage { type = "input", epoch = epoch, inputSeq = inputSequence, playerId = "guest",
                    payload = new GamePayload { matchId=RemoteState.matchId,eventId = inputSequence, kind = InputKind(sample.Action), choice = sample.Choice,
                        power01 = sample.Power01, aimX = sample.AimX, aimZ = sample.AimZ, rallyId = RemoteState.rallyId, shotId = RemoteState.shotId } });
                SentInputs++;
                input.MarkInputSent();
            }
            return null;
        }
        private void ProcessRemoteInput(GameMessage message, double now)
        {
            if (message.playerId != "guest" || message.inputSeq==0 || (hasInput && !Sequence32.IsNewer(message.inputSeq, lastInput))) return;
            var p = message.payload;
            if(p==null||p.eventId==0||p.matchId!=matchId)return;
            if(p.kind=="rematch")
            {
                if(p.choice!=0&&p.choice!=1)return;
                hasInput=true;lastInput=message.inputSeq;ProcessedInputs++;
                if(consent.Vote(CourtSide.Far,p.matchId,p.choice==1,controller.Match.Winner.HasValue))TryBeginRematch();
                return;
            }
            if(!TryAction(p.kind,out var action))return;
            // Host receipt time is local. Client clocks and IMU timestamps never decide a hit.
            var sample = new PlayerInputSample(action, p.eventId, now, p.power01, p.aimX, p.aimZ, false, p.choice);
            if (action != PlayerAction.SelectCharacter && (p.rallyId != controller.Match.RallyId || p.shotId != controller.Match.CurrentShot.ShotId)) return;
            if(!sample.IsValid)return;
            hasInput=true;lastInput=message.inputSeq;ProcessedInputs++;
            controller.ApplyPlayerInput(sample, unchecked(0x80000000u + epoch), CourtSide.Far, p.rallyId, p.shotId);
        }
        private static string InputKind(PlayerAction action)
        {
            switch (action)
            {
                case PlayerAction.Forehand: return "forehand"; case PlayerAction.Backhand: return "backhand";
                case PlayerAction.Smash: return "smash"; case PlayerAction.Volley: return "volley"; case PlayerAction.Slice: return "slice";
                case PlayerAction.Lob: return "lob"; case PlayerAction.Skill: return "skill"; case PlayerAction.SelectCharacter: return "character";
                case PlayerAction.Serve:return "serve";case PlayerAction.FaultServe:return "faultServe";
                case PlayerAction.LetServe:return "letServe";case PlayerAction.NetServe:return "netServe";
                default: return null;
            }
        }
        private static bool TryAction(string kind, out PlayerAction action)
        {
            foreach (PlayerAction candidate in new[] { PlayerAction.Forehand, PlayerAction.Backhand, PlayerAction.Smash, PlayerAction.Volley,
                PlayerAction.Slice, PlayerAction.Lob, PlayerAction.Skill, PlayerAction.SelectCharacter,
                PlayerAction.Serve,PlayerAction.FaultServe,PlayerAction.LetServe,PlayerAction.NetServe })
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
            GUI.Label(new Rect(x + 12, y + 211, 336, 40), $"Input: {input.SourceName} | Space: your serve turn\nQ/W: racket | N: net fault / ball boy");
            input.Typing = GUI.GetNameOfFocusedControl() == "online-address" || GUI.GetNameOfFocusedControl() == "online-code";
        }
        private void DrawOnlineMatch()
        {
            var s = IsHost ? GamePayload.Capture(controller, matchId, lastInput) : RemoteState;
            GUI.Box(new Rect(16, 16, 405, 345), "Digital Tennis - Online / WebSocket relay");
            if (s == null) { GUI.Label(new Rect(32, 48, 370, 30), "Waiting for the host snapshot..."); return; }
            CourtSide local = controller.View.LocalPlayer, opponent = controller.View.Opponent;
            GUI.Label(new Rect(32, 48, 370, 28), $"POINT {s.score.PointDisplay(s.points,(int)local)}:{s.score.PointDisplay(s.points,(int)opponent)} | GAME {s.score.games[(int)local]}:{s.score.games[(int)opponent]} | SET {s.score.sets[(int)local]}:{s.score.sets[(int)opponent]}");
            GUI.Label(new Rect(32, 80, 370, 24), $"{s.phase} | rally {s.rallyId} | hits {s.hits}");
            GUI.Label(new Rect(32, 108, 370, 30), s.phase == "MatchEnd" ? (s.winner == (IsHost ? "host" : "guest") ? "You win the match" : "Opponent wins the match") :
                s.officials != null && s.officials.recovery != 0 ? "Court paused - ball boy collecting" :
                s.phase == "WaitServe" ? $"Ready - serve {s.faultCount + 1}" : (s.lastHitter == (IsHost ? "host" : "guest") ? "You hit the ball" : "Opponent hit the ball"));
            GUI.Label(new Rect(32, 140, 370, 24), $"You: {CharacterCatalog.Ids[s.characters[(int)local].index]} | Opponent: {CharacterCatalog.Ids[s.characters[(int)opponent].index]} | Serve: {(IsHost ? "you" : "opponent")}");
            bool previous = GUI.enabled;
            GUI.enabled = previous && Playing && s.phase=="WaitServe" && s.officials.recovery==0 && s.serverId==(IsHost?"host":"guest");
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
