using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Opt-in real input motion survey. Never writes physics, points or stock.</summary>
    public sealed class MotionValidationDriver:MonoBehaviour
    {
        private TennisMatchController c;private string dir;private int errors,contacts,serves;private float maxContact,maxFoot,maxSlip;
        private readonly HashSet<StrokeKind> checkedStrokes=new HashSet<StrokeKind>();private StrokeKind wanted;private bool hitWanted,recording;
        private void Error(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-motion-verify");if(flag<0||flag+1>=args.Length){enabled=false;yield break;}
            dir=args[flag+1];Directory.CreateDirectory(dir);Application.logMessageReceived+=Error;Application.runInBackground=true;yield return null;
            c=GetComponent<TennisMatchController>();c.SwingEvaluated+=Contact;
            foreach(var stroke in new[]{StrokeKind.Forehand,StrokeKind.Backhand,StrokeKind.Volley,StrokeKind.Slice,StrokeKind.Lob,StrokeKind.Smash})
            {
                wanted=stroke;hitWanted=false;c.Configure(GameMode.Practice);c.AutoServe=c.DemoReturns=false;c.IsPaused=false;c.InputAdapter.UseSource(new Probe(c,stroke));
                double until=Time.realtimeSinceStartupAsDouble+45;
                while((!hitWanted||recording)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            }
            c.Configure(GameMode.Practice);c.AutoServe=c.DemoReturns=false;c.IsPaused=false;wanted=StrokeKind.Forehand;hitWanted=true;
            c.InputAdapter.UseSource(new Probe(c,StrokeKind.Forehand,true));int startContacts=contacts;double stressEnd=Time.realtimeSinceStartupAsDouble+60;
            while(contacts-startContacts<20&&Time.realtimeSinceStartupAsDouble<stressEnd)yield return null;
            bool passed=contacts-startContacts>=20&&maxSlip<.035f&&errors==0&&checkedStrokes.Count==6&&contacts>=6&&serves>=6&&maxContact<.035f&&maxFoot<.04f;
            File.WriteAllText(Path.Combine(dir,"motion.json"),JsonUtility.ToJson(new Report{passed=passed,errors=errors,contacts=contacts,serves=serves,strokes=checkedStrokes.Count,maxContactError=maxContact,maxPlantedError=maxFoot,maxPlantedSlipPerFrame=maxSlip,scope="ordinary practice adapter; actual hit/serve anchors; no score or ball writes"},true));
            c.SwingEvaluated-=Contact;Application.logMessageReceived-=Error;Application.Quit(passed?0:1);
        }
        private void Contact(RacketInput input,SwingResult result)
        {
            if(result!=SwingResult.Hit)return;contacts++;
            var avatar=input.Player==CourtSide.Near?c.NearAvatar:c.FarAvatar;
            maxContact=Mathf.Max(maxContact,Vector3.Distance(avatar.StringCenter,c.Ball.Simulation.Position));
            if(input.Stroke==wanted&&!hitWanted){hitWanted=true;checkedStrokes.Add(wanted);StartCoroutine(Capture(avatar,input.Stroke.ToString()));}
        }
        private void LateUpdate()
        {
            if(c==null)return;
            foreach(var a in new[]{c.NearAvatar,c.FarAvatar})
            {var rig=a.GetComponentInChildren<AthleteMotion>();maxFoot=Mathf.Max(maxFoot,rig.PlantedFootError);maxSlip=Mathf.Max(maxSlip,rig.PlantedFootSlip);}
        }
        private IEnumerator Capture(RacketAvatar actor,string name)
        {
            recording=true;yield return new WaitForEndOfFrame();
            var camera=c.View.Camera;Vector3 p=camera.transform.position;Quaternion q=camera.transform.rotation;float fov=camera.fieldOfView;
            Vector3 focus=actor.transform.position+Vector3.up*.2f;
            camera.transform.position=focus+actor.transform.forward*3.4f+Vector3.up*.8f+actor.transform.right*1.5f;camera.transform.LookAt(focus);camera.fieldOfView=40;
            GameCameraCapture.Save(camera,Path.Combine(dir,name+".png"),1600,900);
            camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=fov;recording=false;
        }
        [Serializable]private sealed class Report{public bool passed;public int errors,contacts,serves,strokes;public float maxContactError,maxPlantedError,maxPlantedSlipPerFrame;public string scope;}
        private sealed class Probe:BufferedPlayerInputSource
        {
            private readonly TennisMatchController c;private readonly StrokeKind wanted;private readonly bool stress;private uint sequence;private double before;private int serves;
            public Probe(TennisMatchController value,StrokeKind stroke,bool movement=false):base("Motion verification (CLI only)"){c=value;wanted=stroke;stress=movement;}
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                if(now-before<.08)return;CourtSide side=c.Match.Server;PlayerAction action;
                if(c.ReadyToServe){action=PlayerAction.Serve;serves++;c.GetComponent<MotionValidationDriver>().serves++;}
                else
                {
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
