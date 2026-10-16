using System;
using System.Collections;
using DigitalTennis.AI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DigitalTennis
{
    public sealed class FrontEndController : MonoBehaviour
    {
        public static FrontEndController Active { get; private set; }
        public static bool HideLegacyHud => Active != null;
        public FrontEndModel Model { get; } = new FrontEndModel();
        public FrontEndView View { get; private set; }
        public FrontEndPortraits Portraits { get; private set; }
        public TennisMatchController Match { get; private set; }
        public OnlineTennisSession Session { get; private set; }
        public Camera UiCamera { get; private set; }
        public bool Ready { get; private set; }
        public bool Connecting { get; private set; }
        public bool ReduceMotion { get; private set; }
        public Difficulty Difficulty { get; private set; } = Difficulty.Medium;
        public int MenuIndex { get; private set; }
        public string ServerAddress = "ws://127.0.0.1:8080/ws", JoinCode = "";
        public CourtSide? Winner { get; private set; }
        public string FinalScore { get; private set; }
        public int Transitions { get; private set; }
        private float age;
        private FrontEndStage rendered;
        private bool onlineChoiceSent;
        private RenderTexture batchUiTarget;
        public string LobbyStatus => Session == null ? "연결을 준비하고 있습니다." :
            Session.LastProtocolError != null || Session.Status.StartsWith("Enter") ? "연결 주소와 방 코드를 확인한 후 다시 시도해 주세요." :
            Session.Playing ? "선수를 확인하고 있습니다…" : Session.InRoom ? (Session.IsHost?"친구의 입장을 기다리는 중입니다.":"상대와 연결하고 있습니다.") :
            Connecting ? "서버에 연결하고 있습니다…\n뒤로 가기를 누르면 연결을 취소합니다." : "같은 방 코드로 두 선수가 만납니다.";
        public static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(),name)>=0;
        private void Awake() { Active=this; }
        private IEnumerator Start()
        {
            // Existing independent physics/network/art verification remains available without the user-facing menu.
            if(HasArgument("-dt-roster-preview") || (HasArgument("-dt-stadium-verify")&&!HasArgument("-dt-stadium-front")) || (HasArgument("-dt-online-role")&&!HasArgument("-dt-menu-online")))
            {SceneManager.LoadScene("SinglePlayerCourt");yield break;}
            UiCamera=GetComponentInChildren<Camera>();ReduceMotion=PlayerPrefs.GetInt("DigitalTennis.ReduceMotion",0)!=0;
            if(Application.isBatchMode && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
            {batchUiTarget=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);batchUiTarget.Create();UiCamera.targetTexture=batchUiTarget;}
            View=new FrontEndView(this,UiCamera);rendered=Model.Stage;View.Render();
            if(EventSystem.current==null)
            {var events=new GameObject("Front End Event System",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(transform,false);events.GetComponent<EventSystem>().sendNavigationEvents=false;}
            if(HasArgument("-dt-frontend-verify")||HasArgument("-dt-input-observe")) gameObject.AddComponent<FrontEndValidationDriver>();
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt",LoadSceneMode.Additive);yield return null;
            Match=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();Session=Match.GetComponent<OnlineTennisSession>();
            Match.AutoServe=Match.DemoReturns=false;Match.IsPaused=true;Match.InputAdapter.GameplayEnabled=false;
            Match.InputAdapter.MenuRoute=RouteMenu;
            Portraits=new FrontEndPortraits(Match);Ready=true;age=0;Refresh();
        }
        private void Update()
        {
            View?.Resize();if(!Ready)return;
            age+=Time.unscaledDeltaTime;
            View.Animate(age,ReduceMotion);
            Portraits.Tick(!ReduceMotion);View.UpdateLive();
            RenderUiNow();
            if(Model.Stage==FrontEndStage.Logo && age>=1.9f) Confirm();
            if(Model.Stage==FrontEndStage.Versus && age>=2.2f)
            {Model.StartPlay();if(Model.Mode!=GameMode.Online)Match.IsPaused=false;Refresh();}
            if(Model.Stage==FrontEndStage.Lobby && Session.Playing) AdvanceOnline();
            if(Model.Stage==FrontEndStage.Lobby && Connecting && !Session.InRoom &&
                (Session.LastProtocolError!=null || Session.Status.StartsWith("Connection lost"))) {Connecting=false;Refresh();}
            Match.InputAdapter.GameplayEnabled=Model.Stage==FrontEndStage.Playing;
            if(Model.Stage==FrontEndStage.Playing)
            {
                if(Model.Mode==GameMode.Online && !Session.IsHost)
                {var r=Session.PresentedState;if(r?.winner=="host"||r?.winner=="guest") {Winner=r.winner=="host"?CourtSide.Near:CourtSide.Far;FinalScore=r.score.Summary;ShowResults();}}
                else if(Match.Match.Winner.HasValue) {Winner=Match.Match.Winner;FinalScore=Match.Match.Progress.Summary;ShowResults();}
            }
        }
        private bool RouteMenu(PlayerInputSample sample)
        {
            var action=sample.Action;
            if(action==PlayerAction.ReloadBalance&&Model.Stage!=FrontEndStage.Playing)
            {ReloadEffects();return true;}
            var focused=EventSystem.current?.currentSelectedGameObject;
            if(focused!=null && focused.TryGetComponent<UnityEngine.UI.InputField>(out var input) && input.isFocused)
            {if(action==PlayerAction.MenuBack)EventSystem.current.SetSelectedGameObject(null);return true;}
            if(action==PlayerAction.MenuBack) {if(Model.Stage==FrontEndStage.Playing)Pause();else Back();return true;}
            if(action==PlayerAction.MenuConfirm || (action==PlayerAction.Serve&&Model.Stage!=FrontEndStage.Playing))
            {if(Model.Stage==FrontEndStage.Menu)MenuChoice(MenuIndex);else if(Model.Stage!=FrontEndStage.Playing)Confirm();return true;}
            int delta=action==PlayerAction.MenuLeft?-1:action==PlayerAction.MenuRight?1:action==PlayerAction.MenuUp?-8:action==PlayerAction.MenuDown?8:0;
            if(Model.Selecting&&delta!=0) {Model.Move(delta);Refresh();return true;}
            if(Model.Stage==FrontEndStage.Menu && (action==PlayerAction.MenuUp||action==PlayerAction.MenuDown))
            {MenuIndex=(MenuIndex+(action==PlayerAction.MenuUp?4:1))%5;Refresh();return true;}
            return delta!=0;
        }
        private void AdvanceOnline()
        {
            if(!onlineChoiceSent && Match.InputAdapter.InputEnabled)
            {
                Match.InputAdapter.Press(PlayerAction.SelectCharacter,false,Model.Player);
                if(Session.IsHost)Match.InputAdapter.Press(PlayerAction.SelectCourt,false,Model.Court);
                onlineChoiceSent=true;
            }
            bool ready=Session.IsHost?Session.ProcessedInputs>0:Session.RemoteState?.characters[1].index==Model.Player;
            if(!onlineChoiceSent || !ready)return;
            var state=Session.IsHost?GamePayload.Capture(Match,"menu",0):Session.RemoteState;
            if(state==null)return;
            if(Model.OnlineVersus(state.characters[0].index,state.characters[1].index,state.courtIndex)) Refresh();
        }
        public CharacterBalance Balance(int index) => Match?.Characters?.Store[index];
        public void RenderUiNow()
        {
            if(batchUiTarget==null)return;
            Portraits?.RenderVisible();View.Resize();Canvas.ForceUpdateCanvases();UiCamera.Render();
        }
        public void Confirm()
        {
            if(Model.Stage==FrontEndStage.Pause) {if(Model.Mode!=GameMode.Online)Match.IsPaused=false;Model.Confirm();Refresh();return;}
            if(!Ready && Model.Stage!=FrontEndStage.Logo && Model.Stage!=FrontEndStage.Title)return;
            if(Model.Confirm()) {if(Model.Stage==FrontEndStage.Versus)PrepareOffline();Refresh();}
        }
        public void Pick(int index) {if(Model.Pick(index))Refresh();}
        public void MenuChoice(int choice)
        {
            if(!Ready || Model.Stage!=FrontEndStage.Menu)return;
            MenuIndex=choice;
            if(choice==0)Model.ChooseMode(GameMode.SinglePlayer);
            else if(choice==1)Model.ChooseMode(GameMode.Online);
            else if(choice==2)Model.ChooseMode(GameMode.Practice);
            else if(choice==3)Model.Options();
            else if(choice==4)Model.Quit();
            Refresh();
        }
        public void SetDifficulty(int level) {if(level<0||level>2)return;Difficulty=(Difficulty)level;Refresh();}
        public void Back()
        {
            if(Model.Stage==FrontEndStage.Lobby)ResetConnection();
            bool wasPause=Model.Stage==FrontEndStage.Pause;
            if(Model.Back()) {if(wasPause&&Model.Mode!=GameMode.Online)Match.IsPaused=false;Refresh();}
        }
        public void Pause()
        {if(Model.Pause()) {if(Model.Mode!=GameMode.Online)Match.IsPaused=true;Refresh();}}
        private void ShowResults()
        {if(Model.Results()) {if(Model.Mode!=GameMode.Online)Match.IsPaused=true;Refresh();}}
        public void ReturnToMenu()
        {ResetConnection();Winner=null;Model.Menu();Refresh();}
        public void Rematch() {if(Model.Rematch()) {PrepareOffline();Refresh();}}
        public void Controls() {Model.Controls();Refresh();}
        public void ToggleMotion()
        {ReduceMotion=!ReduceMotion;PlayerPrefs.SetInt("DigitalTennis.ReduceMotion",ReduceMotion?1:0);PlayerPrefs.Save();Refresh();}
        public void SetEffects(int quality) {Match.Effects.SetQuality((EffectQuality)quality);Refresh();}
        public void ReloadEffects() {Match.Effects.Reload();Match.Characters.Reload();if(Model.Stage==FrontEndStage.Options)Refresh();}
        public void EditLobbyCourt() {if(!Session.InRoom&&!Connecting&&Model.EditLobbyCourt())Refresh();}
        public void CreateRoom() => Connect(true);
        public void JoinRoom() => Connect(false);
        private void Connect(bool host)
        {
            if(Model.Stage!=FrontEndStage.Lobby||Connecting||Session.InRoom)return;
            Match.IsPaused=false;Match.Characters.Select(CourtSide.Near,Model.Player);Match.Characters.SelectCourt(Model.Court);
            Connecting=true;onlineChoiceSent=false;
            if(host)Session.CreateRoom(ServerAddress);else Session.JoinRoom(ServerAddress,JoinCode);
            if(!Session.IsOnline) {Connecting=false;Match.IsPaused=true;}
            Refresh();
        }
        private void ResetConnection()
        {
            if(!Ready)return;
            Session.Leave(false);Match.Ball.IsAuthoritative=true;Match.IsPaused=false;Match.Configure(GameMode.SinglePlayer,Difficulty);
            Match.AutoServe=Match.DemoReturns=false;Match.IsPaused=true;Match.InputAdapter.GameplayEnabled=false;
            Connecting=onlineChoiceSent=false;
        }
        private void PrepareOffline()
        {
            Match.Ball.IsAuthoritative=true;Match.IsPaused=false;Match.Configure(Model.Mode,Difficulty);Match.AutoServe=Match.DemoReturns=false;
            Match.Characters.Select(CourtSide.Near,Model.Player);Match.Characters.Select(CourtSide.Far,Model.Opponent);Match.Characters.SelectCourt(Model.Court);
            Match.InputAdapter.GameplayEnabled=false;Match.IsPaused=true;Winner=null;FinalScore=null;
        }
        private void Refresh()
        {
            if(Model.Stage!=rendered) {Transitions++;age=0;rendered=Model.Stage;}
            Portraits?.Hide();View.Render();View.Resize();
            if(Ready)Match.InputAdapter.GameplayEnabled=Model.Stage==FrontEndStage.Playing;
        }
        private void OnDestroy()
        {if(Active==this)Active=null;Portraits?.Dispose();if(batchUiTarget!=null){UiCamera.targetTexture=null;batchUiTarget.Release();Destroy(batchUiTarget);}}
    }

    public sealed class FrontEndPortraits : IDisposable
    {
        private readonly TennisMatchController match;
        private readonly Camera[] cameras=new Camera[2];
        private readonly GameObject[] models=new GameObject[2];
        private readonly RenderTexture[] targets=new RenderTexture[2];
        private readonly int[] indices={-1,-1};
        private readonly bool hasGpu=SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
        public FrontEndPortraits(TennisMatchController match)
        {
            this.match=match;match.View.Camera.cullingMask&=~((1<<30)|(1<<5));
            for(int i=0;i<2;i++)
            {
                var center=new Vector3(1000+i*20,1,0);
                models[i]=UnityEngine.Object.Instantiate(match.NearAvatar.gameObject,new Vector3(center.x,.85f,0),Quaternion.Euler(0,160,0));models[i].name="Menu Preview Only "+i;
                foreach(var t in models[i].GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                var obj=new GameObject("Portrait Camera "+i);cameras[i]=obj.AddComponent<Camera>();cameras[i].cullingMask=1<<30;cameras[i].clearFlags=CameraClearFlags.SolidColor;cameras[i].backgroundColor=Color.clear;
                cameras[i].transform.position=center+new Vector3(2.5f,.6f,-4.4f);cameras[i].transform.LookAt(center);cameras[i].fieldOfView=29;cameras[i].nearClipPlane=.1f;cameras[i].farClipPlane=12;
                if(hasGpu) {targets[i]=new RenderTexture(620,680,24,RenderTextureFormat.ARGB32);targets[i].Create();cameras[i].targetTexture=targets[i];}
                cameras[i].depth=-10;
            }
            Hide();
        }
        public RenderTexture Texture(int slot) => targets[slot];
        public void Show(int slot,int index)
        {
            models[slot].SetActive(true);cameras[slot].enabled=hasGpu;
            if(indices[slot]!=index) {indices[slot]=index;models[slot].GetComponentInChildren<CharacterPresentation>().Present(new CharacterAbility(index,match.Characters.Store[index]).Capture());}
        }
        public void Hide() {for(int i=0;i<2;i++) {models[i].SetActive(false);cameras[i].enabled=false;}}
        public void Tick(bool motion) {for(int i=0;i<2;i++)if(models[i].activeSelf)models[i].transform.rotation=Quaternion.Euler(0,160+(motion?Mathf.Sin(Time.unscaledTime*.55f)*8:0),0);}
        public void RenderVisible() {if(hasGpu)for(int i=0;i<2;i++)if(cameras[i].enabled)cameras[i].Render();}
        public void Dispose()
        {
            for(int i=0;i<2;i++) {if(models[i]!=null)UnityEngine.Object.Destroy(models[i]);if(cameras[i]!=null){cameras[i].targetTexture=null;UnityEngine.Object.Destroy(cameras[i].gameObject);}if(targets[i]!=null){targets[i].Release();UnityEngine.Object.Destroy(targets[i]);}}
        }
    }
}
