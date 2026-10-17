using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Opt-in real input motion survey. Never writes physics, points or stock.</summary>
    // Observe the completed pose, including profile-scale changes and the final grip lock.
    [DefaultExecutionOrder(10000)]
    public sealed class MotionValidationDriver:MonoBehaviour
    {
        private string nearId="KOR",farId="JPN";private int nearIndex=0,farIndex=1;
        private TennisMatchController c;private string dir;private int errors,contacts,serves;private float maxContact,maxFoot,maxSlip,maxGrip,maxArmStretch;
        private readonly HashSet<string> captured=new HashSet<string>();
        private bool capturedServe,capturedRunning;private Vector3 lastNear,lastFar;private float travel;
        private readonly HashSet<StrokeKind> checkedStrokes=new HashSet<StrokeKind>();private StrokeKind wanted;private bool hitWanted,recording;
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-motion-verify");if(flag<0||flag+1>=args.Length){enabled=false;yield break;}
            dir=args[flag+1];Directory.CreateDirectory(dir);Application.logMessageReceived+=Error;Application.runInBackground=true;yield return null;
            c=GetComponent<TennisMatchController>();c.SwingEvaluated+=Contact;
            int pair=Array.IndexOf(args,"-dt-motion-pair");
            if(pair>=0&&pair+1<args.Length)
            {
                var ids=args[pair+1].Split(',');
                if(ids.Length!=2)throw new ArgumentException("Motion pair requires two roster IDs");
                nearIndex=Array.IndexOf(CharacterCatalog.Ids,ids[0]);farIndex=Array.IndexOf(CharacterCatalog.Ids,ids[1]);
                if(nearIndex<0||farIndex<0)throw new ArgumentException("Unknown motion roster ID");
                nearId=ids[0];farId=ids[1];
            }
            foreach(var stroke in new[]{StrokeKind.Forehand,StrokeKind.Backhand,StrokeKind.Volley,StrokeKind.Slice,StrokeKind.Lob,StrokeKind.Smash})
            {
                wanted=stroke;hitWanted=false;ConfigurePair();c.AutoServe=c.DemoReturns=false;c.IsPaused=false;c.InputAdapter.UseSource(new Probe(c,stroke));
                double until=Time.realtimeSinceStartupAsDouble+45;
                while((!hitWanted||recording)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            }
            ConfigurePair();c.AutoServe=c.DemoReturns=false;c.IsPaused=false;wanted=StrokeKind.Forehand;hitWanted=true;
            c.InputAdapter.UseSource(new Probe(c,StrokeKind.Forehand,true));int startContacts=contacts;double stressEnd=Time.realtimeSinceStartupAsDouble+60;
            while(contacts-startContacts<20&&Time.realtimeSinceStartupAsDouble<stressEnd)yield return null;
            // A cached speed during deceleration is not running evidence. Continue ordinary
            // rallies until both cameras have observed real root travel and stride change.
            double runningEnd=Time.realtimeSinceStartupAsDouble+80;
            while((!captured.Contains(nearId+"-Running")||!captured.Contains(farId+"-Running")||recording)&&Time.realtimeSinceStartupAsDouble<runningEnd)yield return null;
            // Let ordinary missed returns finish the current game so Far also serves.
            // No score/ball setters: service order must be earned by normal point progression.
            c.InputAdapter.UseSource(new Probe(c,StrokeKind.Forehand,false,true));
            double serviceEnd=Time.realtimeSinceStartupAsDouble+80;
            while((!captured.Contains(farId+"-Serve")||recording)&&Time.realtimeSinceStartupAsDouble<serviceEnd)yield return null;
            bool passed=captured.Contains(nearId+"-Running")&&captured.Contains(farId+"-Running")&&captured.Contains(farId+"-Serve")&&captured.Contains(nearId+"-Forehand")&&captured.Contains(farId+"-Forehand")&&captured.Contains(nearId+"-Backhand")&&captured.Contains(farId+"-Backhand")&&capturedServe&&capturedRunning&&maxArmStretch<=AcquiredAthleteMotion.Config.maxArmStretch+.001f&&maxGrip<.035f&&contacts-startContacts>=20&&maxSlip<.035f&&errors==0&&checkedStrokes.Count==6&&contacts>=6&&serves>=6&&maxContact<.035f&&maxFoot<.04f;
            File.WriteAllText(Path.Combine(dir,"motion.json"),JsonUtility.ToJson(new Report{nearId=CharacterCatalog.Ids[c.Characters.Player(CourtSide.Near).Index],farId=CharacterCatalog.Ids[c.Characters.Player(CourtSide.Far).Index],passed=passed,errors=errors,contacts=contacts,serves=serves,strokes=checkedStrokes.Count,maxContactError=maxContact,maxPlantedError=maxFoot,maxPlantedSlipPerFrame=maxSlip,maxGripError=maxGrip,maxArmStretch=maxArmStretch,capturedServe=capturedServe,capturedRunning=capturedRunning,scope="automated ordinary practice adapter; actual hit/serve anchors; no physical keyboard, score or ball writes"},true));
            c.SwingEvaluated-=Contact;Application.logMessageReceived-=Error;Application.Quit(passed?0:1);
        }
        private void ConfigurePair()
        {
            c.Configure(GameMode.Practice);
            c.InputAdapter.Press(PlayerAction.SelectCharacter,false,nearIndex);
            c.InputAdapter.Press(PlayerAction.SelectCharacter,true,farIndex);
        }
        private void Contact(RacketInput input,SwingResult result)
        {
            if(result!=SwingResult.Hit)return;contacts++;
            var avatar=input.Player==CourtSide.Near?c.NearAvatar:c.FarAvatar;
            maxContact=Mathf.Max(maxContact,Vector3.Distance(avatar.StringCenter,c.Ball.Simulation.Position));
            string id=input.Player==CourtSide.Near?nearId:farId;
            if(input.Stroke==wanted&&!recording&&!captured.Contains(id+"-"+wanted))
            {
                captured.Add(id+"-"+wanted);checkedStrokes.Add(wanted);StartCoroutine(Capture(avatar,id+"-"+wanted));
                hitWanted=captured.Contains(nearId+"-"+wanted)&&captured.Contains(farId+"-"+wanted);
            }
        }
        private void LateUpdate()
        {
            if(c==null)return;
            foreach(var a in new[]{c.NearAvatar,c.FarAvatar})
            {
                var rig=a.GetComponentInChildren<AthleteMotion>();maxFoot=Mathf.Max(maxFoot,rig.PlantedFootError);maxSlip=Mathf.Max(maxSlip,rig.PlantedFootSlip);
                var hand=rig.transform.Find("Torso/RightArm/Elbow/Skin_Hand");var pivot=a.transform.Find("Racket Pivot");
                maxArmStretch=Mathf.Max(maxArmStretch,rig.ArmStretch);
                maxGrip=Mathf.Max(maxGrip,Vector3.Distance(rig.GripPosition,pivot.TransformPoint(new Vector3(0,.12f,0))));
                string id=a==c.NearAvatar?nearId:farId;
                if(a.IsServe&&a.SwingAge>=0&&a.SwingAge<.12f&&!recording&&!captured.Contains(id+"-Serve"))
                {captured.Add(id+"-Serve");capturedServe=true;StartCoroutine(Capture(a,id+"-Serve"));}
            }
            // Exclude point resets/teleports; capture an actual moving frame, without posing the actor.
            float nearDistance=Vector3.Distance(c.NearAvatar.transform.position,lastNear),farDistance=Vector3.Distance(c.FarAvatar.transform.position,lastFar);
            float distance=nearDistance+farDistance;
            if(distance>0&&distance<.5f)travel+=distance;
            if(travel>1&&distance>.005f&&distance<.5f&&!recording)
            {
                RacketAvatar actor=null;string id=null;
                if(nearDistance>.005f&&!captured.Contains(nearId+"-Running")){actor=c.NearAvatar;id=nearId;}
                else if(farDistance>.005f&&!captured.Contains(farId+"-Running")){actor=c.FarAvatar;id=farId;}
                if(actor!=null){capturedRunning=true;captured.Add(id+"-Running");StartCoroutine(Capture(actor,id+"-Running"));}
            }
            lastNear=c.NearAvatar.transform.position;lastFar=c.FarAvatar.transform.position;
        }
        private IEnumerator Capture(RacketAvatar actor,string name)
        {
            recording=true;
            var camera=c.View.Camera;
            // Capture live poses over contact, follow-through, recovery, then the ready stance.
            // The camera is restored every frame; neither player nor ball is posed for capture.
            var samples=new List<Sample>();
            for(int frame=0;frame<16;frame++)
            {
                yield return new WaitForEndOfFrame();
                Vector3 p=camera.transform.position;Quaternion q=camera.transform.rotation;float fov=camera.fieldOfView;
                Vector3 focus=actor.transform.position+Vector3.up*.4f;
                camera.transform.position=focus+actor.transform.forward*4.0f+Vector3.up*.6f+actor.transform.right*1.5f;camera.transform.LookAt(focus);camera.fieldOfView=48;
                var rig=actor.GetComponentInChildren<AthleteMotion>();
                samples.Add(new Sample{frame=frame,clock=Time.realtimeSinceStartup,swingId=actor.SwingId,swingAge=actor.SwingAge,swinging=actor.IsSwinging,serve=actor.IsServe,stroke=actor.LastStroke.ToString(),root=actor.transform.position,speed=rig.LocomotionSpeed,stride=rig.Stride,armStretch=rig.ArmStretch});
                GameCameraCapture.Save(camera,Path.Combine(dir,name+"-"+frame.ToString("D2")+".png"),1200,900);
                if(frame==0)File.Copy(Path.Combine(dir,name+"-00.png"),Path.Combine(dir,name+".png"),true);
                camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;
                yield return new WaitForSeconds(.025f);
            }
            if(name.EndsWith("-Running",StringComparison.Ordinal))
            {
                float travelled=0;
                bool continuous=true;
                for(int i=1;i<samples.Count;i++)
                {
                    float step=Vector3.Distance(samples[i].root,samples[i-1].root);
                    continuous&=step<.5f; // Exclude reset/teleport transitions.
                    travelled+=step;
                }
                if(!continuous||travelled<.15f||samples[samples.Count-1].stride-samples[0].stride<.1f)
                {
                    // Retry on a later real movement burst; never label a stationary clip running.
                    captured.Remove(name);
                    for(int frame=0;frame<16;frame++)File.Delete(Path.Combine(dir,name+"-"+frame.ToString("D2")+".png"));
                    File.Delete(Path.Combine(dir,name+".png"));
                    recording=false;
                    yield break;
                }
            }
            File.WriteAllText(Path.Combine(dir,name+"-timeline.json"),JsonUtility.ToJson(new Timeline{samples=samples.ToArray()},true));
            recording=false;
        }
        [Serializable]private sealed class Timeline{public Sample[] samples;}
        [Serializable]private sealed class Sample{public int frame;public float clock,swingAge,speed,stride,armStretch;public uint swingId;public bool swinging,serve;public string stroke;public Vector3 root;}
        [Serializable]private sealed class Report{public string nearId,farId;public bool passed,capturedServe,capturedRunning;public int errors,contacts,serves,strokes;public float maxContactError,maxPlantedError,maxPlantedSlipPerFrame,maxGripError,maxArmStretch;public string scope;}
        private sealed class Probe:BufferedPlayerInputSource
        {
            private readonly TennisMatchController c;private readonly StrokeKind wanted;private readonly bool stress,serveOnly;private uint sequence;private double before;private int serves;
            public Probe(TennisMatchController value,StrokeKind stroke,bool movement=false,bool serviceOnly=false):base("Motion verification (CLI only)"){c=value;wanted=stroke;stress=movement;serveOnly=serviceOnly;}
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                if(now-before<.08)return;CourtSide side=c.Match.Server;PlayerAction action;
                if(c.ReadyToServe){action=PlayerAction.Serve;serves++;c.GetComponent<MotionValidationDriver>().serves++;}
                else
                {
                    if(serveOnly)return;
                    side=TennisMatch.Opponent(c.Match.LastHitter);if(!c.CanReturn(side))return;
                    StrokeKind stroke=wanted;
                    if(wanted==StrokeKind.Volley&&c.Ball.Simulation.BounceCount!=0)stroke=StrokeKind.Lob;
                    if(wanted==StrokeKind.Smash&&c.Ball.Simulation.Position.y<c.Tuning["hit.smash_height"])
                    {if(c.LastHit.HasValue&&c.LastHit.Value.Stroke==StrokeKind.Lob)return;stroke=StrokeKind.Lob;}
                    action=stroke==StrokeKind.Backhand?PlayerAction.Backhand:stroke==StrokeKind.Smash?PlayerAction.Smash:stroke==StrokeKind.Volley?PlayerAction.Volley:stroke==StrokeKind.Lob?PlayerAction.Lob:stroke==StrokeKind.Slice?PlayerAction.Slice:PlayerAction.Forehand;
                }
                before=now;float facing=c.Match.EndOf(side)==CourtSide.Near?1:-1;
                float aim=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*1.2f*facing,17)*Mathf.Rad2Deg/c.Tuning["shot.yaw"],-1,1);
                if(stress)aim=Mathf.Clamp(aim+Mathf.Sin(c.Hits*1.7f)*.2f,-1,1);
                Enqueue(new PlayerInputSample(action,++sequence,now,.35f,aim,0,side==CourtSide.Far));
            }
        }
    }
}
