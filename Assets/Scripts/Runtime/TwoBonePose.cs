using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Analytic cosmetic two-bone IK. The player root and hit gate are never moved.</summary>
    public static class TwoBonePose
    {
        public static float Solve(Transform upper,Transform lower,Transform tip,Vector3 target,Vector3 pole)
        {
            Vector3 root=upper.position;float a=Vector3.Distance(root,lower.position),b=Vector3.Distance(lower.position,tip.position);
            Vector3 delta=target-root;float raw=delta.magnitude;
            if(a<.0001f||b<.0001f||raw<.0001f)return raw;
            Vector3 forward=delta/raw;float distance=Mathf.Clamp(raw,Mathf.Abs(a-b)+.0001f,a+b-.0001f);
            Vector3 bend=Vector3.ProjectOnPlane(pole-root,forward).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(forward,Mathf.Abs(forward.y)<.9f?Vector3.up:Vector3.right).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 elbow=root+forward*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(tip.position-lower.position,target-lower.position)*lower.rotation;
            return Vector3.Distance(tip.position,target);
        }
    }
}
