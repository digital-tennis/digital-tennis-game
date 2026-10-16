using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Opt-in live scene survey, followed by ordinary racket input. No score/ball writes.</summary>
    public sealed class StadiumValidationDriver : MonoBehaviour
    {
        private int errors;
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-dt-stadium-verify");
            if(index<0||index+1>=args.Length){enabled=false;yield break;}
            string dir=args[index+1];Directory.CreateDirectory(dir);Application.runInBackground=true;Application.targetFrameRate=60;
            Application.logMessageReceived+=Error;yield return null;
            var c=GetComponent<TennisMatchController>();var venue=UnityEngine.Object.FindFirstObjectByType<TennisStadium>();
            c.AutoServe=c.DemoReturns=false;
            bool front=FrontEndController.HasArgument("-dt-stadium-front");
            if(front)
            {
                while(FrontEndController.Active?.Ready!=true)yield return null;
                var f=FrontEndController.Active;if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();
                Click("continue");Click("menu.single");Click("country.KOR");Click("confirm");Click("country.JPN");Click("confirm");Click("country.KOR");Click("confirm");
                while(f.Model.Stage!=FrontEndStage.Playing)yield return null;
                yield return new WaitForEndOfFrame();Capture("stadium-hud","real front-end game camera with HUD");
                void Click(string id){if(!f.View.Invoke(id))throw new InvalidOperationException("Stadium front-end button unavailable: "+id);}
            }
            for(int i=0;!front&&i<15;i++)
            {
                c.InputAdapter.Press(PlayerAction.SelectCourt,false,i);
                foreach(var side in new[]{CourtSide.Near,CourtSide.Far})
                {
                    c.View.SetPlayer(side);yield return null;yield return new WaitForEndOfFrame();
                    Capture(CharacterCatalog.Ids[i]+"-"+side.ToString().ToLowerInvariant(),"staged live court view");
                }
            }
            if(!front)
            {
                c.View.SetPlayer(CourtSide.Near);c.InputAdapter.Press(PlayerAction.SelectCourt,false,0);
                var cam=Camera.main;var at=cam.transform.position;var rotation=cam.transform.rotation;
                cam.transform.position=new Vector3(28,30,-34);cam.transform.LookAt(new Vector3(0,1,0));
                yield return new WaitForEndOfFrame();Capture("KOR-overview","staged stadium overview");cam.transform.SetPositionAndRotation(at,rotation);
                c.Configure(GameMode.SinglePlayer,DigitalTennis.AI.Difficulty.Hard,42);
            }
            c.InputAdapter.UseSource(new SurveyInput(c));bool rally=false;
            double end=Time.realtimeSinceStartupAsDouble+60;
            while(Time.realtimeSinceStartupAsDouble<end&&(c.Match.Progress.TotalPoints==0||!rally))
            {
                if(!rally&&c.NearHits>=2&&c.FarHits>=2&&c.Match.Phase==MatchPhase.Rally)
                {yield return new WaitForEndOfFrame();Capture("live-rally","real adapter input and AI");rally=true;}
                yield return null;
            }
            yield return new WaitForEndOfFrame();Capture("after-point","real point awarded");
            end=Time.realtimeSinceStartupAsDouble+10;
            while(!c.ReadyToServe&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            yield return new WaitForEndOfFrame();Capture("next-serve","real next-serve readiness after the point");
            bool pass=errors==0&&rally&&c.Match.Progress.TotalPoints>0&&c.ReadyToServe&&venue.SpectatorCount>=400&&venue.GetComponentsInChildren<Collider>(true).Length==0;
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonUtility.ToJson(new Report{passed=pass,nextServeReady=c.ReadyToServe,runtimeErrors=errors,spectators=venue.SpectatorCount,seats=venue.SeatCount,
                nearHits=c.NearHits,farHits=c.FarHits,totalPoints=c.Match.Progress.TotalPoints,physicsTicks=c.Ball.PhysicsTicks,themes=front?1:15,views=front?1:30,colliders=venue.GetComponentsInChildren<Collider>(true).Length},true));
            Application.logMessageReceived-=Error;Application.Quit(pass?0:1);
            void Capture(string name,string mode)
            {
                var f=FrontEndController.Active;f?.RenderUiNow();
                GameCameraCapture.Save(Camera.main,Path.Combine(dir,name+".png"),1600,1000,f?.UiCamera);
                File.WriteAllText(Path.Combine(dir,name+".json"),JsonUtility.ToJson(new Frame{mode=mode,localView=c.View.LocalPlayer.ToString(),camera=GamePayload.Pack(Camera.main.transform.position),
                    state=GamePayload.Capture(c,"stadium-survey",0)},true));
            }
        }
        [Serializable] private sealed class Frame{public string mode,localView;public float[] camera;public GamePayload state;}
        [Serializable] private sealed class Report{public bool passed,nextServeReady;public int runtimeErrors,spectators,seats,nearHits,farHits,themes,views,colliders;public long totalPoints;public ulong physicsTicks;}
        private sealed class SurveyInput : BufferedPlayerInputSource
        {
            private readonly TennisMatchController c;private uint sequence;private double previous;
            public SurveyInput(TennisMatchController controller):base("Stadium validation (CLI only)"){c=controller;}
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                if(c.Match.Progress.TotalPoints>0||now-previous<.1)return;PlayerAction action;
                if(c.ReadyToServe&&c.Match.Server==CourtSide.Near)action=PlayerAction.Serve;
                else if(c.Hits<6&&c.CanReturn(CourtSide.Near))action=c.NearHits%2==0?PlayerAction.Forehand:PlayerAction.Backhand;
                else return;
                previous=now;float facing=c.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                float aim=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*1.6f*facing,17)*Mathf.Rad2Deg/c.Tuning["shot.yaw"],-1,1);
                Enqueue(new PlayerInputSample(action,++sequence,now,.35f,aim));
            }
        }
    }
}
