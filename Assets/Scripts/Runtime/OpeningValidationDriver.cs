using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Opt-in game-camera-only capture of the natural opening, then skip/replay safety checks.</summary>
    public sealed class OpeningValidationDriver:MonoBehaviour
    {
        [Serializable]private sealed class Report{public bool passed,automaticTitle,skipRestored,replayRestored;public int frames,runtimeErrors;public ulong ticksBefore,ticksAfter;public long points;public float timeScale;public string[] shots;public float[] times;}
        private int errors;
        private void Error(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-intro-verify");if(flag<0||flag+1>=args.Length)yield break;
            string dir=args[flag+1];Directory.CreateDirectory(dir);Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Error;
            var f=GetComponent<FrontEndController>();while(!f.Ready)yield return null;var c=f.Match;ulong before=c.Ball.PhysicsTicks;uint shot=c.Match.CurrentShot.ShotId;float scale=Time.timeScale;
            var shots=new HashSet<string>();var times=new List<float>();int frames=0;float next=0;
            while(f.Model.Stage==FrontEndStage.Logo)
            {
                yield return new WaitForEndOfFrame();if(f.Model.Stage!=FrontEndStage.Logo)break;
                if(f.StageAge>=next){times.Add(f.StageAge);f.Opening.Render();f.RenderUiNow();GameCameraCapture.Save(c.View.Camera,Path.Combine(dir,$"frame-{frames:0000}.png"),1600,900,f.UiCamera);shots.Add(f.Opening.ShotId);frames++;next=f.StageAge+1f/15;}
            }
            bool automatic=f.Model.Stage==FrontEndStage.Title&&!f.Opening.Active;
            f.ReplayOpening();yield return null;bool replay=f.Opening.Active;
            c.InputAdapter.Press(PlayerAction.MenuBack);yield return null;bool skip=f.Model.Stage==FrontEndStage.Title&&!f.Opening.Active&&c.NearAvatar.GetComponentInChildren<Renderer>().forceRenderingOff==false;
            bool pass=automatic&&replay&&skip&&errors==0&&shots.Count>=8&&before==c.Ball.PhysicsTicks&&c.Match.CurrentShot.ShotId==shot&&c.Match.Progress.TotalPoints==0&&!c.InputAdapter.GameplayEnabled&&Time.timeScale==scale;
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonUtility.ToJson(new Report{passed=pass,automaticTitle=automatic,replayRestored=replay,skipRestored=skip,frames=frames,runtimeErrors=errors,ticksBefore=before,ticksAfter=c.Ball.PhysicsTicks,points=c.Match.Progress.TotalPoints,timeScale=Time.timeScale,shots=new List<string>(shots).ToArray(),times=times.ToArray()},true));
            Application.logMessageReceived-=Error;Application.Quit(pass?0:1);
        }
    }
}
