using System;
using UnityEngine;
namespace DigitalTennis
{
    [Serializable]public sealed class SkillArtFrame
    {
        public float time,jump,shout,fov;
        public Vector3 torso,rightArm,leftArm,rightElbow,leftElbow,head,leftLeg,rightLeg,racket,camera,shift;
    }
    [Serializable]public sealed class SkillArt{public string id,name;public int shape;public SkillArtFrame[] frames;}
    [Serializable]internal sealed class SkillArtList{public SkillArt[] skills;}
    public readonly struct SkillArtSample
    {
        public readonly SkillArtFrame A,B;public readonly float T;
        public SkillArtSample(SkillArtFrame a,SkillArtFrame b,float t){A=a;B=b;T=t;}
        public Quaternion Rotation(Func<SkillArtFrame,Vector3> field)=>Quaternion.Slerp(Quaternion.Euler(field(A)),Quaternion.Euler(field(B)),T);
        public Vector3 Camera=>Vector3.Lerp(A.camera,B.camera,T);
        public Vector3 Shift=>Vector3.Lerp(A.shift,B.shift,T);
        public float Jump=>Mathf.Lerp(A.jump,B.jump,T);
        public float Shout=>Mathf.Lerp(A.shout,B.shout,T);
        public float Fov=>Mathf.Lerp(A.fov,B.fov,T);
    }
    public static class SkillArtCatalog
    {
        private static SkillArt[] skills;
        public static SkillArt Get(int profile){if(skills==null)skills=JsonUtility.FromJson<SkillArtList>(Resources.Load<TextAsset>("Characters/skill-art").text).skills;return skills[profile];}
        public static SkillArtSample Sample(int profile,float time)
        {
            var frames=Get(profile).frames;int index=0;while(index<frames.Length-2&&time>frames[index+1].time)index++;
            return new SkillArtSample(frames[index],frames[index+1],Mathf.SmoothStep(0,1,Mathf.InverseLerp(frames[index].time,frames[index+1].time,time)));
        }
        private static float Triangle(float a)=>1-Mathf.Abs(Mathf.Repeat(a,2)-1);
        public static Vector3 Ribbon(int profile,int ribbon,float u,float time)
        {
            float a=u*Mathf.PI*2,r=.7f+ribbon*.10f,phase=time*Mathf.PI*2;
            Vector3 p;switch(Get(profile).shape)
            {
                case 1:p=new Vector3(Mathf.Cos(a*1.4f+phase)*r,.6f+u*.9f,Mathf.Sin(a*1.4f+phase)*r);break;
                case 2:p=new Vector3((Triangle(u*4)-.5f)*2*r,.45f+u*1.35f,(ribbon-1.5f)*.25f+Mathf.Sin(phase)*.12f);break;
                case 3:p=new Vector3(Mathf.Cos(a+phase)*r,.95f+Mathf.Sin(a+phase)*.75f,Mathf.Sin(a*2+phase)*.3f);break;
                case 4:r*=.4f+Triangle(u*10)*.8f;p=new Vector3(Mathf.Cos(a)*r,1+Mathf.Sin(a)*r,(ribbon-1.5f)*.15f);break;
                case 5:float f=Mathf.Floor(u*6)/6*Mathf.PI*2;p=new Vector3(Mathf.Cos(f)*r,.5f+u,Mathf.Sin(f)*r);break;
                case 6:r*=.75f+.25f*Mathf.Cos(a*8);p=new Vector3(Mathf.Cos(a)*r,.8f+Mathf.Sin(a*2+phase)*.3f,Mathf.Sin(a)*r);break;
                case 7:p=new Vector3((u-.5f)*2*r,.55f+Triangle(u*8)*.85f,(ribbon-1.5f)*.22f);break;
                case 8:p=new Vector3((u-.5f)*2*r,.65f+Mathf.Sin(u*Mathf.PI)*.7f,-.3f+ribbon*.18f);break;
                case 9:p=new Vector3(Mathf.Cos(u*Mathf.PI)*r,.55f+Mathf.Sin(u*Mathf.PI)*r,.15f+Mathf.Sin(u*Mathf.PI*12)*.12f+ribbon*.06f);break;
                case 10:p=new Vector3(Mathf.Sin(a)*r,.9f+Mathf.Sin(a*2)*.65f,Mathf.Cos(a)*.25f+ribbon*.1f);break;
                case 11:p=new Vector3((u-.5f)*2*r,.9f+Mathf.Sin(a+phase)*.35f,Mathf.Cos(a+phase)*r);break;
                case 12:p=new Vector3((Triangle(u*4)-.5f)*2*r,.6f+ribbon*.2f,(Triangle(u*4+1)-.5f)*2*r);break;
                case 13:p=new Vector3(Mathf.Cos(a*2+phase)*r,.4f+u*1.1f+Mathf.Sin(phase*2)*.1f,Mathf.Sin(a*2+phase)*r);break;
                case 14:r*=.65f+Triangle(u*16)*.35f;p=new Vector3(Mathf.Cos(a)*r,.55f+ribbon*.23f,Mathf.Sin(a)*r);break;
                default:p=new Vector3((u-.5f)*2*r,.4f+Triangle(u*4)*.75f+ribbon*.1f,Mathf.Cos(a)*.25f);break;
            }
            // Upper arcs pass behind the head so the expression remains readable from authored front cameras.
            if(p.y>1.15f)p.z=Mathf.Min(p.z,-.45f);
            return p;
        }
    }
}
