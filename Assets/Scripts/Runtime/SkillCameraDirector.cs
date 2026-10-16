using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Authored character-specific skill camera. All state comes from confirmed character snapshots.</summary>
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
            var authored=SkillArtCatalog.Sample(performer.Index,t);
            Vector3 focus=actor.position+Vector3.up*.35f;
            var offset=authored.Camera;Vector3 target=focus+actor.right*offset.x+Vector3.up*offset.y+actor.forward*offset.z;
            Quaternion look=Quaternion.LookRotation(focus-target);
            float returnAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.77f)/.23f));
            camera.transform.SetPositionAndRotation(Vector3.Lerp(target,position,returnAmount),Quaternion.Slerp(look,rotation,returnAmount));
            camera.fieldOfView=Mathf.Lerp(authored.Fov,fov,returnAmount);
        }
        public void Restore()
        {
            if(!active)return;
            if(camera!=null){camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
            active=false;
        }
    }
}
