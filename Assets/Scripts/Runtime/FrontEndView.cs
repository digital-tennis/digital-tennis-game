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
        public int ButtonActivations { get; private set; }
        public string PointerProblem { get; private set; }
        public Canvas Canvas { get; private set; }
        private readonly FrontEndController flow;
        private readonly Font font;
        private readonly RectTransform frame;
        private GameObject page;
        private CanvasGroup pageGroup;
        private readonly Image screen;
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
        public void Resize() => frame.localScale=Vector3.one*Mathf.Min(flow.UiCamera.pixelWidth/1600f,flow.UiCamera.pixelHeight/900f);
        public void Animate(float age,bool reduced)
        { if(pageGroup!=null) pageGroup.alpha=reduced||flow.Model.Stage==FrontEndStage.Playing?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.2f)); }
        public bool Invoke(string id)
        {
            if (!Buttons.TryGetValue(id,out var button) || !button.interactable || !button.gameObject.activeInHierarchy) return false;
            // Offscreen players have no physical window pointer; invoke the same Unity UI button callback.
            if(Application.isBatchMode) {ButtonActivations++;button.onClick.Invoke();return true;}
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) {button.onClick.Invoke();return true;}
            if(EventSystem.current==null)return false;
            flow.RenderUiNow();
            Canvas.ForceUpdateCanvases();
            var rect=button.GetComponent<RectTransform>();
            var pointer=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(flow.UiCamera,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=button.gameObject)
            {PointerProblem=$"{id}: point={pointer.position} screen={Screen.width}x{Screen.height} hits={hits.Count} top={(hits.Count>0?hits[0].gameObject.name:"none")}";return false;}
            ButtonActivations++;ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);return true;
        }
        public void Render()
        {
            if (page!=null) { page.SetActive(false);UnityEngine.Object.Destroy(page); }
            Buttons.Clear();Texts.Clear();Address=RoomCode=null;
            page=new GameObject("Page "+flow.Model.Stage,typeof(RectTransform));page.transform.SetParent(frame,false);var pr=page.GetComponent<RectTransform>();pr.anchorMin=Vector2.zero;pr.anchorMax=Vector2.one;pr.offsetMin=pr.offsetMax=Vector2.zero;
            pageGroup=page.AddComponent<CanvasGroup>();
            var stage=flow.Model.Stage; screen.enabled=stage!=FrontEndStage.Playing;
            if (stage==FrontEndStage.Logo) { Logo(page.transform,665,245,270);Label("studio","DIGITAL TENNIS",460,545,680,58,40,White,TextAnchor.MiddleCenter);Label("present","A WORLD OF PLAY",460,608,680,40,18,Muted,TextAnchor.MiddleCenter);return; }
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
            else if(stage==FrontEndStage.Quit) Quit();
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
            Label("status",flow.LobbyStatus,865,750,670,80,19,White,TextAnchor.MiddleCenter);
            Address.interactable=RoomCode.interactable=free;
        }
        private void Hud()
        {
            var c=flow.Match;var s=flow.Session;CourtSide own=s.IsOnline?c.View.LocalPlayer:CourtSide.Near;
            var a=c.Characters.Player(own);var b=c.Characters.Player(TennisMatch.Opponent(own));
            Image(page.transform,"Score Backplate",380,18,840,92,new Color(.025f,.065f,.09f,.94f));
            Label("score","",400,29,800,55,33,White,TextAnchor.MiddleCenter);
            Label("phase","",400,83,800,23,14,Lime,TextAnchor.MiddleCenter);
            Meter("you",a,48,735);Meter("opponent",b,1210,735);
            Button("pause","ESC  ·  메뉴",1360,24,192,42,()=>flow.Pause());
            Button("serve","SPACE  서브",640,812,320,49,()=>c.InputAdapter.Press(PlayerAction.Serve),true);
            Label("keys","Q / W  포핸드·백핸드     S  스매시     V  발리     X  슬라이스     B  로브     K  스킬",342,872,920,24,14,White,TextAnchor.MiddleCenter);
            Label("call","",375,650,850,55,26,Lime,TextAnchor.MiddleCenter);
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
            string da=remote!=null?ScoreText(remote.points[i],remote.points[j]):c.Match.Score.Display(own);
            string db=remote!=null?ScoreText(remote.points[j],remote.points[i]):c.Match.Score.Display((CourtSide)j);
            Texts["score"].text=$"{CharacterCatalog.Get(a.Index).localName}    {da}  :  {db}    {CharacterCatalog.Get(b.Index).localName}";
            bool ready=s.IsOnline&&!s.IsHost?remote?.phase=="WaitServe":c.ReadyToServe;
            Texts["phase"].text=c.Characters.Cinematic?"SPECIAL SKILL":ready?"READY TO SERVE":"RALLY  /  PLAY";
            foreach(var pair in new[]{("you",a),("opponent",b)})
            { var p=CharacterCatalog.Get(pair.Item2.Index);Texts[pair.Item1+".name"].text=p.country+" / "+p.localName;Texts[pair.Item1+".meter"].text=$"SKILL {pair.Item2.Meter.Stock}/3  ·  {pair.Item2.Meter.Gauge:0}%  ·  체력 {pair.Item2.Energy:0}";Texts[pair.Item1+".skill"].text=p.skillName; }
            Texts["you.meter"].color=Color.Lerp(Lime,White,c.Effects.ChargePulse(own));
            Texts["opponent.meter"].color=Color.Lerp(Lime,White,c.Effects.ChargePulse((CourtSide)j));
            Texts["call"].text=remote!=null?remote.officials.text:c.Match.CallText;
            if(c.Characters.Cinematic)
            {var active=c.Characters.Player(CourtSide.Near).Cinematic?c.Characters.Player(CourtSide.Near):c.Characters.Player(CourtSide.Far);Texts["call"].text=CharacterCatalog.Get(active.Index).skillName;}
            if(s.IsOnline&&!s.Playing) {Texts["phase"].text="연결 복구 중 · ESC로 메뉴";Texts["call"].text="상대와의 연결을 확인하고 있습니다.";}
            Buttons["serve"].gameObject.SetActive(ready&&(!s.IsOnline||s.IsHost));
            Buttons["skill"].interactable=c.InputAdapter.InputEnabled&&a.Meter.Stock>0&&!c.Characters.Cinematic&&a.Remaining==0;
        }
        private static string ScoreText(int own,int other)
        {return new TennisScore(own,other).Display(CourtSide.Near);}
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
            Label("result.head","GAME SET",820,227,680,70,52,Lime);
            Label("result.winner",CharacterCatalog.Get(winner).localName+" 승리",820,329,680,72,51,White);
            Label("result.score",flow.FinalScore,825,429,670,55,32,Muted);
            if(flow.Model.Mode!=GameMode.Online) Button("rematch","다시 경기하기",820,560,650,62,()=>flow.Rematch(),true);
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
            Button("controls","키보드 조작 안내",190,548,1220,54,()=>flow.Controls());
            Label("audio.note","음성·BGM은 청음 제작을 준비 중입니다.",199,623,1150,38,21,Muted);
            Label("balance.path","밸런스 CSV: "+flow.Match.Balance.Path+"\n캐릭터: "+flow.Match.Characters.Store.Path+"\n밸런스는 서브 대기 중 호스트가 일괄 적용합니다.",199,663,1150,64,14,Muted);
            Label("balance.status",flow.Match.Balance.Status,199,734,1200,32,16,Lime);
            Button("back","←  뒤로",190,778,300,58,()=>flow.Back());
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
            button.onClick.AddListener(()=>clicked());Buttons[id]=button;return button;
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
