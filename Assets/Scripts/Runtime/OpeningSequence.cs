using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalTennis
{
    [Serializable] public sealed class OpeningShot
    {
        public string id,caption;
        public float start,end,fovFrom,fovTo,bank;
        public Vector3 from,to,lookFrom,lookTo;
    }
    [Serializable] public sealed class OpeningEdit {public OpeningShot[] shots;}

    /// <summary>Authored real-time montage of the actual court and original player meshes.
    /// Actors and ball are presentation copies; no input, score, stock or physics is written.</summary>
    public sealed class OpeningSequence : IDisposable
    {
        public const float Duration=12.5f;
        private readonly TennisMatchController match;
        private readonly OpeningEdit edit;
        private readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        private readonly int originalMask;
        private Camera camera;
        private RenderTexture target;
        private GameObject root,ball;
        private LineRenderer streak;
        private readonly RacketAvatar[] actors=new RacketAvatar[2];
        private readonly CharacterPresentation[] looks=new CharacterPresentation[2];
        private readonly CharacterState[] states=new CharacterState[2];
        private bool active;private float previousAge=-1;
        public Texture Texture=>target;
        public Camera Camera=>camera;
        public bool Active=>active;
        public string ShotId {get;private set;}="studio";
        public static string Caption(float age)=>age<2.6f?"한 번의 서브로 시작되는 세계":age<4.3f?"고요한 코트. 시작되는 승부.":age<6.65f?"읽고. 달리고. 되받아쳐라.":age<9.1f?"당신만의 결정적인 한 방.":age<10.6f?"15 NATIONS  /  ONE COURT":"PLAY BEYOND BORDERS";
        public OpeningSequence(TennisMatchController controller)
        {
            match=controller;edit=JsonUtility.FromJson<OpeningEdit>(Resources.Load<TextAsset>("FrontEnd/opening-edit").text);
            originalMask=match.View.Camera.cullingMask;match.View.Camera.cullingMask&=~(1<<29);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var go=new GameObject("Opening Camera (presentation only)");camera=go.AddComponent<Camera>();camera.CopyFrom(match.View.Camera);
            camera.tag="Untagged";camera.cullingMask=(originalMask|1<<29)&~((1<<30)|(1<<5));camera.depth=-20;camera.nearClipPlane=.08f;camera.farClipPlane=160;camera.enabled=false;
            target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        }
        private void Begin()
        {
            active=true;previousAge=-1;root=new GameObject("Opening Actors - no match authority");
            foreach(var source in new[]{match.NearAvatar.gameObject,match.FarAvatar.gameObject,match.Ball.gameObject})
                foreach(var renderer in source.GetComponentsInChildren<Renderer>(true)){hidden[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
            for(int i=0;i<2;i++)
            {
                var clone=UnityEngine.Object.Instantiate(i==0?match.NearAvatar.gameObject:match.FarAvatar.gameObject,root.transform);clone.name="Opening "+CharacterCatalog.Ids[i];actors[i]=clone.GetComponent<RacketAvatar>();
                foreach(var t in clone.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
                foreach(var r in clone.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
                foreach(var collider in clone.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                clone.GetComponentInChildren<AthleteMotion>().Showcase=true;
                states[i]=new CharacterAbility(i,match.Characters.Store[i]).Capture();looks[i]=clone.GetComponentInChildren<CharacterPresentation>();looks[i].Present(states[i]);
            }
            ball=new GameObject("Opening Ball - render only",typeof(MeshFilter),typeof(MeshRenderer));ball.transform.SetParent(root.transform);ball.layer=29;
            ball.GetComponent<MeshFilter>().sharedMesh=match.Ball.GetComponent<MeshFilter>().sharedMesh;ball.GetComponent<MeshRenderer>().sharedMaterial=match.Ball.GetComponent<MeshRenderer>().sharedMaterial;ball.transform.localScale=match.Ball.transform.lossyScale;
            var line=new GameObject("Ball Speed Streak",typeof(LineRenderer));line.transform.SetParent(root.transform);line.layer=29;streak=line.GetComponent<LineRenderer>();streak.sharedMaterial=ball.GetComponent<Renderer>().sharedMaterial;streak.useWorldSpace=true;streak.positionCount=12;streak.widthMultiplier=.028f;streak.startColor=new Color(.8f,1,.3f,.7f);streak.endColor=new Color(.8f,1,.3f,0);
            if(camera!=null)camera.enabled=true;
        }
        public void Present(float age,bool reduced)
        {
            if(reduced||age>=Duration){Stop();return;}
            if(!active)Begin();
            float time=Mathf.Clamp(age,0,Duration);var shot=Sample(edit,time);
            var audio=match.GetComponent<TennisAudio>();
            if(previousAge<4.3f&&time>=4.3f)audio?.Play("serve");
            if(previousAge<5.55f&&time>=5.55f)audio?.Play("hit");
            if(previousAge<7.35f&&time>=7.35f)audio?.Play("skill");
            previousAge=time;
            ShotId=shot.id;float u=Mathf.SmoothStep(0,1,Mathf.InverseLerp(shot.start,shot.end,time));
            Vector3 position=Vector3.Lerp(shot.from,shot.to,u),focus=Vector3.Lerp(shot.lookFrom,shot.lookTo,u);
            if(camera!=null){camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position)*Quaternion.Euler(0,0,shot.bank));camera.fieldOfView=Mathf.Lerp(shot.fovFrom,shot.fovTo,u);}
            actors[0].transform.SetPositionAndRotation(new Vector3(time<6?0:Mathf.Lerp(0,-1.2f,Mathf.InverseLerp(6,6.7f,time)),.85f,-8),Quaternion.identity);
            actors[1].transform.SetPositionAndRotation(new Vector3(Mathf.Lerp(0,1.3f,Mathf.InverseLerp(4.5f,5.3f,time)),.85f,7),Quaternion.Euler(0,180,0));
            states[0].cinematicAge=time>=9.1f&&time<10.6f?Mathf.Lerp(0,CharacterAbility.CinematicSeconds,Mathf.InverseLerp(9.1f,10.6f,time)):CharacterAbility.CinematicSeconds;
            states[1].cinematicAge=CharacterAbility.CinematicSeconds;
            if(time>=3.2f&&time<5.2f)actors[0].PresentRemoteSwing(1,StrokeKind.Forehand,ServeAge(time),true,true,new Vector3(.55f,2.65f,-7.8f));
            if(time>=5.4f&&time<6.5f)actors[1].PresentRemoteSwing(1,StrokeKind.Backhand,Mathf.Clamp((time-5.55f)*.65f,0,.35f),true,false,new Vector3(.7f,1.3f,6.6f));
            if(time>=6.65f)actors[0].PresentRemoteSwing(2,StrokeKind.Smash,Mathf.Clamp((time-7.35f)*.4f,0,.35f),true,false,new Vector3(-.65f,2.7f,-7.6f));
            for(int i=0;i<2;i++){looks[i].Present(states[i]);looks[i].SynchronizeVisualPose();}
            ball.transform.position=BallAt(time);ball.transform.Rotate(100*Time.unscaledDeltaTime,180*Time.unscaledDeltaTime,0);
            streak.enabled=shot.id=="chase"||shot.id=="line";
            if(camera!=null&&Vector3.Dot(ball.transform.position-camera.transform.position,camera.transform.forward)<.5f)streak.enabled=false;
            for(int i=0;i<12;i++)streak.SetPosition(i,BallAt(Mathf.Max(0,time-i*.009f)));
        }
        private static float ServeAge(float t)=>t<4.3f?Mathf.Lerp(.30f,0,Mathf.InverseLerp(3.2f,4.3f,t)):Mathf.Clamp((t-4.3f)*.6f,0,.35f);
        public static Vector3 BallAt(float time)
        {
            if(time<3.2f)return new Vector3(.55f,1.1f,-7.8f);
            if(time<4.3f){float u=Mathf.InverseLerp(3.2f,4.3f,time);return new Vector3(.55f,Mathf.Lerp(1.1f,2.65f,u)+Mathf.Sin(u*Mathf.PI)*.55f,-7.8f);}
            if(time<5.55f)return Arc(new Vector3(.55f,2.65f,-7.8f),new Vector3(.7f,1.3f,6.6f),Mathf.InverseLerp(4.3f,5.55f,time),-.7f);
            if(time<7.35f)return Arc(new Vector3(.7f,1.3f,6.6f),new Vector3(-.65f,2.7f,-7.6f),Mathf.InverseLerp(5.55f,7.35f,time),2);
            if(time<8.5f)return Arc(new Vector3(-.65f,2.7f,-7.6f),new Vector3(-3.5f,.1f,9.5f),Mathf.InverseLerp(7.35f,8.5f,time),.15f);
            return Arc(new Vector3(-3.5f,.1f,9.5f),new Vector3(-4.2f,1.1f,13.5f),Mathf.InverseLerp(8.5f,9.1f,time),.5f);
        }
        private static Vector3 Arc(Vector3 a,Vector3 b,float t,float height)=>Vector3.Lerp(a,b,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*height);
        public static OpeningShot Sample(OpeningEdit edit,float time)
        {foreach(var shot in edit.shots)if(time<shot.end)return shot;return edit.shots[edit.shots.Length-1];}
        public void Render(){if(camera!=null&&active)camera.Render();}
        public void Stop()
        {
            if(!active)return;active=false;if(camera!=null)camera.enabled=false;
            foreach(var pair in hidden)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value;hidden.Clear();
            if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}root=null;
        }
        public void Dispose(){Stop();if(match?.View?.Camera!=null)match.View.Camera.cullingMask=originalMask;if(camera!=null){camera.targetTexture=null;UnityEngine.Object.Destroy(camera.gameObject);}if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}}
    }
}
