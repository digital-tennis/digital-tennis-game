using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
namespace DigitalTennis
{
    /// <summary>Opt-in local measurements. No identifiers, tokens or network addresses are recorded.</summary>
    public sealed class PolishMetrics : MonoBehaviour
    {
        public static int CaptureUntilFrame;
        public static void BeforeCapture(){CaptureUntilFrame=Time.frameCount+3;}
        private TennisMatchController c;private StreamWriter points,swings;private string directory;
        private readonly List<float> frames=new List<float>(12000);
        private object observed;private long total;private int serial,peakRenderers;private long peakBytes;private bool failed;
        private double pointStart;private int previousHits;
        private RenderTexture target;private Camera measuredCamera;private RenderTexture oldTarget;
        private void Start()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-dt-metrics-dir");
            if(i<0||i+1>=args.Length){enabled=false;return;}
            try{directory=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(directory);
                points=new StreamWriter(Path.Combine(directory,"points.csv"));swings=new StreamWriter(Path.Combine(directory,"swings.csv"));
                points.WriteLine("match,mode,difficulty,near,far,total_points,near_points,far_points,rally_hits,point_seconds,near_energy,far_energy,quality");
                swings.WriteLine("match,player,character,stroke,result,power,aim_x,aim_z,ball_speed");
                c=GetComponent<TennisMatchController>();c.SwingEvaluated+=OnSwing;
                measuredCamera=c.View.Camera;
                if(Application.isBatchMode&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {oldTarget=measuredCamera.targetTexture;target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);target.Create();measuredCamera.targetTexture=target;}
            }catch(Exception e){Fail(e);}
        }
        private static string N(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        private void OnSwing(RacketInput input,SwingResult result)
        {
            if(failed)return;try{swings.WriteLine(string.Join(",",serial,input.Player,CharacterCatalog.Ids[c.Characters.Player(input.Player).Index],input.Stroke,result,N(input.Power01),N(input.AimX),N(input.AimZ),N(c.Ball.Simulation.Velocity.magnitude)));}
            catch(Exception e){Fail(e);}
        }
        private void LateUpdate()
        {
            if(c?.Match==null||failed)return;
            if(!ReferenceEquals(observed,c.Match)){observed=c.Match;serial++;total=0;previousHits=0;pointStart=Time.realtimeSinceStartupAsDouble;}
            var s=c.GetComponent<OnlineTennisSession>();bool playing=FrontEndController.Active==null||FrontEndController.Active.Model.Stage==FrontEndStage.Playing;
            if(playing&&!c.IsPaused&&Time.frameCount>CaptureUntilFrame)
            {if(frames.Count<12000)frames.Add(Time.unscaledDeltaTime*1000);peakBytes=Math.Max(peakBytes,Profiler.GetTotalAllocatedMemoryLong());}
            if(Time.frameCount%120==0)peakRenderers=Math.Max(peakRenderers,FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length);
            if(!c.Ball.IsAuthoritative)return;
            long next=c.Match.Progress.TotalPoints;
            if(next>total)
            {
                try{points.WriteLine(string.Join(",",serial,c.Mode,c.AiDifficulty,CharacterCatalog.Ids[c.Characters.Player(CourtSide.Near).Index],CharacterCatalog.Ids[c.Characters.Player(CourtSide.Far).Index],next,c.Match.Progress.NearTotalPoints,c.Match.Progress.FarTotalPoints,c.Hits-previousHits,N(Time.realtimeSinceStartupAsDouble-pointStart),N(c.Characters.Player(CourtSide.Near).Energy),N(c.Characters.Player(CourtSide.Far).Energy),GamePresentationQuality.Current));points.Flush();swings.Flush();}
                catch(Exception e){Fail(e);}
                total=next;previousHits=c.Hits;pointStart=Time.realtimeSinceStartupAsDouble;
            }
        }
        private void Fail(Exception e){failed=true;Debug.LogWarning("Local metrics stopped: "+e.GetType().Name);}
        private void OnDestroy(){Save();if(target!=null){if(measuredCamera!=null)measuredCamera.targetTexture=oldTarget;target.Release();Destroy(target);}}
        private void OnApplicationQuit(){Save();}
        private void Save()
        {
            if(c!=null)c.SwingEvaluated-=OnSwing;points?.Dispose();swings?.Dispose();points=swings=null;
            if(directory==null)return;
            try{frames.Sort();float P(float p)=>frames.Count==0?0:frames[Mathf.Clamp((int)Math.Ceiling(frames.Count*p)-1,0,frames.Count-1)];
                File.WriteAllText(Path.Combine(directory,"performance.json"),JsonUtility.ToJson(new Report{samples=frames.Count,medianMs=P(.5f),p95Ms=P(.95f),p99Ms=P(.99f),over33Ms=frames.FindAll(x=>x>33.333f).Count,peakAllocatedBytes=peakBytes,rendererObjects=peakRenderers,quality=GamePresentationQuality.Current.ToString(),device=SystemInfo.graphicsDeviceName,resolution=(measuredCamera!=null?measuredCamera.pixelWidth:Screen.width)+"x"+(measuredCamera!=null?measuredCamera.pixelHeight:Screen.height),batch=Application.isBatchMode,scope="wall frame time during Playing, capture frames excluded; not GPU timings or Windows evidence"},true));}
            catch(Exception e){Debug.LogWarning("Local metrics save failed: "+e.GetType().Name);}directory=null;
        }
        [Serializable]private sealed class Report{public int samples,over33Ms,rendererObjects;public float medianMs,p95Ms,p99Ms;public long peakAllocatedBytes;public string quality,device,resolution,scope;public bool batch;}
    }
}
