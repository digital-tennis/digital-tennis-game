using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Three-shot character-specific skill camera. All state comes from confirmed character snapshots.</summary>
    public sealed class SkillCameraDirector
    {
        private bool active;
        private Vector3 position;
        private Quaternion rotation;
        private float fov;
        private Camera camera;
        public void Present(TennisMatchController c,CharacterAbility near,CharacterAbility far,bool reduced)
        {
            camera=c.View.Camera;
            var performer=near.Cinematic?near:far.Cinematic?far:null;
            if(performer==null||reduced)
            {Restore();return;}
            if(!active){position=camera.transform.position;rotation=camera.transform.rotation;fov=camera.fieldOfView;active=true;}
            var actor=performer==near?c.NearAvatar.transform:c.FarAvatar.transform;
            float t=Mathf.Clamp01(performer.CinematicAge/CharacterAbility.CinematicSeconds);
            float style=performer.Index;
            Vector3 focus=actor.position+Vector3.up*.35f;
            float angle=(style%5-2)*13;
            Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*actor.forward;
            Vector3 close=focus+direction*(1.8f+style%3*.18f)+actor.right*.65f+Vector3.up*.35f;
            Vector3 sweep=focus+Quaternion.AngleAxis(50+style%4*12,Vector3.up)*direction*2.5f+Vector3.up*.65f;
            Vector3 target=t<.26f?close:Vector3.Lerp(close,sweep,Mathf.SmoothStep(0,1,(t-.26f)/.40f));
            Quaternion look=Quaternion.LookRotation(focus-target);
            float returnAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.72f)/.28f));
            camera.transform.SetPositionAndRotation(Vector3.Lerp(target,position,returnAmount),Quaternion.Slerp(look,rotation,returnAmount));
            camera.fieldOfView=Mathf.Lerp(42,fov,returnAmount);
        }
        public void Restore()
        {
            if(!active)return;
            if(camera!=null){camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
            active=false;
        }
    }
}
