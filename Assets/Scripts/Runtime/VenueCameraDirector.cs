using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Short venue establishing shot while the versus screen gates gameplay.</summary>
    public sealed class VenueCameraDirector
    {
        private Camera camera;private bool active;private Vector3 position;private Quaternion rotation;private float fov,age;
        public void Present(Camera view,CourtSide localEnd,bool versus,bool reduced,float deltaTime)
        {
            if(!versus||reduced){Restore();return;}
            if(!active){camera=view;position=view.transform.position;rotation=view.transform.rotation;fov=view.fieldOfView;age=0;active=true;}
            age+=Mathf.Max(0,deltaTime);var turn=localEnd==CourtSide.Far?Quaternion.Euler(0,180,0):Quaternion.identity;
            Vector3 target=turn*new Vector3(3.5f,5.2f,-18f),focus=turn*new Vector3(0,4,23);
            float back=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-1.3f)/.9f));
            camera.transform.SetPositionAndRotation(Vector3.Lerp(target,position,back),Quaternion.Slerp(Quaternion.LookRotation(focus-target),rotation,back));camera.fieldOfView=Mathf.Lerp(53,fov,back);
        }
        public void Restore(){if(!active)return;if(camera!=null){camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}active=false;}
    }
}
