using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalTennis
{
    [DefaultExecutionOrder(100)]
    public sealed class TennisEffects : MonoBehaviour
    {
        public const int PoolCapacity=12;
        [SerializeField] private Material particles, rings;
        private TennisMatchController controller;
        private readonly TennisEffectJournal journal=new TennisEffectJournal();
        private readonly TennisEffectGate gate=new TennisEffectGate();
        private readonly List<Slot> pool=new List<Slot>();
        private readonly HashSet<uint> playedIds=new HashSet<uint>();
        private readonly Queue<uint> playedOrder=new Queue<uint>();
        private readonly float[] chargedAt={-100,-100};
        private string remoteMatch;
        private float clock,trailUntil;
        private TrailRenderer trail;
        private Gradient originalTrail;
        private float originalWidth;
        private DateTime effectsWrite;
        private bool validationReloaded;
        public TennisEffectStore Store { get; private set; }
        public uint Sequence=>journal.Sequence;
        public int[] PresentedByKind { get; }=new int[5];
        public int RenderedEvents { get; private set; }
        public int DuplicateErrors { get; private set; }
        public int PeakParticles { get; private set; }
        public int PoolReuses { get; private set; }
        public int RemoteBaselines=>gate.Baselines;
        public int ActiveEffects { get; private set; }
        public TennisEffectKind? ActiveKind { get; private set; }
        public int LiveParticles { get; private set; }
        public int CreatedSlots=>pool.Count;
        public bool HasActive(TennisEffectKind kind,float from,float until)=>pool.Exists(s=>s.Active&&s.Kind==kind&&s.Age>=from&&s.Age<=until);
        public TennisEffectMetrics Metrics()=>new TennisEffectMetrics {kinds=(int[])PresentedByKind.Clone(),rendered=RenderedEvents,
            duplicateErrors=DuplicateErrors,peakParticles=PeakParticles,poolCreated=pool.Count,poolReuses=PoolReuses,remoteBaselines=RemoteBaselines,
            csvRevision=Store.Revision,hitCount=Store[TennisEffectKind.Hit].Count,status=Store.Status};
        public float Clock=>clock;
        public EffectQuality Quality=>FrontEndController.HasArgument("-dt-effects")?EffectQuality.Rich:(EffectQuality)Mathf.Clamp(PlayerPrefs.GetInt("DigitalTennis.EffectQuality",1),0,2);
        public void Initialize(TennisMatchController owner)
        {
            controller=owner;var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-effects-file");
            string path=flag>=0&&flag+1<args.Length?args[flag+1]:Path.Combine(Application.persistentDataPath,"Effects","effects.csv");
            Store=new TennisEffectStore(path,File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Effects","effects.csv")));
            effectsWrite=File.GetLastWriteTimeUtc(path);
            trail=owner.Ball.GetComponent<TrailRenderer>();if(trail!=null){originalTrail=trail.colorGradient;originalWidth=trail.widthMultiplier;}
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null && particles!=null && rings!=null)
                for(int i=0;i<PoolCapacity;i++)pool.Add(new Slot(transform,particles,rings,i));
        }
        public TennisEffectEvent[] Capture()=>journal.Capture(clock);
        public bool Reload()=>Store!=null&&Store.Reload();
        public void SetQuality(EffectQuality value) {PlayerPrefs.SetInt("DigitalTennis.EffectQuality",(int)value);PlayerPrefs.Save();ClearVisuals();}
        public float ChargePulse(CourtSide side)=>Mathf.Clamp01(1-(clock-chargedAt[(int)side])/.65f);
        public void ResetMatch()
        {journal.Clear();gate.Reset();clock=0;playedIds.Clear();playedOrder.Clear();chargedAt[0]=chargedAt[1]=-100;ClearVisuals();}
        public void BeginRemoteEpoch() {gate.Reset();ClearVisuals();}
        public void ClearVisuals()
        {foreach(var s in pool)s.Clear();ActiveEffects=LiveParticles=0;ActiveKind=null;chargedAt[0]=chargedAt[1]=-100;RestoreTrail();trail?.Clear();}
        private void RestoreTrail() {trailUntil=0;if(trail!=null){trail.widthMultiplier=originalWidth;trail.colorGradient=originalTrail;}}
        public void Record(TennisEffectKind kind,CourtSide side,Vector3 position,Vector3 direction,StrokeKind stroke=0)
        {
            if(controller==null||!controller.Ball.IsAuthoritative)return;
            var item=journal.Add(kind,side,controller.Characters.Player(side).Index,stroke,position,direction,clock);
            Play(item);
        }
        public void Charge(CourtSide side,int before)
        {if(controller.Characters.Player(side).Meter.Stock>before)Record(TennisEffectKind.Charge,side,Avatar(side)+Vector3.up*1.35f,Vector3.up);}
        public Vector3 Avatar(CourtSide side)=>(side==CourtSide.Near?controller.NearAvatar:controller.FarAvatar).transform.position;
        public void PresentRemote(string matchId,uint sequence,TennisEffectEvent[] events,float age)
        {
            if(controller.Ball.IsAuthoritative)return;
            if(remoteMatch!=matchId){remoteMatch=matchId;playedIds.Clear();playedOrder.Clear();ClearVisuals();}
            gate.Consume(matchId,sequence,events,age,Play);
        }
        private void Play(TennisEffectEvent item)
        {
            var kind=(TennisEffectKind)item.kind;var preset=Store[kind];
            if(item.age>=preset.Duration)return;
            if(!playedIds.Add(item.id)){DuplicateErrors++;return;}
            playedOrder.Enqueue(item.id);if(playedOrder.Count>512)playedIds.Remove(playedOrder.Dequeue());
            PresentedByKind[item.kind]++;
            if(!preset.Enabled)return;
            if(kind==TennisEffectKind.Charge)chargedAt[item.player]=clock-item.age;
            if((kind==TennisEffectKind.Serve||kind==TennisEffectKind.Hit)&&trail!=null)
            {
                bool strong=kind==TennisEffectKind.Serve||item.stroke==(int)StrokeKind.Smash;
                trail.widthMultiplier=originalWidth*(strong?1.45f:1.1f);trailUntil=clock+.25f-item.age;
            }
            if(pool.Count==0)return;
            Slot slot=pool.Find(s=>!s.Active);
            if(slot==null){slot=pool[0];foreach(var s in pool)if(s.Age>slot.Age)slot=s;PoolReuses++;}
            float quality=Quality==EffectQuality.Low?.45f:Quality==EffectQuality.Rich?1.5f:1;
            bool reduced=FrontEndController.Active?.ReduceMotion??false;
            slot.Start(item,preset,quality,reduced,CharacterCatalog.Get(controller.Characters.CourtIndex).surface);
            RenderedEvents++;
        }
        private void Update()
        {
            if(controller==null)return;
            // The explicit CLI proof uses an isolated CSV and the normal local F5 route.
            if(FrontEndController.HasArgument("-dt-effects")&&!validationReloaded&&File.GetLastWriteTimeUtc(Store.Path)>effectsWrite&&controller.InputAdapter.GameplayEnabled&&!controller.IsPaused)
            {validationReloaded=true;controller.InputAdapter.Press(PlayerAction.ReloadBalance);}
            float dt=controller.IsPaused?0:Time.deltaTime;clock+=dt;
            ActiveEffects=LiveParticles=0;ActiveKind=null;
            foreach(var s in pool)
            {
                if(!s.Active)continue;s.Advance(dt);if(!s.Active)continue;
                ActiveEffects++;LiveParticles+=s.ParticleCount;ActiveKind=s.Kind;
            }
            PeakParticles=Mathf.Max(PeakParticles,LiveParticles);
            if(trailUntil>0&&clock>=trailUntil)RestoreTrail();
        }
        private void OnDestroy() {foreach(var s in pool)s.Dispose();}

        private sealed class Slot
        {
            private readonly GameObject root;
            private readonly ParticleSystem ps;
            private readonly LineRenderer ring;
            private TennisEffectEvent item;
            private TennisEffectPreset preset;
            private Color tint;
            private float amount,scale;
            private bool released;
            private Vector3 center;
            public float Age { get; private set; }
            public bool Active { get; private set; }
            public TennisEffectKind Kind=>(TennisEffectKind)item.kind;
            public int ParticleCount=>ps.particleCount;
            public Slot(Transform parent,Material material,Material ringMaterial,int index)
            {
                root=new GameObject("Pooled Tennis Effect "+index);root.transform.SetParent(parent,false);
                ps=root.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=128;main.startLifetime=1.2f;
                main.gravityModifier=0;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var color=ps.colorOverLifetime;color.enabled=true;color.color=new ParticleSystem.MinMaxGradient(new Gradient {colorKeys=new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},alphaKeys=new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}});
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                ps.useAutoRandomSeed=false;
                ring=new GameObject("Contact Ring").AddComponent<LineRenderer>();ring.transform.SetParent(root.transform,false);ring.sharedMaterial=ringMaterial;
                ring.useWorldSpace=true;ring.positionCount=49;ring.loop=false;ring.shadowCastingMode=ShadowCastingMode.Off;ring.receiveShadows=false;
                Clear();
            }
            public void Start(TennisEffectEvent value,TennisEffectPreset settings,float density,bool reduced,string surface)
            {
                Clear();item=value;preset=settings;center=GamePayload.Unpack(item.position);Age=0;Active=true;released=false;
                amount=density*(reduced?.5f:1);scale=reduced?.8f:1;
                tint=preset.Tint=="character"?Color.Lerp(CharacterProfile.ColorOf(CharacterCatalog.Get(item.character).accent),Color.white,.28f):CharacterProfile.ColorOf(preset.Tint);
                if(Kind==TennisEffectKind.Bounce)tint=surface=="clay"?new Color(.86f,.61f,.4f):surface=="grass"?new Color(.59f,.78f,.49f):tint;
                if(reduced)tint.a=.65f;
                ps.randomSeed=item.id==0?1:item.id;
                ring.enabled=true;Emit(false);Advance(item.age);
            }
            private void Emit(bool release)
            {
                int count=Mathf.Clamp(Mathf.RoundToInt(preset.Count*amount),0,120);
                var random=new System.Random(unchecked((int)(item.id*7919+(release?127u:0u))));
                var normal=GamePayload.Unpack(item.direction);var rotation=normal.sqrMagnitude>.001f?Quaternion.LookRotation(normal):Quaternion.identity;
                for(int i=0;i<count;i++)
                {
                    float a=(float)random.NextDouble()*Mathf.PI*2, radius=(float)random.NextDouble();
                    var radial=new Vector3(Mathf.Cos(a),.2f+radius*.7f,Mathf.Sin(a));
                    Vector3 position=center,velocity=radial*preset.Speed;
                    if(Kind==TennisEffectKind.Skill&&!release)
                    {position+=new Vector3(radial.x,.2f+radius,radial.z)*preset.Size;velocity=(center+Vector3.up*.5f-position)*2;}
                    else if(Kind==TennisEffectKind.Skill)
                    {position+=Vector3.up*.5f;velocity=radial*preset.Speed*(1+radius*.5f);}
                    else if(Kind==TennisEffectKind.Charge){position+=new Vector3(radial.x,0,radial.z)*.25f;velocity=Vector3.up*preset.Speed+radial*.2f;}
                    else if(Kind==TennisEffectKind.Hit||Kind==TennisEffectKind.Serve)velocity=rotation*new Vector3(radial.x,radial.z,.4f+radius)*preset.Speed;
                    var e=new ParticleSystem.EmitParams {position=position,velocity=velocity,startColor=tint,startSize=(Kind==TennisEffectKind.Skill?.11f:.07f)*scale*(.5f+radius),
                        startLifetime=Kind==TennisEffectKind.Skill?(release?.65f:.42f):preset.Duration,randomSeed=(uint)i+item.id};
                    ps.Emit(e,1);
                }
            }
            public void Advance(float dt)
            {
                if(!Active)return;
                float target=Age+dt;
                if(Kind==TennisEffectKind.Skill&&!released&&target>=.42f)
                {ps.Simulate(Mathf.Max(0,.42f-Age),false,false,true);Age=.42f;Emit(true);released=true;}
                ps.Simulate(Mathf.Max(0,target-Age),false,false,true);Age=target;
                if(Age>=preset.Duration){Clear();return;}
                float t=Age/preset.Duration;
                float radius=preset.Size*scale*Mathf.Lerp(.2f,1,t);
                if(Kind==TennisEffectKind.Skill)radius=preset.Size*scale*(t<.35f?Mathf.Lerp(1,.3f,t/.35f):Mathf.Lerp(.3f,1.2f,(t-.35f)/.65f));
                ring.widthMultiplier=(Kind==TennisEffectKind.Skill?.055f:.035f)*scale*(1-t*.6f);
                var color=tint;color.a*=1-t;ring.startColor=ring.endColor=color;
                var normal=GamePayload.Unpack(item.direction);
                Quaternion plane=(Kind==TennisEffectKind.Hit||Kind==TennisEffectKind.Serve)&&normal.sqrMagnitude>.001f?Quaternion.LookRotation(normal):Quaternion.Euler(90,0,0);
                int style=CharacterCatalog.Get(item.character).style;
                for(int i=0;i<49;i++)
                {
                    float a=i/48f*Mathf.PI*2;
                    float wave=Kind==TennisEffectKind.Skill?1+.16f*Mathf.Sin(a*(3+style%5)+Age*(3+style%4)):1;
                    ring.SetPosition(i,center+Vector3.up*.035f+plane*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*wave);
                }
            }
            public void Clear(){Active=false;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ring.enabled=false;}
            public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);}
        }
    }
    [Serializable] public sealed class TennisEffectMetrics
    {
        public int[] kinds;
        public int rendered,duplicateErrors,peakParticles,poolCreated,poolReuses,remoteBaselines,hitCount;
        public uint csvRevision;
        public string status;
    }
}
