using System;
using UnityEngine;
namespace DigitalTennis
{
    [Serializable]public sealed class AthleteStyle
    {
        public string id,hair,costume,lower,acquiredHead,acquiredHair;
        public float shoulders,waist,hips,faceWidth,jawWidth,eyeOpenness,armRadius,legRadius,noseLength,browTilt,lipWidth,sleeve;
        public int motif,collar;
    }
    [Serializable]internal sealed class AthleteStyles{public AthleteStyle[] styles;}
    public static class AthleteStyleCatalog
    {
        private static AthleteStyle[] styles;
        public static AthleteStyle Get(int index)
        {if(styles==null)styles=JsonUtility.FromJson<AthleteStyles>(Resources.Load<TextAsset>("Characters/appearance").text).styles;return styles[index];}
    }
    /// <summary>Adapted CC0 heads/hair with original sportswear and contact rig. Changes appearance and expressions only.</summary>
    public sealed class AthleteSkin:MonoBehaviour
    {
        [SerializeField]private SkinnedMeshRenderer surface;
        private int selected=-1;
        public SkinnedMeshRenderer Surface=>surface;
        public static readonly string[] BonePaths={"","Torso","Torso/Head","Torso/RightArm","Torso/RightArm/Elbow","Torso/RightArm/Elbow/Hand Socket","Torso/LeftArm","Torso/LeftArm/Elbow","Torso/LeftArm/Elbow/Hand Socket","RightLeg","RightLeg/Knee","RightLeg/Knee/Foot Socket","LeftLeg","LeftLeg/Knee","LeftLeg/Knee/Foot Socket"};
        public void Present(int profile)
        {
            if(selected==profile)return;selected=profile;var style=AthleteStyleCatalog.Get(profile);var mesh=Resources.Load<Mesh>("Athletes/"+style.id);
            if(mesh==null){Debug.LogError("Missing athlete skin: "+style.id);return;}
            if(surface==null)surface=GetComponentInChildren<SkinnedMeshRenderer>();
            ApplyRestShape(transform,style);surface.sharedMesh=null;surface.sharedMesh=mesh;
            foreach(var r in GetComponentsInChildren<Renderer>(true))if(!(r is SkinnedMeshRenderer))r.enabled=false;
            surface.enabled=true;
        }
        public static void ApplyRestShape(Transform root,AthleteStyle style)
        {
            root.Find("Torso").localScale=Vector3.one;
            foreach(int side in new[]{-1,1})
            {var arm=root.Find(side<0?"Torso/LeftArm":"Torso/RightArm");var at=arm.localPosition;at.x=side*style.shoulders*.5f;arm.localPosition=at;
             var leg=root.Find(side<0?"LeftLeg":"RightLeg");at=leg.localPosition;at.x=side*style.hips*.28f;leg.localPosition=at;}
        }
        public void Expressions(float focus,float smile,float shout,float blink)
        {
            if(surface==null||surface.sharedMesh==null)return;
            float[] values={focus,smile,shout,blink};for(int i=0;i<4;i++)surface.SetBlendShapeWeight(i,Mathf.Clamp01(values[i])*100);
        }
        public void SyncSockets()
        {
            foreach(string path in new[]{"Torso/RightArm/Elbow","Torso/LeftArm/Elbow"})
            {var elbow=transform.Find(path);var socket=elbow.Find("Hand Socket");socket.SetPositionAndRotation(elbow.Find("Skin_Hand").position,elbow.rotation);}
            foreach(string path in new[]{"RightLeg/Knee","LeftLeg/Knee"})
            {var knee=transform.Find(path);var sole=knee.Find("Accent_Sole");knee.Find("Foot Socket").SetPositionAndRotation(sole.position,sole.rotation);}
        }
    }
}
