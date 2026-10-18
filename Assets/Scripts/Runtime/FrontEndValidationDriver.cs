using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Opt-in UI verification. Invokes real UI buttons and feeds normal racket input only.</summary>
    public sealed class FrontEndValidationDriver : MonoBehaviour
    {
        private FrontEndController flow;
        private string directory;
        private int errors, selectable;
        private bool finished, pauseHeld, rematchReset, observing;
        private double began;
        private GamePayload completedGame;
        private HumanSource source;
        private readonly List<string> captures=new List<string>();
        private void Count(string text,string trace,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-frontend-verify");
            int observer=Array.IndexOf(args,"-dt-input-observe");
            if(observer>=0) {observing=true;flag=observer;}
            if(flag<0||flag+1>=args.Length) {enabled=false;yield break;}
            directory=args[flag+1];Directory.CreateDirectory(directory);began=Time.realtimeSinceStartupAsDouble;Application.runInBackground=true;Application.targetFrameRate=60;
            Application.logMessageReceived+=Count;flow=GetComponent<FrontEndController>();
            while(!flow.Ready)yield return null;
            if(observing)
            {
                // Passive evidence only: keep the real keyboard source and never press a button,
                // submit an input, change a match, or quit the user's game.
                string previousStage=null;int delivered=-1,previousFocus=-1,deviceRevision=-1;
                while(captures.Count<48)
                {
                    if(previousStage!=flow.Model.Stage.ToString()||delivered!=flow.Match.InputAdapter.Delivered||previousFocus!=flow.Model.Focus||deviceRevision!=flow.InputDevices.Revision)
                    {
                        yield return Capture("input-"+captures.Count.ToString("D2"));
                        previousStage=flow.Model.Stage.ToString();delivered=flow.Match.InputAdapter.Delivered;
                        previousFocus=flow.Model.Focus;deviceRevision=flow.InputDevices.Revision;
                    }
                    yield return null;
                }
                enabled=false;yield break;
            }
            yield return Capture("01-logo");flow.Confirm();yield return Capture("02-title");
            Click("continue");yield return Capture("03-menu");
            Click("menu.options");Click("effects.reload");yield return Capture("04-options");Click("controls");yield return Capture("05-controls");Click("back");Click("back");
            Click("menu.single");
            for(int i=0;i<15;i++) {Click("map."+CharacterCatalog.Ids[i]);yield return null;if(flow.Model.Focus==i&&flow.View.Texts["name"].text==CharacterCatalog.Get(i).localName)selectable++;}
            Click("country.KOR");yield return Capture("06-world-player");Click("confirm");
            Click("country.ESP");Click("difficulty.0");yield return Capture("07-world-opponent");Click("country.JPN");Click("confirm");
            Click("map.MAR");yield return Capture("08-world-court");Click("country.KOR");Click("confirm");yield return Capture("09-versus");
            while(flow.Model.Stage!=FrontEndStage.Playing)yield return null;
            source=new HumanSource(flow);flow.Match.InputAdapter.UseSource(source);
            while(flow.Match.Hits<1 && flow.Model.Stage!=FrontEndStage.Results)yield return null;
            yield return Capture("10-playing");
            Click("pause");ulong ticks=flow.Match.Ball.PhysicsTicks;long points=flow.Match.Match.Score.TotalPoints;
            yield return new WaitForSeconds(.25f);pauseHeld=ticks==flow.Match.Ball.PhysicsTicks&&points==flow.Match.Match.Score.TotalPoints;
            yield return Capture("11-paused");Click("confirm");
            while(flow.Model.Stage!=FrontEndStage.Results)yield return null;
            completedGame=GamePayload.Capture(flow.Match,"front-end-ui-validation",0);yield return Capture("12-results");
            Click("rematch");rematchReset=flow.Match.Match.Progress.TotalPoints==0 && flow.Match.Match.Progress.NearSets==0&&flow.Match.Match.Progress.FarSets==0&&flow.Match.Match.Progress.NearGames==0&&flow.Match.Match.Progress.FarGames==0 && flow.Match.Characters.Player(CourtSide.Near).Index==0 && flow.Match.Characters.Player(CourtSide.Far).Index==1 && flow.Match.Characters.CourtIndex==0;
            yield return Capture("13-rematch");
            while(flow.Model.Stage!=FrontEndStage.Playing)yield return null;
            Click("pause");Click("menu");yield return Capture("14-return-menu");Finish(false);
        }
        private void Update()
        {
            if(observing&&flow?.Ready==true&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonUp(0)))
            {
                var e=UnityEngine.EventSystems.EventSystem.current;var pointer=new UnityEngine.EventSystems.PointerEventData(e){position=Input.mousePosition};var hits=new List<UnityEngine.EventSystems.RaycastResult>();e.RaycastAll(pointer,hits);
                Debug.Log($"POINTER_OBSERVE down={Input.GetMouseButtonDown(0)} pos={Input.mousePosition} screen={Screen.width}x{Screen.height} mousePresent={Input.mousePresent} focused={Application.isFocused} module={e.currentInputModule?.GetType().Name} hits={hits.Count} top={(hits.Count>0?hits[0].gameObject.name: "none")}");
            }
            if(!observing&&!finished&&directory!=null&&Time.realtimeSinceStartupAsDouble-began>900)Finish(true);
        }
        private void OnGUI()
        {
            if(!observing||flow?.Ready!=true)return;
            var e=Event.current;
            if(e.rawType!=EventType.MouseDown&&e.rawType!=EventType.MouseUp)return;
            var screenPosition=new Vector2(e.mousePosition.x,Screen.height-e.mousePosition.y);
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events==null)return;
            var pointer=new UnityEngine.EventSystems.PointerEventData(events){position=screenPosition};
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(pointer,hits);
            // Observe only; never consume the OS event, invoke a button, or replace gameplay input.
            var frame=new PointerFrame{frame=Time.frameCount,stage=flow.Model.Stage.ToString(),kind=e.rawType.ToString(),button=e.button,
                focused=Application.isFocused,eventPosition=e.mousePosition,screenPosition=screenPosition,polledPosition=Input.mousePosition,
                inside=screenPosition.x>=0&&screenPosition.x<Screen.width&&screenPosition.y>=0&&screenPosition.y<Screen.height,
                top=hits.Count>0?hits[0].gameObject.name:null};
            File.AppendAllText(Path.Combine(directory,"native-pointer.ndjson"),JsonUtility.ToJson(frame)+"\n");
            Debug.Log($"POINTER_NATIVE {frame.kind} event={frame.eventPosition} screen={frame.screenPosition} polled={frame.polledPosition} focused={frame.focused} top={frame.top}");
        }
        private void Click(string id) {if(!flow.View.Invoke(id)) {errors++;Debug.LogWarning("FRONT_END_VERIFY missing enabled button "+id+" "+flow.View.PointerProblem);Finish(false);StopAllCoroutines();}}
        private IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            flow.RenderUiNow();
            string path=Path.Combine(directory,name+".png");
            if(observing&&!Application.isBatchMode)ScreenCapture.CaptureScreenshot(path);
            else GameCameraCapture.Save(Camera.main,path,1600,900,flow.UiCamera);
            captures.Add(name);
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(new Frame {
                stage=flow.Model.Stage.ToString(),source=observing&&!Application.isBatchMode?"game-frame-with-canvas":"running-game-cameras-with-canvas",player=flow.Model.Player,opponent=flow.Model.Opponent,court=flow.Model.Court,
                focused=flow.Model.Focus,points=flow.Match.Match.Progress.TotalPoints,score=MatchScoreState.Capture(flow.Match.Match.Progress),gameplayEnabled=flow.Match.InputAdapter.GameplayEnabled,
                inputSource=flow.Match.InputAdapter.SourceName,action=flow.Match.InputAdapter.LastAction?.ToString(),swingResult=flow.Match.InputAdapter.LastSwingResult?.ToString(),
                nearHits=flow.Match.NearHits,farHits=flow.Match.FarHits,swingId=flow.Match.NearAvatar.SwingId,swingAge=flow.Match.NearAvatar.SwingAge,
                inputFeedback=flow.View.Texts.TryGetValue("input.feedback",out var feedback)?feedback.text:null },true));
            Debug.Log("FRONT_END_CAPTURE "+name+" "+flow.Model.Stage);
        }
        private void Finish(bool timedOut)
        {
            if(finished)return;finished=true;
            bool pass=!timedOut&&errors==0&&selectable==15&&pauseHeld&&rematchReset&&completedGame!=null&&completedGame.winner!=null&&completedGame.nearHits>0&&completedGame.farHits>0&&source.Serves>=4&&captures.Count==14&&flow.Model.Stage==FrontEndStage.Menu;
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Report {
                passed=pass,timedOut=timedOut,runtimeErrors=errors,selectableCharacters=selectable,pauseHeld=pauseHeld,rematchReset=rematchReset,
                serves=source?.Serves??0,transitions=flow.Transitions,buttonActivations=flow.View.ButtonActivations,captures=captures.ToArray(),completedGame=completedGame,rendering="actual-game-cameras-with-canvas",uiInput="Unity Button callbacks in offscreen mode" },true));
            Debug.Log("FRONT_END_VALIDATION "+(pass?"PASS":"FAIL"));Application.Quit(pass?0:1);
        }
        private void OnDestroy() {Application.logMessageReceived-=Count;}
        [Serializable]private sealed class PointerFrame
        {public int frame,button;public string stage,kind,top;public bool focused,inside;public Vector2 eventPosition,screenPosition;public Vector3 polledPosition;}
        [Serializable] private sealed class Frame
            {public string stage,source,inputSource,action,swingResult,inputFeedback;public int player,opponent,court,focused,nearHits,farHits;public uint swingId;public float swingAge;public long points;public bool gameplayEnabled;public MatchScoreState score;}
        [Serializable] private sealed class Report
        {public bool passed,timedOut,pauseHeld,rematchReset;public int runtimeErrors,selectableCharacters,serves,transitions,buttonActivations;public string rendering,uiInput;public string[] captures;public GamePayload completedGame;}
        private sealed class HumanSource : BufferedPlayerInputSource
        {
            private readonly FrontEndController flow;
            private uint sequence;
            private double last;
            public int Serves {get;private set;}
            public HumanSource(FrontEndController owner):base("Front end validation (CLI only)"){flow=owner;}
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                var c=flow.Match;if(flow.Model.Stage!=FrontEndStage.Playing||now-last<.1)return;
                PlayerAction action;
                if(c.ReadyToServe&&c.Match.Server==CourtSide.Near) {action=PlayerAction.Serve;Serves++;}
                else if(c.Match.Progress.TotalPoints<5&&c.Match.Phase==MatchPhase.Rally && c.Ball.Simulation.BounceCount==1 && c.Ball.Simulation.Position.y>=.95f && c.CanReturn(CourtSide.Near))
                    action=c.NearHits%2==0?PlayerAction.Forehand:PlayerAction.Backhand;
                else return;
                last=now;float facing=c.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                float horizontal=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*1.6f*facing,17)*Mathf.Rad2Deg/25,-1,1);
                Enqueue(new PlayerInputSample(action,++sequence,now,.35f,horizontal));
            }
        }
    }
}
