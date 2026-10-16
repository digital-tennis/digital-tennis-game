using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace DigitalTennis
{
    /// <summary>Local audio only. Confirmed, buffered game events drive sound; audio never changes gameplay.</summary>
    public sealed class TennisAudio : MonoBehaviour
    {
        private sealed class Slot {public AudioSource Source;public AudioCue Cue;public float Gain=1;}
        private readonly TennisVoiceQueue pendingVoices=new TennisVoiceQueue();
        private readonly TennisFootstepTrack[] footsteps={new TennisFootstepTrack(),new TennisFootstepTrack()};
        private readonly Dictionary<string,float> effectCooldown=new Dictionary<string,float>();
        private int stepTake,hitTake;
        private readonly List<Slot> slots=new List<Slot>();
        private readonly Dictionary<string,int> played=new Dictionary<string,int>();
        private readonly TennisEffectGate eventGate=new TennisEffectGate();
        private readonly List<AudioClip> retired=new List<AudioClip>();
        private Dictionary<string,AudioCue> catalog=new Dictionary<string,AudioCue>();
        private Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private Dictionary<string,string> captions=new Dictionary<string,string>();
        private readonly float[] busVolume={.65f,.75f,.65f,.20f};
        private readonly int[] variants=new int[3];
        private Slot[] music,voices,effects;
        private Slot ambience;
        private TennisMatchController controller;
        private TennisMatch boundMatch;
        private string boundRemote,desiredMusic;
        private uint boundEpoch,lastCall;
        private bool callBaseline;
        private long previousPoints;
        private FrontEndStage previousStage;
        private readonly Dictionary<string,int> voiceTakes=new Dictionary<string,int>();
        private int musicIndex=-1;
        private float blend,fadeSeconds=1,duck=1,captionUntil,auditionUntil;
        private string audition;
        private float[] fadeFrom={0,0};
        private bool validation;
        public string CsvPath {get;private set;}
        public string Status {get;private set;}="음원을 준비하고 있습니다.";
        public bool Ready=>clips.Count>0;
        public bool Loading {get;private set;}
        public uint Revision {get;private set;}
        public int MusicSwitches {get;private set;}
        public int TotalPlays {get;private set;}
        public int RejectedReloads {get;private set;}
        public int LoadedCues=>catalog.Count;
        public int AdvancingSources {get;private set;}
        public string CurrentMusic=>musicIndex<0?null:music[musicIndex].Cue?.Id;
        public bool MatchMusicMuted {get;private set;}
        public float AudibleMusicVolume=>music==null?0:music.Sum(s=>s.Source.isPlaying?s.Source.volume:0);
        public bool CrowdPlaying=>effects!=null&&effects.Any(s=>s.Cue?.Id=="cheer"&&s.Source.isPlaying);
        public string Caption {get;private set;}="";
        public bool VoicePlaying=>voices!=null&&voices.Any(s=>s.Source.isPlaying);
        public string CurrentVoice=>voices?.FirstOrDefault(s=>s.Source.isPlaying)?.Cue?.Id;
        public string CueFile(string id)=>catalog.TryGetValue(id,out var cue)?cue.File:null;
        public float CueVolume(string id)=>catalog[id].Volume;
        public bool Captions {get;private set;}=true;
        public float Master {get;private set;}=.8f;
        public float Volume(AudioBus bus)=>busVolume[(int)bus];
        public int Variant(int purpose)=>variants[purpose];
        public int PlayCount(string cue)=>played.TryGetValue(cue,out int count)?count:0;
        private void Awake()
        {
            validation=FrontEndController.HasArgument("-dt-audio-validation");
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-audio-file");
            CsvPath=flag>=0&&flag+1<args.Length?args[flag+1]:Path.Combine(Application.persistentDataPath,"Audio","audio.csv");
            if(!validation)
            {
                Master=Mathf.Clamp01(PlayerPrefs.GetFloat("DigitalTennis.Audio.Master",.8f));
                foreach(AudioBus bus in Enum.GetValues(typeof(AudioBus)))busVolume[(int)bus]=Mathf.Clamp01(PlayerPrefs.GetFloat("DigitalTennis.Audio."+bus,busVolume[(int)bus]));
                Captions=PlayerPrefs.GetInt("DigitalTennis.Audio.Captions",1)!=0;
                for(int i=0;i<3;i++)variants[i]=Mathf.Clamp(PlayerPrefs.GetInt("DigitalTennis.Audio.Variant"+i,0),0,1);
            }
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){Status="오디오 장치가 없는 자동 검증 모드";enabled=false;return;}
            Slot Make(string name)
            {
                var child=new GameObject(name);child.transform.SetParent(transform,false);var source=child.AddComponent<AudioSource>();
                source.playOnAwake=false;source.spatialBlend=0;var slot=new Slot{Source=source};slots.Add(slot);return slot;
            }
            music=new[]{Make("Music A"),Make("Music B")};voices=new[]{Make("Voice 1"),Make("Voice 2")};
            effects=Enumerable.Range(0,12).Select(i=>Make("Effect "+i)).ToArray();ambience=Make("Stadium ambience");
        }
        private IEnumerator Start()
        {
            controller=GetComponent<TennisMatchController>();
            if(Camera.main.GetComponent<AudioOutputLimiter>()==null)Camera.main.gameObject.AddComponent<AudioOutputLimiter>();
            string shipped=Path.Combine(Application.streamingAssetsPath,"Audio");
            Task copy=Task.Run(()=>
            {
                string directory=Path.GetDirectoryName(Path.GetFullPath(CsvPath));Directory.CreateDirectory(directory);
                foreach(string file in Directory.GetFiles(shipped))
                {
                    if(file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))continue;
                    string dest=Path.Combine(directory,Path.GetFileName(file));if(!File.Exists(dest))File.Copy(file,dest,false);
                }
            });
            while(!copy.IsCompleted)yield return null;
            if(copy.IsFaulted)Status="외부 음원을 준비하지 못해 기본 음원을 사용합니다.";
            yield return Load(CsvPath);
            if(!Ready){string issue=Status;yield return Load(Path.Combine(shipped,"audio.csv"));if(Ready)Status="기본 음원을 사용합니다. "+issue;}
        }
        public bool Reload()
        {if(Loading||!enabled)return false;StartCoroutine(Load(CsvPath));return true;}
        private IEnumerator Load(string path)
        {
            Loading=true;string problem=null;Dictionary<string,AudioCue> next=null;var nextClips=new Dictionary<string,AudioClip>();var nextCaptions=new Dictionary<string,string>();
            try
            {
                string factoryCsv=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Audio","audio.csv"));
                next=AudioCatalog.WithOptionalDefaults(File.ReadAllText(path),factoryCsv);
                string manifest=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Audio","factory-legacy.json"));
                var upgraded=AudioFactoryUpgrade.Apply(next,AudioCatalog.Parse(factoryCsv),manifest,Path.GetDirectoryName(Path.GetFullPath(path)));
                var shippedRows=CharacterBalanceCsv.ReadRows(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Audio","subtitles.csv")));
                foreach(var row in shippedRows.Skip(1))if(row.Count==3&&row[1]=="ko")nextCaptions[row[0]]=row[2];
                string subs=Path.Combine(Path.GetDirectoryName(path),"subtitles.csv");
                if(File.Exists(subs))
                {
                    AudioFactoryUpgrade.MergeCaptions(nextCaptions,File.ReadAllText(subs),manifest,upgraded);
                }
            }
            catch(Exception e){problem=e.Message;}
            if(problem==null)
            {
                long bytes=0;
                foreach(var cue in next.Values)
                {
                    string file=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)),cue.File);
                    if(cue.FactoryFile)file=Path.Combine(Application.streamingAssetsPath,"Audio",cue.File);
                    try{var info=new FileInfo(file);if(!info.Exists||info.Length>32*1024*1024)throw new IOException("Missing or oversized audio file: "+cue.File);bytes+=info.Length;if(bytes>128*1024*1024)throw new IOException("Audio bank exceeds 128 MB.");}
                    catch(Exception e){problem=e.Message;break;}
                    if(nextClips.ContainsKey(cue.File))continue;
                    using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri,Path.GetExtension(file).ToLowerInvariant()==".ogg"?AudioType.OGGVORBIS:AudioType.WAV))
                    {
                        yield return request.SendWebRequest();
                        if(request.result!=UnityWebRequest.Result.Success){problem="Cannot read audio file: "+cue.File;break;}
                        var clip=DownloadHandlerAudioClip.GetContent(request);
                        if(clip==null||clip.length<=0||clip.length>120){if(clip!=null)Destroy(clip);problem="Audio duration is invalid: "+cue.File;break;}
                        nextClips.Add(cue.File,clip);
                    }
                }
            }
            Loading=false;
            if(problem!=null)
            {
                foreach(var clip in nextClips.Values)Destroy(clip);
                RejectedReloads++;Status="이전 음원을 유지합니다. "+problem;yield break;
            }
            retired.AddRange(clips.Values);catalog=next;clips=nextClips;captions=nextCaptions;Revision++;
            Status="오디오 "+catalog.Count+"개를 읽었습니다. (revision "+Revision+")";
            string current=desiredMusic;desiredMusic=null;
            if(current!=null)Music(current);
            if(ambience.Source.isPlaying&&catalog.TryGetValue("ambience",out var air))Begin(ambience,air,clips[air.File]);
        }
        public void SetMaster(float value){Master=Mathf.Clamp01(value);Save("Master",Master);}
        public void SetVolume(AudioBus bus,float value){busVolume[(int)bus]=Mathf.Clamp01(value);Save(bus.ToString(),busVolume[(int)bus]);}
        private void Save(string name,float value){if(validation)return;PlayerPrefs.SetFloat("DigitalTennis.Audio."+name,value);PlayerPrefs.Save();}
        public void SetCaptions(bool value){Captions=value;if(!validation){PlayerPrefs.SetInt("DigitalTennis.Audio.Captions",value?1:0);PlayerPrefs.Save();}}
        public void SetVariant(int purpose,int variant)
        {if(purpose<0||purpose>2)return;variants[purpose]=Mathf.Clamp(variant,0,1);if(!validation){PlayerPrefs.SetInt("DigitalTennis.Audio.Variant"+purpose,variants[purpose]);PlayerPrefs.Save();}}
        private void Begin(Slot slot,AudioCue cue,AudioClip clip)
        {slot.Cue=cue;slot.Source.clip=clip;slot.Source.loop=cue.Loop;slot.Source.volume=0;slot.Source.Play();TotalPlays++;played[cue.Id]=PlayCount(cue.Id)+1;}
        public bool Play(string id)
        {
            if(!Ready||!catalog.TryGetValue(id,out var cue)||!clips.TryGetValue(cue.File,out var clip))return false;
            if(cue.Bus==AudioBus.Music)return Audition(id);
            if(cue.Bus==AudioBus.Voice)
            {
                if(!pendingVoices.Enqueue(id,Time.unscaledTime))return false;
                PumpVoice();return true;
            }
            if(effectCooldown.TryGetValue(id,out float until)&&Time.unscaledTime<until)return false;
            effectCooldown[id]=Time.unscaledTime+.055f;
            var pool=cue.Bus==AudioBus.Voice?voices:effects;
            var slot=pool.FirstOrDefault(s=>!s.Source.isPlaying)??pool.OrderByDescending(s=>s.Source.time).First();
            Begin(slot,cue,clip);
            if(cue.Bus==AudioBus.Voice){Caption=captions.TryGetValue(id,out string line)?line:"";captionUntil=Time.unscaledTime+Mathf.Clamp(clip.length+.4f,1.5f,5);}
            return true;
        }
        private void PumpVoice()
        {
            var active=voices.FirstOrDefault(s=>s.Source.isPlaying);
            string id=pendingVoices.Take(Time.unscaledTime,active==null?0:TennisVoiceQueue.Priority(active.Cue.Id));
            if(id==null)return;
            if(active!=null)active.Source.Stop();
            var cue=catalog[id];var clip=clips[cue.File];Begin(voices[0],cue,clip);
            Caption=captions.TryGetValue(id,out string line)?line:"";
            captionUntil=Time.unscaledTime+clip.length+.4f;
        }
        public bool Audition(string id)
        {
            if(!catalog.TryGetValue(id,out var cue)||!clips.TryGetValue(cue.File,out var clip))return false;
            if(cue.Bus!=AudioBus.Music)return Play(id);
            if(!TennisAudioPolicy.MusicAllowed(FrontEndController.Active?.Model.Stage??FrontEndStage.Playing))return false;
            audition=id;auditionUntil=Time.unscaledTime+Mathf.Min(40,clip.length);Music(id);return true;
        }
        private void Music(string id)
        {
            if(desiredMusic==id||!catalog.TryGetValue(id,out var cue)||!clips.TryGetValue(cue.File,out var clip))return;
            int nextIndex=musicIndex<0?0:1-musicIndex;
            fadeFrom=new[]{music[0].Gain,music[1].Gain};if(musicIndex<0)fadeFrom[0]=fadeFrom[1]=0;
            Begin(music[nextIndex],cue,clip);musicIndex=nextIndex;desiredMusic=id;blend=0;fadeSeconds=Mathf.Max(.05f,cue.Fade);MusicSwitches++;
        }
        private void PersonaVoice(string id)
        {
            int take=voiceTakes.TryGetValue(id,out int value)?value:0;voiceTakes[id]=take+1;
            Play(take%2==1&&catalog.ContainsKey(id+"_alt")?id+"_alt":id);
        }
        private void Effect(TennisEffectEvent item)
        {
            if(item.age>.22f)return;
            string[] names={"serve","hit","bounce","charge","skill"};
            Play(item.kind==(int)TennisEffectKind.Hit&&catalog["hit"].File=="court_hit_0_v2.wav"?"hit_"+(hitTake++%3):names[item.kind]);
            if(item.kind==(int)TennisEffectKind.Skill)PersonaVoice(CharacterCatalog.Ids[item.character]+".skill");
        }
        private void LateUpdate()
        {
            if(!Ready||controller?.Match==null)return;
            var front=FrontEndController.Active;var stage=front?.Model.Stage??FrontEndStage.Playing;
            int purpose=stage==FrontEndStage.Results?2:stage==FrontEndStage.Playing||stage==FrontEndStage.Pause?1:0;
            var session=controller.GetComponent<OnlineTennisSession>();var remote=controller.Ball.IsAuthoritative?null:session?.PresentedState;
            var tally=remote?.score??MatchScoreState.Capture(controller.Match.Progress);var scorePoints=remote?.points??new[]{controller.Match.Score.NearPoints,controller.Match.Score.FarPoints};
            var mood=TennisMusicSituation.Classify(tally,scorePoints);
            MatchMusicMuted=!TennisAudioPolicy.MusicAllowed(stage);
            if(MatchMusicMuted) {foreach(var slot in music){slot.Source.Stop();slot.Source.volume=0;}desiredMusic=null;musicIndex=-1;}
            string musicName=purpose==1?(mood==TennisMusicMood.Deuce?"deuce":mood==TennisMusicMood.Pressure?"pressure":"game"):purpose==2?"result":"menu";
            string sceneMusic=musicName+(variants[purpose]==0?"_a":"_b");
            if(!MatchMusicMuted)Music(stage==FrontEndStage.Audio&&Time.unscaledTime<auditionUntil?audition:sceneMusic);
            if(stage!=previousStage)
            {
                var own=controller.Characters.Player(controller.View.LocalPlayer);
                if(stage==FrontEndStage.Versus)PersonaVoice(CharacterCatalog.Ids[own.Index]+".ready");
                if(stage==FrontEndStage.Results)PersonaVoice(CharacterCatalog.Ids[own.Index]+(front.Winner==controller.View.LocalPlayer?".win":".lose"));
                previousStage=stage;
            }
            string phase=remote?.phase??controller.Match.Phase.ToString();
            bool stadium=TennisAudioPolicy.CrowdAllowed(stage,phase);
            if(!stadium)foreach(var slot in effects)if(slot.Cue?.Id=="cheer")slot.Source.Stop();
            if(stadium&&!ambience.Source.isPlaying&&catalog.TryGetValue("ambience",out var air))Begin(ambience,air,clips[air.File]);
            if(!stadium&&ambience.Source.isPlaying)ambience.Source.Stop();
            uint epoch=session?.Epoch??0;string remoteId=remote?.matchId;
            if(boundMatch!=controller.Match||boundRemote!=remoteId||boundEpoch!=epoch)
            {boundMatch=controller.Match;boundRemote=remoteId;boundEpoch=epoch;pendingVoices.Reset();effectCooldown.Clear();eventGate.Reset();callBaseline=false;previousPoints=remote?.score?.totalPoints.Sum()??controller.Match.Progress.TotalPoints;}
            PumpVoice();
            bool moving=stage==FrontEndStage.Playing&&!controller.IsPaused&&!controller.Characters.Cinematic;
            for(int i=0;i<2;i++)
            {
                var avatar=i==0?controller.NearAvatar:controller.FarAvatar;
                int sound=footsteps[i].Observe(avatar.transform.position,Time.unscaledDeltaTime,Time.unscaledTime,moving);
                if(sound!=0)Play((sound==2?"skid_":"step_")+(stepTake++%3));
            }
            string key=remoteId??"offline";
            eventGate.Consume(key,remote?.effectSequence??controller.Effects.Sequence,remote?.effects??controller.Effects.Capture(),0,Effect);
            uint call=remote?.officials?.callId??controller.Match.CallId;
            if(!callBaseline){lastCall=call;callBaseline=true;}
            else if(Sequence32.IsNewer(call,lastCall))
            {
                lastCall=call;string ruling=remote!=null?((UmpireCall)remote.officials.call).ToString():controller.Match.Call.ToString();
                string cue=ruling=="DoubleFault"?"double_fault":ruling=="Fault"?"fault":ruling=="Let"?"let":ruling=="Out"?"out":ruling=="Game"?"game":ruling=="Set"?"set":ruling=="Match"?"match":null;
                if(cue!=null)Play("umpire."+cue);

            }
            long points=remote?.score?.totalPoints.Sum()??controller.Match.Progress.TotalPoints;
            if(points>previousPoints){if(stadium)Play("cheer");if(TennisMusicSituation.MatchPoint(tally,scorePoints))Play("umpire.match_point");else if(mood==TennisMusicMood.Deuce)Play("umpire.deuce");}previousPoints=points;
            blend=Mathf.Clamp01(blend+Time.unscaledDeltaTime/fadeSeconds);
            float targetDuck=voices.Any(s=>s.Source.isPlaying)?.38f:stage==FrontEndStage.Pause?.45f:stage==FrontEndStage.Playing&&controller.IsPaused?.35f:1;
            duck=Mathf.MoveTowards(duck,targetDuck,Time.unscaledDeltaTime*3);
            for(int i=0;i<2;i++)
            {
                music[i].Gain=Mathf.Lerp(fadeFrom[i],i==musicIndex?1:0,Mathf.SmoothStep(0,1,blend));
                if(blend>=1&&i!=musicIndex)music[i].Source.Stop();
            }
            foreach(var slot in slots)
                if(slot.Cue!=null)slot.Source.volume=(MatchMusicMuted&&slot.Cue.Bus==AudioBus.Music?0:1)*Master*busVolume[(int)slot.Cue.Bus]*slot.Cue.Volume*(slot.Cue.Bus==AudioBus.Music?slot.Gain*duck:1);
            AdvancingSources=slots.Count(s=>s.Source.isPlaying&&s.Source.timeSamples>0);
            if(Time.unscaledTime>captionUntil)Caption="";
            for(int i=retired.Count-1;i>=0;i--)
                if(!slots.Any(s=>s.Source.isPlaying&&s.Source.clip==retired[i])){Destroy(retired[i]);retired.RemoveAt(i);}
        }
        private void OnDestroy(){foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);foreach(var clip in retired)if(clip!=null)Destroy(clip);}
    }
}
