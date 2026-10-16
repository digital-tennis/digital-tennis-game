using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DigitalTennis
{
    /// <summary>Camera-rendered production flow UI; no match rules or keyboard simulation.</summary>
    public sealed class FrontEndView
    {
        public readonly Dictionary<string,Button> Buttons = new Dictionary<string,Button>();
        public readonly Dictionary<string,Text> Texts = new Dictionary<string,Text>();
        public InputField Address, RoomCode;
        public readonly Dictionary<string,Slider> Sliders=new Dictionary<string,Slider>();
        public int ButtonActivations { get; private set; }
        public int PointerActivations { get; private set; }
        public int CallbackActivations { get; private set; }
        public string PointerProblem { get; private set; }
        public Canvas Canvas { get; private set; }
        private readonly FrontEndController flow;
        private readonly Font font;
        private readonly RectTransform frame;
        private GameObject page;
        private CanvasGroup pageGroup;
        private int storyPage = -1, storyAthlete = -1;
        private readonly Image screen;
        private RawImage opening;private CanvasGroup openingLogo;
        private static readonly Color Ink = C("#071520"), Panel = C("#102530"), Muted = C("#87A8B0"), Lime = C("#D8F879"), White = C("#EDF5EE");
        private static Color C(string hex) => CharacterProfile.ColorOf(hex);
        public FrontEndView(FrontEndController owner,Camera camera)
        {
            flow=owner;font=Resources.Load<Font>("FrontEnd/Fonts/NanumGothic-Regular");
            var root=new GameObject("Front End Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));root.transform.SetParent(owner.transform,false);root.layer=5;
            Canvas=root.GetComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceCamera;Canvas.worldCamera=camera;Canvas.planeDistance=1;Canvas.sortingOrder=100;
            screen=Image(root.transform,"Menu Background",0,0,1600,900,Ink);
            var sr=screen.rectTransform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;
            var content=new GameObject("Safe 16 by 9 Frame",typeof(RectTransform));content.transform.SetParent(root.transform,false);content.layer=5;
            frame=content.GetComponent<RectTransform>();frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.sizeDelta=new Vector2(1600,900);
        }
        public bool RouteProfileInput(PlayerAction action)
        {
            if(!flow.Model.Selecting || storyPage<0) return false;
            if(action==PlayerAction.MenuBack) storyPage=-1;
            else if(action==PlayerAction.MenuLeft || action==PlayerAction.MenuUp) storyPage=(storyPage+2)%3;
            else if(action==PlayerAction.MenuRight || action==PlayerAction.MenuDown || action==PlayerAction.MenuConfirm || action==PlayerAction.Serve) storyPage=(storyPage+1)%3;
            else return true;
            flow.RefreshView();
            return true;
        }
        public void Resize() => frame.localScale=Vector3.one*Mathf.Min(flow.UiCamera.pixelWidth/1600f,flow.UiCamera.pixelHeight/900f);
        public void Animate(float age,bool reduced)
        {
            if(opening!=null){opening.texture=flow.Opening?.Texture;openingLogo.alpha=flow.Ready&&!reduced?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,1.25f,age)):1;
                Texts["opening.beat"].text=age<1.25f?"":OpeningSequence.Caption(age);Texts["opening.title"].text=age>10.6f?"DIGITAL\nTENNIS":"";}
            if(pageGroup!=null) pageGroup.alpha=reduced||flow.Model.Stage==FrontEndStage.Playing?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.2f)); }
        public bool Invoke(string id)
        {
            PointerProblem=null;
            if (!Buttons.TryGetValue(id,out var button) || !button.interactable || !button.gameObject.activeInHierarchy)
            {PointerProblem=id+": missing, disabled or inactive button";return false;}
            // Offscreen players have no physical window pointer; invoke the same Unity UI button callback.
            if(Application.isBatchMode || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
            {ButtonActivations++;CallbackActivations++;button.onClick.Invoke();return true;}
            if(EventSystem.current==null) {PointerProblem=id+": no EventSystem";return false;}
            flow.RenderUiNow();
            Canvas.ForceUpdateCanvases();
            var rect=button.GetComponent<RectTransform>();
            var pointer=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(flow.UiCamera,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=button.gameObject)
            {PointerProblem=$"{id}: point={pointer.position} screen={Screen.width}x{Screen.height} depth={button.targetGraphic.depth} hits={hits.Count} top={(hits.Count>0?hits[0].gameObject.name:"none")}";return false;}
            ButtonActivations++;PointerActivations++;ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);return true;
        }
        public void Render()
        {
            if (page!=null) { page.SetActive(false);UnityEngine.Object.Destroy(page); }
            Buttons.Clear();Texts.Clear();Sliders.Clear();Address=RoomCode=null;opening=null;openingLogo=null;
            page=new GameObject("Page "+flow.Model.Stage,typeof(RectTransform));page.transform.SetParent(frame,false);var pr=page.GetComponent<RectTransform>();pr.anchorMin=Vector2.zero;pr.anchorMax=Vector2.one;pr.offsetMin=pr.offsetMax=Vector2.zero;
            pageGroup=page.AddComponent<CanvasGroup>();
            var stage=flow.Model.Stage; if(!flow.Model.Selecting) storyPage=-1; screen.enabled=stage!=FrontEndStage.Playing;
            if (stage==FrontEndStage.Logo)
            {
                opening=new GameObject("Live Court Opening",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();Set(opening.rectTransform,page.transform,0,0,1600,900);opening.texture=flow.Opening?.Texture;opening.raycastTarget=false;
                Image(page.transform,"Cinema Top",0,0,1600,72,Color.black);Image(page.transform,"Cinema Bottom",0,828,1600,72,Color.black);
                Label("opening.beat","",65,740,1200,58,34,White);Label("opening.title","",70,160,1440,320,114,Lime).fontStyle=FontStyle.BoldAndItalic;
                Button("opening.skip","ENTER / ESC  ·  건너뛰기",1185,840,350,36,()=>flow.Confirm(),false,16);
                var logoRoot=new GameObject("Studio Card",typeof(RectTransform),typeof(CanvasGroup));Set(logoRoot.GetComponent<RectTransform>(),page.transform,0,0,1600,828);openingLogo=logoRoot.GetComponent<CanvasGroup>();openingLogo.blocksRaycasts=false;
                Image(logoRoot.transform,"Studio Black",0,0,1600,828,Ink);Logo(logoRoot.transform,665,200,270);
                var label=Label("studio","DIGITAL TENNIS",460,510,680,58,40,White,TextAnchor.MiddleCenter);label.transform.SetParent(logoRoot.transform,false);Set(label.rectTransform,logoRoot.transform,460,510,680,58);
                return;
            }
            if (stage==FrontEndStage.Playing) { Hud();return; }
            Graphic(page.transform,FrontGraphicKind.Diagonal,0,0,1600,900,new Color(.08f,.23f,.25f,.7f));
            Logo(page.transform,48,30,48);Label("brand","DIGITAL TENNIS",112,34,360,32,22,White);Label("edition","WORLD TOUR  /  15 NATIONS",112,66,430,24,12,Muted);
            Label("corner","PLAY BEYOND BORDERS",1150,47,400,24,14,Lime,TextAnchor.UpperRight);
            Image(page.transform,"Top Rule",48,112,1504,1,new Color(.3f,.55f,.57f,.35f));
            if(stage==FrontEndStage.Title || stage==FrontEndStage.Menu) Hero(stage==FrontEndStage.Menu);
            else if(flow.Model.Selecting) Selection();
            else if(stage==FrontEndStage.Versus) Versus();
            else if(stage==FrontEndStage.Lobby) Lobby();
            else if(stage==FrontEndStage.Pause) Pause();
            else if(stage==FrontEndStage.Results) Results();
            else if(stage==FrontEndStage.Options) Options();
            else if(stage==FrontEndStage.Controls) Controls();
            else if(stage==FrontEndStage.Audio) AudioOptions();
            else if(stage==FrontEndStage.Quit) Quit();
            Label("audio.caption","",200,812,1200,38,21,White,TextAnchor.MiddleCenter);
            Label("footer","ENTER / SPACE 확인     방향키 선택     ESC 뒤로",48,860,1000,24,14,Muted);
            Label("version","DIGITAL TENNIS  /  ORIGINAL WORLD ROSTER",1050,860,500,24,12,Muted,TextAnchor.UpperRight);
        }
        private void Hero(bool menu)
        {
            Portrait(0,850,140,670,670,flow.Model.Player);
            Label("kicker","THE WORLD IS YOUR COURT.",60,178,740,38,24,Lime);
            Label("digital","DIGITAL",48,224,890,146,118,White).fontStyle=FontStyle.BoldAndItalic;
            Label("tennis","TENNIS",48,340,890,156,138,Lime).fontStyle=FontStyle.BoldAndItalic;
            if(!menu)
            {
                Label("intro","15개 국가의 선수 · 나만의 플레이 · 하나의 코트",64,529,780,42,23,White);
                Button("continue","ENTER  ·  시작하기",64,628,480,72,()=>flow.Confirm(),true);
                Button("intro.replay","오프닝 다시 보기",64,790,360,44,()=>flow.ReplayOpening(),false,17);
                Label("ready",flow.Ready?"라켓을 들고, 세계로.":"선수와 코트를 준비하고 있습니다…",68,725,730,34,18,Muted);
            }
            else
            {
                string[] titles={"1인 플레이","온라인 대전","연습 경기","설정 / 조작 안내","게임 종료"};
                string[] ids={"single","online","practice","options","quit"};
                for(int i=0;i<5;i++) { int choice=i;Button("menu."+ids[i],$"0{i+1}    {titles[i]}",64,520+i*59,620,50,()=>flow.MenuChoice(choice),i==flow.MenuIndex); }
                Label("menu.note","국가를 선택하고, 당신의 플레이를 시작하세요.",840,790,650,30,20,Muted,TextAnchor.MiddleCenter);
            }
        }
        private void Selection()
        {
            var m=flow.Model;var p=CharacterCatalog.Get(m.Focus);var b=flow.Balance(m.Focus);
            string title=m.Stage==FrontEndStage.Player?"당신의 선수를 선택하세요":m.Stage==FrontEndStage.Opponent?"상대 선수를 선택하세요":"경기가 열릴 코트를 선택하세요";
            string step=m.Stage==FrontEndStage.Player?"01  PLAYER":m.Stage==FrontEndStage.Opponent?"02  OPPONENT":"03  COURT";
            Label("selection.step",step,48,126,350,30,16,Lime);
            Label("selection.title",title,412,127,1100,45,31,White);
            Image(page.transform,"Profile Panel",48,174,330,606,Panel);
            Portrait(0,63,186,300,278,m.Focus);
            Label("country",p.country+"   /   "+p.id,69,468,290,28,19,Lime);
            Label("name",p.localName,69,501,292,45,30,White);
            Label("persona",$"{p.name}  ·  {p.age}세",69,546,292,28,15,Muted);
            Label("skill",p.skillName,69,581,292,28,17,White);
            var radar=Graphic(page.transform,FrontGraphicKind.Radar,67,627,142,136,C(p.accent));radar.Ratings=b.Ratings;radar.SetVerticesDirty();
            string[] axes={"서브","스매시","발리","수비","체력","스피드"};
            for(int i=0;i<6;i++) Label("rating."+i,axes[i]+"  "+b.Ratings[i].ToString("0"),230,627+i*22,126,24,14,White);
            Button("profile.open","선수 이야기",310,802,300,48,()=>{storyPage=0;flow.RefreshView();});
            if(storyAthlete!=m.Focus) {storyAthlete=m.Focus;storyPage=-1;}
            if(storyPage>=0)
            {
                Image(page.transform,"Story Panel",412,184,1140,606,Panel);
                string[] titles={"출발과 목표","약점 · 성장 · 관계","목소리와 연출"};
                Label("profile.title",p.localName+"  /  "+titles[storyPage],442,204,1060,46,30,Lime);
                Label("profile.body",p.narrative.Page(storyPage),442,272,1060,420,23,White);
                for(int i=0;i<3;i++)
                {int selected=i;Button("profile.page."+i,titles[i],442+i*280,719,265,46,()=>{storyPage=selected;flow.RefreshView();},i==storyPage,18);}
                Button("profile.close","지도 보기",1290,719,230,46,()=>{storyPage=-1;flow.RefreshView();},false,18);
                Button("back","←  닫기",48,802,230,48,()=>{storyPage=-1;flow.RefreshView();});
                return;
            }
            Image(page.transform,"Map Panel",412,184,1140,355,new Color(.025f,.08f,.115f,1));
            Graphic(page.transform,FrontGraphicKind.World,432,192,1100,330,White);
            for(int i=0;i<15;i++)
            {
                int index=i;var at=WorldRoster.Position(i);float x=432+at.x*1100,y=192+(1-at.y)*330;
                var offset=WorldRoster.Callouts[i];float lx=Mathf.Clamp(x+offset.x,464,1496),ly=Mathf.Clamp(y+offset.y,210,510);
                var line=Graphic(page.transform,FrontGraphicKind.Connector,x,y,1,1,i==m.Focus?Lime:new Color(.4f,.65f,.6f,.65f));line.Endpoint=new Vector2(lx-x,-(ly-y));line.SetVerticesDirty();
                Image(page.transform,"City "+i,x-3,y-3,6,6,i==m.Focus?Lime:Muted);
                Button("map."+CharacterCatalog.Ids[i],CharacterCatalog.Ids[i],lx-27,ly-13,54,27,()=>flow.Pick(index),i==m.Focus,12);
            }
            Label("map.note","세계지도 · 도시 위치를 잇는 국가 마커",432,545,1000,23,13,Muted);
            for(int i=0;i<15;i++)
            {
                int index=i;var c=CharacterCatalog.Get(i);float x=412+i%8*143,y=580+i/8*84;
                Button("country."+c.id,c.id+"\n"+c.localName,x,y,132,72,()=>flow.Pick(index),i==m.Focus,15);
                Image(page.transform,"Roster Accent "+i,x,y,132,3,C(c.accent));
            }
            if(m.Stage==FrontEndStage.Opponent)
            {
                Label("difficulty","AI 난이도",840,761,190,30,19,Muted);
                string[] names={"하","중","상"};for(int i=0;i<3;i++) {int level=i;Button("difficulty."+i,names[i],1030+i*95,753,82,42,()=>flow.SetDifficulty(level),i==(int)flow.Difficulty,17);}
            }
            else Label("court.name",p.courtCode+"  /  "+p.courtName,412,756,1120,38,21,White);
            Button("back","←  뒤로",48,802,230,48,()=>flow.Back());
            Button("confirm",m.Stage==FrontEndStage.Court?"코트 확정  →":"선수 확정  →",1222,802,330,48,()=>flow.Confirm(),true);
        }
        private void Versus()
        {
            var a=CharacterCatalog.Get(flow.Model.Player);var b=CharacterCatalog.Get(flow.Model.Opponent);var court=CharacterCatalog.Get(flow.Model.Court);
            Portrait(0,70,160,560,530,flow.Model.Player);Portrait(1,970,160,560,530,flow.Model.Opponent);
            Label("versus","VS",650,295,300,170,118,Lime,TextAnchor.MiddleCenter).fontStyle=FontStyle.BoldAndItalic;
            Label("near",a.country+" / "+a.localName,60,680,600,60,37,White,TextAnchor.MiddleCenter);
            Label("far",b.country+" / "+b.localName,940,680,600,60,37,White,TextAnchor.MiddleCenter);
            Label("venue",court.courtName+"  ·  "+court.courtCode,400,775,800,45,24,Muted,TextAnchor.MiddleCenter);
            Label("loading","COURT READY  /  경기를 시작합니다",400,823,800,30,16,Lime,TextAnchor.MiddleCenter);
        }
        private void Lobby()
        {
            Label("lobby.title","온라인 대전",64,150,850,74,56,White);
            Label("lobby.subtitle","방을 만들거나 친구의 6자리 코드를 입력하세요.",68,241,900,40,23,Muted);
            Label("address.label","연결 주소",70,329,220,30,18,Muted);
            Address=Field("address",flow.ServerAddress,70,370,720,58);Address.onValueChanged.AddListener(v=>flow.ServerAddress=v);
            Label("code.label","방 코드",70,453,220,30,18,Muted);
            RoomCode=Field("room-code",flow.JoinCode,70,493,330,58);RoomCode.characterLimit=6;RoomCode.onValueChanged.AddListener(v=>flow.JoinCode=v);
            bool free=!flow.Session.InRoom && !flow.Connecting;
            Button("lobby.create","방 만들기",70,598,345,60,()=>flow.CreateRoom(),true).interactable=free;
            Button("lobby.join","코드로 입장",445,598,345,60,()=>flow.JoinRoom()).interactable=free;
            Button("lobby.court","코트 선택",70,681,345,48,()=>flow.EditLobbyCourt()).interactable=free;
            Button("back","←  뒤로 / 연결 종료",70,802,440,48,()=>flow.Back());
            Portrait(0,945,175,525,485,flow.Model.Player);
            Label("room","ROOM  "+(flow.Session.RoomCode??"------"),865,678,670,55,38,Lime,TextAnchor.MiddleCenter);
            Button("lobby.copy","방 코드 복사",1040,737,320,34,()=>flow.CopyRoomCode(),false,17).interactable=flow.Session.InRoom;
            Label("status",flow.LobbyStatus,865,778,670,58,19,White,TextAnchor.MiddleCenter);
            Address.interactable=RoomCode.interactable=free;
        }
        private void Hud()
        {
            var c=flow.Match;var s=flow.Session;CourtSide own=s.IsOnline?c.View.LocalPlayer:CourtSide.Near;
            var a=c.Characters.Player(own);var b=c.Characters.Player(TennisMatch.Opponent(own));
            Image(page.transform,"Score Backplate",32,18,640,112,new Color(.025f,.065f,.09f,.94f));
            Label("score","",48,27,608,42,26,White,TextAnchor.MiddleCenter);
            Label("sets","",48,72,608,25,17,White,TextAnchor.MiddleCenter);
            Label("phase","",48,101,608,23,14,Lime,TextAnchor.MiddleCenter);
            Image(page.transform,"Input Backplate",480,808,640,58,new Color(.025f,.065f,.09f,.92f));
            Label("input.hint","",492,812,616,25,17,Lime,TextAnchor.MiddleCenter);
            Label("input.feedback","",492,838,616,24,14,White,TextAnchor.MiddleCenter);
            Meter("you",a,48,735);Meter("opponent",b,1210,735);
            Button("pause","ESC  ·  메뉴",1360,24,192,42,()=>flow.Pause());
            Button("serve","SPACE  서브",640,700,320,44,()=>c.InputAdapter.Press(PlayerAction.Serve),true);
            Label("keys","Q / W  포핸드·백핸드     S  스매시     V  발리     X  슬라이스     B  로브     K  스킬",342,872,920,24,14,White,TextAnchor.MiddleCenter);
            Label("call","",375,650,850,55,26,Lime,TextAnchor.MiddleCenter);
            Label("audio.caption","",400,760,800,40,21,White,TextAnchor.MiddleCenter);
            UpdateLive();
        }
        private void Meter(string id,CharacterAbility ability,float x,float y)
        {
            Image(page.transform,id+" panel",x,y,342,122,new Color(.025f,.065f,.09f,.94f));
            Label(id+".name","",x+16,y+13,310,30,20,White);
            Label(id+".meter","",x+16,y+46,310,28,18,Lime);
            Label(id+".skill","",x+16,y+78,310,25,14,Muted);
            if(id=="you") Button("skill","K",x+266,y-49,76,40,()=>flow.Match.InputAdapter.Press(PlayerAction.Skill),true,18);
        }
        public void UpdateLive()
        {
            var speech=flow.Match?.GetComponent<TennisAudio>();
            if(Texts.TryGetValue("audio.caption",out var caption))caption.text=speech!=null&&speech.Captions?speech.Caption:"";
            if(flow.Model.Stage==FrontEndStage.Results&&flow.Model.Mode==GameMode.Online&&Texts.TryGetValue("result.rematch",out var rematchStatus))
            {rematchStatus.text=flow.Session.RematchStatus;if(Buttons.TryGetValue("rematch",out var button)){button.interactable=flow.Session.CanRematch;button.GetComponentInChildren<Text>().text=flow.Session.OwnRematchRequested?"재경기 요청 취소":"같은 방에서 재경기 요청";}}
            if(flow.Model.Stage==FrontEndStage.Audio&&Texts.TryGetValue("audio.status",out var audioStatus))audioStatus.text=flow.Match.GetComponent<TennisAudio>().Status;
            if(flow.Model.Stage==FrontEndStage.Lobby)
            {
                if(Texts.TryGetValue("status",out var st)) st.text=flow.LobbyStatus;
                if(Texts.TryGetValue("room",out var rm)) rm.text="ROOM  "+(flow.Session.RoomCode??"------");
                bool free=!flow.Session.InRoom&&!flow.Connecting;
                foreach(var key in new[]{"lobby.create","lobby.join","lobby.court"}) if(Buttons.TryGetValue(key,out var bt)) bt.interactable=free;
                if(Address!=null) Address.interactable=RoomCode.interactable=free;
            }
            if(flow.Model.Stage!=FrontEndStage.Playing || flow.Match==null) return;
            var c=flow.Match;var s=flow.Session;var remote=s.IsOnline&&!s.IsHost?s.PresentedState:null;
            var own=c.View.LocalPlayer;int i=(int)own,j=1-i;
            var a=c.Characters.Player(own);var b=c.Characters.Player((CourtSide)j);
            string da=remote!=null?remote.score.PointDisplay(remote.points,i):c.Match.Progress.Display(own);
            string db=remote!=null?remote.score.PointDisplay(remote.points,j):c.Match.Progress.Display((CourtSide)j);
            Texts["score"].text=$"{CharacterCatalog.Get(a.Index).localName}    {da}  :  {db}    {CharacterCatalog.Get(b.Index).localName}";
            var tally=remote?.score??MatchScoreState.Capture(c.Match.Progress);
            Texts["sets"].text=$"세트 {tally.sets[i]} : {tally.sets[j]}    ·    게임 {tally.games[i]} : {tally.games[j]}    ·    {tally.rules[0]/2+1}세트 선승"+(tally.tieBreak?$"    ·    TB {tally.rules[3]}점":"");
            bool ready=s.IsOnline&&!s.IsHost?remote?.phase=="WaitServe"&&remote.officials.recovery==0&&!c.Characters.Cinematic&&s.Playing:c.ReadyToServe;
            bool ownServer=remote!=null?remote.serverId==(own==CourtSide.Near?"host":"guest"):c.Match.Server==own;
            bool pointEnd=(remote?.phase??c.Match.Phase.ToString())=="PointEnd";
            Texts["phase"].text=c.Characters.Cinematic?"SPECIAL SKILL":pointEnd?
                (tally.setComplete?"세트 종료 · 다음 세트 준비":tally.gameComplete?"게임 종료 · 서버 교대 준비":"포인트 종료 · 다음 서브 준비"):
                ready?(ownServer?"내 서브":"상대 서브"):"RALLY  /  PLAY";
            foreach(var pair in new[]{("you",a),("opponent",b)})
            { var p=CharacterCatalog.Get(pair.Item2.Index);Texts[pair.Item1+".name"].text=p.country+" / "+p.localName;Texts[pair.Item1+".meter"].text=$"SKILL {pair.Item2.Meter.Stock}/3  ·  {pair.Item2.Meter.Gauge:0}%  ·  체력 {pair.Item2.Energy:0}";Texts[pair.Item1+".skill"].text=p.skillName; }
            Texts["you.meter"].color=Color.Lerp(Lime,White,c.Effects.ChargePulse(own));
            var opponentStroke=remote?.players[j]??PlayerState.Capture("opponent",j==0?c.NearAvatar:c.FarAvatar);
            if(opponentStroke.swingId!=0&&!opponentStroke.swingContact&&opponentStroke.swingAge<RacketAvatar.SwingDuration)Texts["opponent.skill"].text="상대 헛스윙";
            Texts["opponent.meter"].color=Color.Lerp(Lime,White,c.Effects.ChargePulse((CourtSide)j));
            Texts["call"].text=remote!=null?remote.officials.text:c.Match.CallText;
            if(c.Characters.Cinematic)
            {var active=c.Characters.Player(CourtSide.Near).Cinematic?c.Characters.Player(CourtSide.Near):c.Characters.Player(CourtSide.Far);Texts["call"].text=CharacterCatalog.Get(active.Index).skillName;}
            if(s.IsOnline&&!s.Playing) {Texts["phase"].text="연결 복구 중 · ESC로 메뉴";Texts["call"].text=s.FriendlyStatus??"상대와의 연결을 확인하고 있습니다.";}
            Buttons["serve"].gameObject.SetActive(ready&&ownServer);
            Buttons["skill"].interactable=c.InputAdapter.InputEnabled&&a.Meter.Stock>0&&!c.Characters.Cinematic&&a.Remaining==0;
            var audio=c.GetComponent<TennisAudio>();Texts["audio.caption"].text=audio!=null&&audio.Captions?audio.Caption:"";
            UpdateInputGuide(remote,ready,ownServer);
        }
        private void UpdateInputGuide(GamePayload remote,bool ready,bool ownServer)
        {
            var c=flow.Match;var s=flow.Session;
            bool recovering=remote!=null?remote.officials.recovery!=0:c.Recovery.Active;
            bool canHit=remote!=null?remote.hitReady:c.CanReturn(c.View.LocalPlayer);
            bool pointEnd=(remote?.phase??c.Match.Phase.ToString())=="PointEnd";
            string hint=c.Characters.Cinematic?"스킬 연출 중입니다":
                s.IsOnline&&!s.Playing?"연결 복구를 기다려 주세요":
                recovering?"볼보이가 공을 회수하고 있습니다":
                pointEnd?"점수를 반영하고 다음 서브를 준비합니다":
                ready?(!ownServer?"상대의 서브를 기다리세요":"Space를 눌러 서브하세요"):
                canHit?"지금 Q / W로 타격하세요 · 이동은 자동입니다":"공이 라켓 범위에 올 때 Q / W · 이동은 자동입니다";
            Texts["input.hint"].text=hint;
            Texts["input.hint"].color=canHit||ready?Lime:Muted;
            var input=c.InputAdapter;
            string action=ActionName(input.LastAction);
            string feedback="키를 누르면 입력 결과를 이곳에 표시합니다";
            if(action!=null)
            {
                if(input.LastActionSentOnline)feedback=action+" 입력 전송됨";
                else if(input.LastSwingResult.HasValue)
                {
                    string outcome;
                    switch(input.LastSwingResult.Value)
                    {
                        case SwingResult.Hit:outcome="타격 성공";break;
                        case SwingResult.OutOfReach:outcome="헛스윙 · 공이 라켓 범위 밖입니다";break;
                        case SwingResult.Height:outcome=input.LastAction==PlayerAction.Smash?"헛스윙 · 스매시는 높은 공에 사용하세요":"헛스윙 · 공의 높이가 맞지 않습니다";break;
                        case SwingResult.WrongSide:outcome="헛스윙 · 공이 내 코트로 오기를 기다리세요";break;
                        case SwingResult.WrongPhase:outcome="헛스윙 · 상대의 리턴을 기다리세요";break;
                        case SwingResult.NoBall:outcome=ownServer?"Space로 다음 서브를 시작하세요":"상대의 서브를 기다리세요";break;
                        default:outcome=ready?"Space로 먼저 서브하세요":"이번 타격은 반영되지 않았습니다";break;
                    }
                    feedback=action+" 입력됨 · "+outcome;
                }
                else feedback=action+" 입력됨";
            }
            Texts["input.feedback"].text=feedback;
            Texts["input.feedback"].color=input.LastSwingResult==SwingResult.Hit?Lime:White;
        }
        private static string ActionName(PlayerAction? action)
        {
            switch(action)
            {
                case PlayerAction.Serve:return "서브";case PlayerAction.Forehand:return "포핸드";
                case PlayerAction.Backhand:return "백핸드";case PlayerAction.Smash:return "스매시";
                case PlayerAction.Volley:return "발리";case PlayerAction.Slice:return "슬라이스";
                case PlayerAction.Lob:return "로브";case PlayerAction.Skill:return "스킬";
                default:return null;
            }
        }
        private void Pause()
        {
            Label("pause.title","잠시 쉬어가기",190,225,1220,90,64,White,TextAnchor.MiddleCenter);
            Label("pause.note",flow.Model.Mode==GameMode.Online?"온라인 경기는 계속 진행됩니다.":"경기가 일시 정지되었습니다.",300,345,1000,50,25,Muted,TextAnchor.MiddleCenter);
            Button("confirm","경기 계속하기",540,459,520,64,()=>flow.Confirm(),true);
            Button("controls","조작 안내",540,545,520,58,()=>flow.Controls());
            Button("menu","메인 메뉴로",540,625,520,58,()=>flow.ReturnToMenu());
        }
        private void Results()
        {
            bool near=flow.Winner==CourtSide.Near;int winner=near?flow.Match.Characters.Player(CourtSide.Near).Index:flow.Match.Characters.Player(CourtSide.Far).Index;
            Portrait(0,100,180,650,590,winner);
            Label("result.head","MATCH COMPLETE",820,227,680,70,44,Lime);
            Label("result.winner",CharacterCatalog.Get(winner).localName+" 승리",820,329,680,72,51,White);
            string order=CharacterCatalog.Get(flow.Match.Characters.Player(CourtSide.Near).Index).localName+"  /  "+CharacterCatalog.Get(flow.Match.Characters.Player(CourtSide.Far).Index).localName;
            Label("result.score",order+"\n"+flow.FinalScore,825,429,670,116,25,Muted);
            Button("rematch",flow.Model.Mode==GameMode.Online?(flow.Session.OwnRematchRequested?"재경기 요청 취소":"같은 방에서 재경기 요청"):"다시 경기하기",820,560,650,62,()=>flow.Rematch(),true);
            if(flow.Model.Mode==GameMode.Online)Label("result.rematch",flow.Session.RematchStatus,825,622,650,28,17,Lime);
            Button("menu","메인 메뉴로",820,653,650,62,()=>flow.ReturnToMenu());
        }
        private void Options()
        {
            Label("options.title","설정",190,150,1200,76,54,White);
            Button("motion",flow.ReduceMotion?"연출 움직임 줄이기   ON":"연출 움직임 줄이기   OFF",190,238,1220,50,()=>flow.ToggleMotion(),flow.ReduceMotion);
            Label("options.note","카메라 움직임과 입자·섬광의 강도를 줄입니다.",199,298,1150,30,18,Muted);
            Label("effects.title","파티클 효과",199,347,280,32,23,White);
            string[] levels={"적게","보통","풍부하게"};
            for(int i=0;i<3;i++){int q=i;Button("effects.quality."+i,levels[i],510+i*300,337,280,48,()=>flow.SetEffects(q),i==(int)flow.Match.Effects.Quality,19);}
            Label("effects.path","효과 CSV: "+flow.Match.Effects.Store.Path,199,404,1200,42,14,Muted);
            Button("effects.reload","CSV 다시 읽기  F5",190,457,380,46,()=>flow.ReloadEffects(),false,18);
            Label("effects.status",flow.Match.Effects.Store.Status,604,465,800,32,18,Lime);
            string[] graphics={"그래픽 · 낮음","그래픽 · 보통","그래픽 · 높음"};
            for(int i=0;i<3;i++){int q=i;Button("graphics."+i,graphics[i],190+i*415,510,390,34,()=>{GamePresentationQuality.Apply((PresentationQuality)q);flow.RefreshView();},i==(int)GamePresentationQuality.Current,17);}
            Button("controls","키보드 조작 안내",190,552,1220,54,()=>flow.Controls());
            Button("audio.options","음악 · 효과음 · 음성 설정과 A/B 청음",190,610,1220,46,()=>flow.AudioOptions());
            Label("balance.path","밸런스 CSV: "+flow.Match.Balance.Path+"\n캐릭터: "+flow.Match.Characters.Store.Path+"\n경기 규칙: "+flow.Match.RulesStore.Path+"\n밸런스는 서브 대기 중 적용 · 규칙 CSV는 새 경기부터 적용",199,659,1150,80,14,Muted);
            Label("balance.status",flow.Match.RulesStore.Status,199,741,1200,28,15,Lime);
            Button("back","←  뒤로",190,778,300,58,()=>flow.Back());
        }
        private void AudioOptions()
        {
            var audio=flow.Match.GetComponent<TennisAudio>();
            Label("audio.title","오디오 · 청음 설정",190,140,1220,76,48,White);
            string[] labels={"전체 음량","BGM · 경기 중 자동 음소거","캐릭터 · 심판 음성","효과음","경기장 환경음"};
            for(int i=0;i<5;i++)
            {
                int index=i;float value=i==0?audio.Master:audio.Volume((AudioBus)(i-1));float y=240+i*55;
                Label("audio.label."+i,(flow.AudioFocus==i?"▶ ":"")+labels[i],198,y,320,35,22,flow.AudioFocus==i?Lime:White);
                var number=Label("audio.value."+i,Mathf.RoundToInt(value*100)+"%",1260,y,150,35,22,Lime);
                VolumeSlider("audio.volume."+i,value,545,y+6,660,24,v=>{if(index==0)audio.SetMaster(v);else audio.SetVolume((AudioBus)(index-1),v);number.text=Mathf.RoundToInt(v*100)+"%";});
            }
            Button("audio.captions",audio.Captions?"한국어 자막 ON":"한국어 자막 OFF",190,530,380,44,()=>{audio.SetCaptions(!audio.Captions);flow.RefreshView();},audio.Captions,18);
            Button("audio.reload","외부 음원 · CSV 다시 읽기",605,530,805,44,()=>audio.Reload(),false,19);
            string[] purposes={"메뉴 음악","청음용 · 경기 중 OFF","결과 음악"};
            for(int i=0;i<3;i++)
            {
                int index=i;float y=594+i*49;Label("audio.purpose."+i,purposes[i],199,y,275,35,21,White);
                for(int variant=0;variant<2;variant++)
                {int selection=variant;Button("audio.variant."+i+"."+variant,variant==0?"A · 밝은 아케이드":"B · 다른 분위기",530+variant*445,y,420,39,()=>{audio.SetVariant(index,selection);audio.Audition(new[]{"menu","game","result"}[index]+(selection==0?"_a":"_b"));flow.RefreshView();},audio.Variant(i)==variant,18);}
            }
            Label("audio.path","외부 음원: "+audio.CsvPath,199,748,1220,30,13,Muted);
            Label("audio.status",audio.Status,199,780,1220,30,16,Lime);
            Button("back","←  뒤로",190,817,300,42,()=>flow.Back());
            Label("audio.keys","↑↓ 항목 선택 · ←→ 음량 조절  /  영어 합성 음성 초안",560,824,850,30,16,Muted);
        }
        private void VolumeSlider(string id,float value,float x,float y,float width,float height,Action<float> changed)
        {
            var background=Image(page.transform,id,x,y,width,height,Panel);background.raycastTarget=true;
            var fill=Image(background.transform,"Fill",0,0,width,height,Lime);fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
            var handle=Image(background.transform,"Handle",0,0,16,height+12,White);handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.anchoredPosition=Vector2.zero;
            var slider=background.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=value;slider.onValueChanged.AddListener(v=>changed(v));Sliders[id]=slider;
        }
        private void Controls()
        {
            Label("controls.title","키보드로 시작하기",145,187,1300,90,60,White);
            Label("controls.body","SPACE   서브\nQ / W   포핸드 / 백핸드\nS / V     스매시 / 발리\nX / B     슬라이스 / 로브\nK            고유 스킬 (사용권 1개)\nESC        경기 메뉴",155,317,1280,345,29,White);
            Label("controls.note","캐릭터는 자동 이동합니다. 내 코트의 타격 가능한 공을 맞추세요.\n게이지는 성공한 공격·수비로 채워지고, 사용권은 최대 3개입니다.",155,670,1270,78,23,Muted);
            Button("back","←  뒤로",150,790,300,58,()=>flow.Back());
        }
        private void Quit()
        {Label("quit.title","게임을 종료할까요?",200,280,1200,100,64,White,TextAnchor.MiddleCenter);Button("quit.confirm","게임 종료",420,500,350,65,()=>Application.Quit(),true);Button("back","계속하기",830,500,350,65,()=>flow.Back());}
        private void Portrait(int slot,float x,float y,float w,float h,int index)
        {flow.Portraits?.Show(slot,index);var raw=new GameObject("Live Character Preview "+slot,typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();Set(raw.rectTransform,page.transform,x,y,w,h);raw.texture=flow.Portraits?.Texture(slot);raw.raycastTarget=false;}
        private void Logo(Transform parent,float x,float y,float size)
        {var raw=new GameObject("Organization Logo",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();Set(raw.rectTransform,parent,x,y,size,size);raw.texture=Resources.Load<Texture2D>("FrontEnd/Logo");raw.raycastTarget=false;}
        private Text Label(string id,string text,float x,float y,float w,float h,int size,Color tint,TextAnchor align=TextAnchor.UpperLeft)
        {var t=Text(page.transform,text,x,y,w,h,size,tint,align);t.name=id;Texts[id]=t;return t;}
        private Text Text(Transform parent,string text,float x,float y,float w,float h,int size,Color tint,TextAnchor align=TextAnchor.MiddleLeft)
        {var t=new GameObject("Text",typeof(RectTransform),typeof(Text)).GetComponent<Text>();Set(t.rectTransform,parent,x,y,w,h);t.font=font;t.text=text;t.fontSize=size;t.color=tint;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        private Button Button(string id,string title,float x,float y,float w,float h,Action clicked,bool selected=false,int size=22)
        {
            var im=Image(page.transform,id,x,y,w,h,selected?Lime:Panel);im.raycastTarget=true;
            var button=im.gameObject.AddComponent<Button>();button.targetGraphic=im;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.7f,.88f,.8f);colors.pressedColor=new Color(.4f,.65f,.55f);colors.disabledColor=new Color(.4f,.4f,.4f,.7f);button.colors=colors;
            var nav=button.navigation;nav.mode=Navigation.Mode.None;button.navigation=nav;
            Text(im.transform,title,12,0,w-24,h,size,selected?Ink:White,TextAnchor.MiddleCenter);
            button.onClick.AddListener(()=>{flow.Match?.GetComponent<TennisAudio>()?.Play("confirm");clicked();});Buttons[id]=button;return button;
        }
        private InputField Field(string id,string value,float x,float y,float w,float h)
        {var im=Image(page.transform,id,x,y,w,h,Panel);im.raycastTarget=true;var field=im.gameObject.AddComponent<InputField>();field.textComponent=Text(im.transform,"",18,0,w-36,h,23,White);field.text=value;field.lineType=InputField.LineType.SingleLine;field.characterLimit=256;return field;}
        private static Image Image(Transform parent,string name,float x,float y,float w,float h,Color tint)
        {var im=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();Set(im.rectTransform,parent,x,y,w,h);im.color=tint;im.raycastTarget=false;return im;}
        private static FrontEndGraphic Graphic(Transform parent,FrontGraphicKind kind,float x,float y,float w,float h,Color tint)
        {var g=new GameObject(kind.ToString(),typeof(RectTransform),typeof(CanvasRenderer),typeof(FrontEndGraphic)).GetComponent<FrontEndGraphic>();Set(g.rectTransform,parent,x,y,w,h);g.Kind=kind;g.color=tint;g.raycastTarget=false;g.SetVerticesDirty();return g;}
        private static void Set(RectTransform rect,Transform parent,float x,float y,float w,float h)
        {rect.SetParent(parent,false);rect.gameObject.layer=5;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
    }
}
