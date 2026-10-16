using UnityEngine;
namespace DigitalTennis
{
    public sealed partial class AthleteMotion
    {
        private void ApplyAuthoredSkill(CharacterState state)
        {
            var pose=SkillArtCatalog.Sample(profile,state.cinematicAge/CharacterAbility.CinematicSeconds);
            bool reduced=FrontEndController.Active?.ReduceMotion==true;float amplitude=reduced?.45f:1;
            Quaternion R(System.Func<SkillArtFrame,Vector3> value)=>Quaternion.Slerp(Quaternion.identity,pose.Rotation(value),amplitude);
            torso.localRotation=R(f=>f.torso);rightArm.localRotation=R(f=>f.rightArm);leftArm.localRotation=R(f=>f.leftArm);
            rightElbow.localRotation=R(f=>f.rightElbow);leftElbow.localRotation=R(f=>f.leftElbow);head.localRotation=R(f=>f.head);
            leftLeg.localRotation=R(f=>f.leftLeg);rightLeg.localRotation=R(f=>f.rightLeg);
            avatar.RacketPivot.localRotation=R(f=>f.racket);
        }
    }
}
