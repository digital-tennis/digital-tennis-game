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
            bool audioTest=front&&FrontEndController.HasArgument("-dt-audio-validation");
            var audio=GetComponent<TennisAudio>();
            if(audioTest){double audioEnd=Time.realtimeSinceStartupAsDouble+30;while(!audio.Ready&&Time.realtimeSinceStartupAsDouble<audioEnd)yield return null;if(!audio.Ready)throw new InvalidOperationException("Audio bank failed to load: "+audio.Status);}
            if(front)
            {
                while(FrontEndController.Active?.Ready!=true)yield return null;
                var f=FrontEndController.Active;if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();
                Click("continue");
                if(audioTest)
                {
                    Click("menu.options");
                    for(int q=0;q<3;q++){Click("graphics."+q);if((int)GamePresentationQuality.Current!=q||venue.transform.Find("Seated Audience").GetComponent<Renderer>().enabled!=(q!=0))throw new InvalidOperationException("Graphics option or crowd visibility failed.");}
                    Click("audio.options");yield return new WaitForSeconds(.4f);Capture("audio-options","actual audio settings");
                    f.View.Sliders["audio.volume.0"].value=.55f;
                    if(Mathf.Abs(audio.Master-.55f)>.001f)throw new InvalidOperationException("Master volume slider failed.");
                    Click("audio.variant.0.1");yield return new WaitForSeconds(.3f);Click("audio.variant.0.0");
                    audio.Audition("deuce_a");yield return new WaitForSeconds(1.2f);if(audio.CurrentMusic!="deuce_a")throw new InvalidOperationException("Deuce music did not play");
                    audio.Audition("pressure_b");audio.Play("KOR.skill_alt");yield return new WaitForSeconds(1.2f);if(audio.CurrentMusic!="pressure_b")throw new InvalidOperationException("Pressure music did not play");
                    Click("back");Click("back");
                }
                Click("menu.single");Click("country.KOR");Click("confirm");Click("country.JPN");Click("confirm");Click("country.KOR");Click("confirm");
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
                    if(side==CourtSide.Near)
                    {
                        var intro=new VenueCameraDirector();intro.Present(c.View.Camera,side,true,false,.3f);
                        Capture(CharacterCatalog.Ids[i]+"-venue","staged view of the actual versus venue camera");intro.Restore();
                    }
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
            var voiceEvidence=new System.Collections.Generic.List<VoiceFrame>();
            if(audioTest&&FrontEndController.HasArgument("-dt-audio-upgrade-verify"))
            {
                var output=Camera.main.GetComponent<AudioOutputLimiter>();
                // Explicit speech auditions while the real match waits for normal serve input.
                // This does not fabricate a contact, stock, point, or skill activation.
                foreach(string id in (FrontEndController.HasArgument("-dt-audio-existing-verify")?new[]{"KOR","JPN"}:CharacterCatalog.Ids))
                foreach(string cue in new[]{"ready","skill","win","lose"})
                {
                    while(audio.VoicePlaying)yield return null;
                    string key=id+"."+cue;output.RestartCapture();
                    if(!audio.Play(key))throw new InvalidOperationException("Speech audition rejected: "+key);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(audio.CurrentVoice!=key||string.IsNullOrWhiteSpace(audio.Caption))throw new InvalidOperationException("Speech/subtitle did not start: "+key);
                    string caption=audio.Caption,file=audio.CueFile(key);
                    if(cue=="ready"){yield return new WaitForEndOfFrame();Capture(id+"-speech-audition","explicit offline synthetic English speech audition; no gameplay event claimed");}
                    while(audio.VoicePlaying)yield return null;
                    if(!output.Save(Path.Combine(dir,key+"-playback.wav")))throw new InvalidOperationException("No listener audio: "+key);
                    voiceEvidence.Add(new VoiceFrame{cue=key,file=file,caption=caption,plays=audio.PlayCount(key)});
                }
                audio.Play("KOR.skill_alt");yield return new WaitForSecondsRealtime(.1f);audio.Play("umpire.out");
                if(audio.CurrentVoice!="umpire.out")throw new InvalidOperationException("Official voice did not interrupt persona speech.");
                while(audio.VoicePlaying)yield return null;
                output.RestartCapture();
            }
            c.InputAdapter.UseSource(new SurveyInput(c));bool rally=false;int quietSamples=0;bool sawCheer=false;
            double end=Time.realtimeSinceStartupAsDouble+60;
            while(Time.realtimeSinceStartupAsDouble<end&&(c.Match.Progress.TotalPoints==0||!rally))
            {
                if(audioTest&&fStagePlaying())
                {
                    if(!audio.MatchMusicMuted||audio.AudibleMusicVolume>0)throw new InvalidOperationException("Music leaked into live tennis.");
                    if(c.Match.Phase!=MatchPhase.PointEnd&&audio.CrowdPlaying)throw new InvalidOperationException("Crowd carried into serve or rally.");
                    quietSamples++;sawCheer|=audio.CrowdPlaying;
                }
                if(!rally&&c.NearHits>=2&&c.FarHits>=2&&c.Match.Phase==MatchPhase.Rally)
                {yield return new WaitForEndOfFrame();Capture("live-rally","real adapter input and AI");rally=true;}
                yield return null;
            }
            yield return new WaitForEndOfFrame();sawCheer|=audioTest&&audio.PlayCount("cheer")>0;Capture("after-point","real point awarded");
            end=Time.realtimeSinceStartupAsDouble+10;
            while(!c.ReadyToServe&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            yield return new WaitForEndOfFrame();Capture("next-serve","real next-serve readiness after the point");
            bool audioPass=true;
            if(audioTest)
            {
                int cheerPlays=audio.PlayCount("cheer");uint revision=audio.Revision;string saved=File.ReadAllText(audio.CsvPath);
                File.WriteAllText(audio.CsvPath,"invalid audio bank");audio.Reload();while(audio.Loading)yield return null;
                bool rejected=audio.Revision==revision&&audio.Ready&&audio.RejectedReloads>0;
                File.WriteAllText(audio.CsvPath,saved);audio.Reload();while(audio.Loading)yield return null;
                var output=Camera.main.GetComponent<AudioOutputLimiter>();
                bool recorded=output.Save(Path.Combine(dir,"game-mix.wav"));
                int steps=0,skids=0,contacts=0;for(int i=0;i<3;i++){steps+=audio.PlayCount("step_"+i);skids+=audio.PlayCount("skid_"+i);contacts+=audio.PlayCount("hit_"+i);}
                bool upgrade=FrontEndController.HasArgument("-dt-audio-upgrade-verify");
                if(upgrade)File.WriteAllText(Path.Combine(dir,"upgrade-result.json"),JsonUtility.ToJson(new UpgradeReport{monitorMuted=Camera.main.GetComponent<AudioOutputLimiter>().MonitorMuted,voices=voiceEvidence.ToArray(),steps=steps,skids=skids,contacts=contacts,korVolume=audio.CueVolume("KOR.ready"),korFile=audio.CueFile("KOR.ready"),jpnFile=audio.CueFile("JPN.ready"),nativePlatform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString()},true));
                audioPass=(!upgrade||(voiceEvidence.Count==(FrontEndController.HasArgument("-dt-audio-existing-verify")?8:60)&&steps>0&&skids>0&&contacts>0))&&rejected&&audio.Revision>revision&&sawCheer&&cheerPlays==1&&quietSamples>10&&!audio.CrowdPlaying&&audio.MatchMusicMuted&&audio.AudibleMusicVolume==0&&audio.Ready&&audio.MusicSwitches>=2&&audio.TotalPlays>=8&&output.ProcessedSamples>0&&recorded;
                File.WriteAllText(Path.Combine(dir,"audio-result.json"),JsonUtility.ToJson(new AudioReport{passed=audioPass,quietSamples=quietSamples,pointCheers=cheerPlays,musicMuted=audio.MatchMusicMuted,musicVolume=audio.AudibleMusicVolume,crowdStoppedAtNextServe=!audio.CrowdPlaying,loaded=audio.LoadedCues,revision=audio.Revision,rejected=audio.RejectedReloads,plays=audio.TotalPlays,musicSwitches=audio.MusicSwitches,advancingSources=audio.AdvancingSources,dspSamples=output.ProcessedSamples,limitedPeak=output.Peak},true));
            }
            bool pass=audioPass&&errors==0&&rally&&c.Match.Progress.TotalPoints>0&&c.ReadyToServe&&venue.SpectatorCount>=400&&venue.GetComponentsInChildren<Collider>(true).Length==0;
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonUtility.ToJson(new Report{passed=pass,nextServeReady=c.ReadyToServe,runtimeErrors=errors,spectators=venue.SpectatorCount,seats=venue.SeatCount,
                nearHits=c.NearHits,farHits=c.FarHits,totalPoints=c.Match.Progress.TotalPoints,physicsTicks=c.Ball.PhysicsTicks,themes=front?1:15,views=front?1:30,colliders=venue.GetComponentsInChildren<Collider>(true).Length},true));
            Application.logMessageReceived-=Error;Application.Quit(pass?0:1);
            bool fStagePlaying()=>FrontEndController.Active?.Model.Stage==FrontEndStage.Playing;
            void Capture(string name,string mode)
            {
                var f=FrontEndController.Active;f?.View.UpdateLive();f?.RenderUiNow();
                GameCameraCapture.Save(Camera.main,Path.Combine(dir,name+".png"),1600,1000,f?.UiCamera);
                File.WriteAllText(Path.Combine(dir,name+".json"),JsonUtility.ToJson(new Frame{mode=mode,localView=c.View.LocalPlayer.ToString(),camera=GamePayload.Pack(Camera.main.transform.position),
                    state=GamePayload.Capture(c,"stadium-survey",0)},true));
            }
        }
        [Serializable] private sealed class VoiceFrame{public string cue,file,caption;public int plays;}
        [Serializable] private sealed class UpgradeReport{public bool monitorMuted;public VoiceFrame[] voices;public int steps,skids,contacts;public float korVolume;public string korFile,jpnFile,nativePlatform,graphics;}
        [Serializable] private sealed class AudioReport{public int quietSamples,pointCheers;public bool musicMuted,crowdStoppedAtNextServe;public float musicVolume;public bool passed;public int loaded,rejected,plays,musicSwitches,advancingSources;public uint revision;public long dspSamples;public float limitedPeak;}
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
