using System;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>CC0 UAL Idle_Loop head delta only. No Animator, root motion, or contact overrides.</summary>
    public static class AcquiredAthleteIdle
    {
        [Serializable]private sealed class Clip { public float duration; public float[] times; public Quaternion[] rotations; }
        private static Clip clip;
        public static Quaternion Sample(float presentationTime)
        {
            if(clip==null)clip=JsonUtility.FromJson<Clip>(Resources.Load<TextAsset>("Characters/Acquired/IdleHead").text);
            float time=Mathf.Repeat(presentationTime,clip.duration);
            int i=Array.BinarySearch(clip.times,time);if(i<0)i=Mathf.Max(0,~i-1);
            int next=Mathf.Min(i+1,clip.times.Length-1);
            var pose=Quaternion.Slerp(clip.rotations[i],clip.rotations[next],Mathf.InverseLerp(clip.times[i],clip.times[next],time));
            // Source is glTF right-handed. Convert the additive local rotation to Unity.
            var delta=Quaternion.Inverse(clip.rotations[0])*pose;
            delta=new Quaternion(-delta.x,-delta.y,delta.z,delta.w);
            return Quaternion.Slerp(Quaternion.identity,delta,.65f);
        }
    }
}
