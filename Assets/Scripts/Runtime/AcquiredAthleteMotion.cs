using System;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Retargeted CC0 skeleton tracks, sampled on presentation distance/time.
    /// No Animator root motion, authority state, input, or ball writes.</summary>
    public sealed class AcquiredAthleteMotion
    {
        [Serializable] public sealed class Bone { public string path; }
        [Serializable] public sealed class Settings
        {
            public Bone[] bones;
            public Vector3 gripPalmOffset,gripRotation;
            public float maxArmStretch,contactLean,turnSpeed,headingDeadZone,resetDistance,recoveryBegin;
            public float strideMeters,walkSpeed,jogSpeed,sprintSpeed,blendRate,turnLean,pelvisBob,armCarry,stanceFraction,stepHeight,contactRelease,recoveryEnd,anklePlantHeight,footRetargetScale,torsoRotationWeight;
        }
        [Serializable] private sealed class Frame { public Quaternion[] rotations; public Vector3[] feet; public float pelvisY; }
        [Serializable] private sealed class Clip { public string name; public float duration; public Frame[] frames; }
        [Serializable] private sealed class Library { public Clip[] clips; }
        private static Library library;
        private static Settings settings;
        private readonly Transform[] bones;
        public static Settings Config
        {
            get
            {
                if(settings==null)settings=JsonUtility.FromJson<Settings>(Resources.Load<TextAsset>("Characters/motion-retarget").text);
                return settings;
            }
        }
        public AcquiredAthleteMotion(Transform root)
        {
            if(library==null)library=JsonUtility.FromJson<Library>(Resources.Load<TextAsset>("Characters/Acquired/FullBodyMotion").text);
            bones=new Transform[Config.bones.Length];
            for(int i=0;i<bones.Length;i++)bones[i]=root.Find(Config.bones[i].path);
        }
        public static Quaternion Sample(int clip,int bone,float cycle)
        {
            if(library==null)library=JsonUtility.FromJson<Library>(Resources.Load<TextAsset>("Characters/Acquired/FullBodyMotion").text);
            var frames=library.clips[clip].frames;float f=Mathf.Repeat(cycle,1)*(frames.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(a+1,frames.Length-1);
            return Quaternion.Slerp(frames[a].rotations[bone],frames[b].rotations[bone],f-a);
        }
        public static Vector3 FootSample(int side,float cycle)
        {
            var frames=library.clips[2].frames;float f=Mathf.Repeat(cycle,1)*(frames.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(a+1,frames.Length-1);
            return Vector3.Lerp(frames[a].feet[side],frames[b].feet[side],f-a);
        }
        public void Apply(float cycle,float clock,float metersPerSecond,Vector3 localVelocity)
        {
            var cfg=Config;
            float moving=Mathf.SmoothStep(0,1,metersPerSecond/cfg.walkSpeed);
            int clip=metersPerSecond<cfg.jogSpeed?1:2;
            float blend=clip==1?Mathf.InverseLerp(cfg.walkSpeed,cfg.jogSpeed,metersPerSecond):Mathf.InverseLerp(cfg.jogSpeed,cfg.sprintSpeed,metersPerSecond);
            for(int i=0;i<bones.Length;i++)
            {
                Quaternion idle=Sample(0,i,clock/library.clips[0].duration);
                Quaternion run=Quaternion.Slerp(Sample(clip,i,cycle),Sample(clip+1,i,cycle),blend);
                bones[i].localRotation=Quaternion.Slerp(idle,run,moving);
                if(i==0)bones[i].localRotation=Quaternion.Slerp(Quaternion.identity,bones[i].localRotation,cfg.torsoRotationWeight);
            }
            // A small racket-carry offset keeps acquired arm swing compatible with a grip.
            bones[2].localRotation*=Quaternion.Euler(-cfg.armCarry*30,0,0);
            var frames=library.clips[2].frames;float f=Mathf.Repeat(cycle,1)*(frames.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(a+1,frames.Length-1);
            float bob=(Mathf.Lerp(frames[a].pelvisY,frames[b].pelvisY,f-a)-frames[0].pelvisY)*cfg.pelvisBob*moving;
            bones[0].localPosition=new Vector3(0,1.02f+bob,.015f);
            bones[0].localRotation*=Quaternion.Euler(Mathf.Clamp(localVelocity.z,-1,1)*cfg.turnLean,0,-Mathf.Clamp(localVelocity.x,-1,1)*cfg.turnLean);
        }
    }
}
