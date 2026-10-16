using UnityEngine;
namespace DigitalTennis
{
    public sealed partial class AthleteMotion
    {
        private Transform hand,forearm,upperArm,wrist;
        private readonly Foot[] feet={new Foot(),new Foot()};
        private Vector3 elbowRest,handRest;
        private float forearmY,upperArmY,wristY,forearmScale,upperArmScale;
        public float ArmStretch {get;private set;}=1;
        public float ContactError {get;private set;}
        public float PlantedFootError {get;private set;}
        public float PlantedFootSlip {get;private set;}
        public int PlantedFootSamples {get;private set;}
        public bool Airborne {get;private set;}
        public float GroundedModelY
        {
            get {var model=transform.parent;return -model.parent.position.y/Mathf.Max(.001f,model.parent.lossyScale.y)-.0575f*model.localScale.y;}
        }
        private sealed class Foot
        {public Transform Hip,Knee,Sole,Shoe;public Vector3 Anchor,Previous,From,RestSole,RestKnee;public bool Ready,WasPlanted;}
        private void BindDetailedMotion()
        {
            hand=rightElbow.Find("Skin_Hand");forearm=rightElbow.Find("Skin_Forearm");upperArm=rightArm.Find("Skin_UpperArm");wrist=rightElbow.Find("Accent_Wristband");
            elbowRest=rightElbow.localPosition;handRest=hand.localPosition;
            forearmY=forearm.localPosition.y;upperArmY=upperArm.localPosition.y;wristY=wrist.localPosition.y;
            forearmScale=forearm.localScale.y;upperArmScale=upperArm.localScale.y;
            feet[0].Hip=leftLeg;feet[0].Knee=leftKnee;feet[1].Hip=rightLeg;feet[1].Knee=rightKnee;
            foreach(var foot in feet){foot.Sole=foot.Knee.Find("Accent_Sole");foot.Shoe=foot.Knee.Find("White_Shoe");foot.RestSole=foot.Sole.localPosition;foot.RestKnee=foot.Knee.localPosition;}
        }
        private void ResetLimbLengths()
        {
            foreach(var foot in feet){foot.Knee.localPosition=foot.RestKnee;foot.Sole.localPosition=foot.RestSole;}
            rightElbow.localPosition=elbowRest;hand.localPosition=handRest;
            SetY(forearm,forearmY,forearmScale);SetY(upperArm,upperArmY,upperArmScale);
            var p=wrist.localPosition;p.y=wristY;wrist.localPosition=p;
        }
        private static void SetY(Transform part,float position,float scale)
        {var p=part.localPosition;p.y=position;part.localPosition=p;var s=part.localScale;s.y=scale;part.localScale=s;}
        public void CompleteDetailedPose(CharacterState state)
        {
            if(torso==null)Bind();bool preview=gameObject.layer==30,cinematic=state.cinematicAge<CharacterAbility.CinematicSeconds;
            var model=transform.parent;
            Airborne=cinematic&&model.localPosition.y>GroundedModelY+.025f;
            // A low athletic stance provides leg reach without lifting a planted foot.
            if(!cinematic&&!preview)model.localPosition+=Vector3.down*(speed*.075f);
            var pivot=avatar.RacketPivot;
            if(!cinematic)
            {
                float t=avatar.IsSwinging?Mathf.Clamp01(avatar.SwingAge/RacketAvatar.SwingDuration):1;
                float arc=avatar.IsSwinging?(avatar.LastSwingContact?Mathf.Cos(t*Mathf.PI*.5f):Mathf.Sin(t*Mathf.PI)):0;
                float direction=avatar.LastStroke==StrokeKind.Backhand?-1:1;
                pivot.rotation=avatar.transform.rotation*Quaternion.Euler(avatar.IsServe||avatar.LastStroke==StrokeKind.Smash?-55*arc:0,direction*75*arc,-direction*25*arc);
                if(!preview&&!avatar.IsSwinging&&match?.Match!=null&&match.Match.LastHitter!=(avatar==match.NearAvatar?CourtSide.Near:CourtSide.Far)&&match.Ball.Simulation.HasShot)
                {rightArm.localRotation=Quaternion.Euler(-44,0,20);rightElbow.localRotation=Quaternion.Euler(-70,0,0);leftArm.localRotation=Quaternion.Euler(-35,0,-22);}
                if(!preview&&avatar.IsSwinging&&avatar.HasContactPoint&&avatar.LastSwingContact)
                {
                    float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.035f,.20f,avatar.SwingAge));
                    if(weight>0)ContactPose(pivot,weight);
                }
            }
            if(!preview)PlantFeet(Airborne);
            var skin=GetComponent<AthleteSkin>();
            if(skin!=null){skin.SyncSockets();float blink=Mathf.Pow(Mathf.Max(0,Mathf.Cos(time*1.4f+profile*2.3f)),48);skin.Expressions(avatar.IsSwinging?.8f:.15f,celebration>0&&won?celebration/1.1f:0,cinematic?SkillArtCatalog.Sample(profile,state.cinematicAge/CharacterAbility.CinematicSeconds).Shout:0,blink);}
        }
        private void ContactPose(Transform pivot,float weight,bool shift=true)
        {
            Vector3 toBall=avatar.ContactPoint-rightArm.position;
            Vector3 horizontal=Vector3.ProjectOnPlane(toBall,Vector3.up);
            // A bounded cosmetic weight shift; authority and actual automatic movement remain untouched.
            Vector3 lean=Vector3.ClampMagnitude(horizontal,UsesAcquiredMotion?AcquiredAthleteMotion.Config.contactLean:.22f)*weight;
            if(shift)transform.parent.position+=lean-Vector3.up*(.085f*weight);
            float jump=Mathf.Clamp(avatar.ContactPoint.y-2.0f,0,.55f)*weight;
            if(jump>.02f){if(shift)transform.parent.position+=Vector3.up*jump;Airborne=true;}
            Vector3 up=(avatar.ContactPoint-rightArm.position).normalized;
            if(up.sqrMagnitude<.1f)up=Vector3.up;
            Vector3 normal=Vector3.ProjectOnPlane(avatar.transform.forward,up).normalized;
            if(normal.sqrMagnitude<.1f)normal=Vector3.ProjectOnPlane(avatar.transform.right,up).normalized;
            Quaternion contactRotation=Quaternion.LookRotation(normal,up);
            pivot.rotation=Quaternion.Slerp(pivot.rotation,contactRotation,weight);
            Vector3 gripTarget=avatar.ContactPoint-pivot.TransformVector(new Vector3(0,.68f,0));
            if(UsesAcquiredMotion)gripTarget-=GripRotation*AcquiredAthleteMotion.Config.gripPalmOffset;
            gripTarget=Vector3.Lerp(hand.position,gripTarget,weight);
            float length=rightArm.TransformVector(elbowRest).magnitude+rightElbow.TransformVector(handRest).magnitude;
            if(UsesAcquiredMotion)
            {
                // Spend the lunge in the coherent body, not by elongating the humerus.
                Vector3 excess=gripTarget-rightArm.position;
                float over=Mathf.Max(0,excess.magnitude-length*AcquiredAthleteMotion.Config.maxArmStretch*.995f);
                transform.parent.position+=excess.normalized*over;
            }
            float stretch=Mathf.Max(1,Vector3.Distance(rightArm.position,gripTarget)/Mathf.Max(.01f,length)*1.002f);
            if(UsesAcquiredMotion)stretch=Mathf.Min(stretch,AcquiredAthleteMotion.Config.maxArmStretch);
            ArmStretch=stretch;
            // Stylized reach matches the existing arcade hit volume, without delaying or moving the ball.
            rightElbow.localPosition=elbowRest*stretch;hand.localPosition=handRest*stretch;
            SetY(forearm,forearmY*stretch,forearmScale*stretch);SetY(upperArm,upperArmY*stretch,upperArmScale*stretch);
            var wristPosition=wrist.localPosition;wristPosition.y=wristY*stretch;wrist.localPosition=wristPosition;
            Vector3 pole=rightArm.position+avatar.transform.right*.8f-avatar.transform.forward*.35f;
            for(int i=0;i<3;i++)TwoBonePose.Solve(rightArm,rightElbow,hand,gripTarget,pole);
            LockGrip();ContactError=Vector3.Distance(avatar.StringCenter,avatar.ContactPoint);
        }
        private void PlantFeet(bool cinematic)
        {
            // Preserve stance anchors by lowering the pelvis before solving either leg.
            // Moving an anchor to an unreachable hip makes the planted shoe visibly slide.
            float lower=0;
            if(!cinematic&&!Airborne)foreach(var foot in feet)
            {
                if(!foot.Ready||!foot.WasPlanted)continue;
                float reach=(Vector3.Distance(foot.Hip.position,foot.Knee.position)+Vector3.Distance(foot.Knee.position,foot.Sole.position)-.002f)*1.18f;
                float horizontal=Vector2.Distance(new Vector2(foot.Anchor.x,foot.Anchor.z),new Vector2(foot.Hip.position.x,foot.Hip.position.z));
                float maxHeight=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-horizontal*horizontal));
                lower=Mathf.Max(lower,foot.Hip.position.y-foot.Anchor.y-maxHeight+.008f);
            }
            if(lower>0)
            {
                transform.parent.position-=Vector3.up*Mathf.Min(lower,.24f);
                if(avatar.IsSwinging&&avatar.HasContactPoint&&avatar.LastSwingContact)
                {float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.035f,.20f,avatar.SwingAge));if(weight>0)ContactPose(avatar.RacketPivot,weight,false);}
            }
            PlantedFootError=PlantedFootSlip=0;
            for(int i=0;i<2;i++)
            {
                var f=feet[i];float phase=Mathf.Repeat((UsesAcquiredMotion?motionCycle:stride/(Mathf.PI*2))+i*.5f,1);
                float stance=UsesAcquiredMotion?AcquiredAthleteMotion.Config.stanceFraction:.55f;
                Vector3 acquiredFoot=UsesAcquiredMotion?AcquiredAthleteMotion.FootSample(i==0?1:0,motionCycle):Vector3.zero;
                bool planted=speed<.1f||(UsesAcquiredMotion?acquiredFoot.y<AcquiredAthleteMotion.Config.anklePlantHeight:phase<stance);
                float soleHeight=.0175f*transform.lossyScale.y;
                Vector3 rest=transform.TransformPoint(new Vector3(f.Hip.localPosition.x,.0575f,.07f));rest.y=soleHeight;
                if(UsesAcquiredMotion)
                    rest+=transform.forward*(acquiredFoot.z*AcquiredAthleteMotion.Config.footRetargetScale*speed);
                if(cinematic||Airborne){f.Ready=false;continue;}
                if(!f.Ready||Vector3.Distance(rest,f.Anchor)>.55f){f.Anchor=f.Previous=f.From=rest;f.Ready=true;f.WasPlanted=planted;}
                if(planted&&!f.WasPlanted){f.Anchor=rest;f.From=rest;}
                Vector3 target=f.Anchor;
                if(!planted&&UsesAcquiredMotion)
                {
                    target=rest;
                    target.y+=Mathf.Max(0,acquiredFoot.y-AcquiredAthleteMotion.Config.anklePlantHeight)*AcquiredAthleteMotion.Config.footRetargetScale*speed;
                }
                else if(!planted)
                {float t=Mathf.InverseLerp(stance,1,phase);target=Vector3.Lerp(f.Anchor,rest,Mathf.SmoothStep(0,1,t));target.y+=Mathf.Sin(t*Mathf.PI)*(UsesAcquiredMotion?AcquiredAthleteMotion.Config.stepHeight:.10f)*speed;}
                float reach=Vector3.Distance(f.Hip.position,f.Knee.position)+Vector3.Distance(f.Knee.position,f.Sole.position)-.002f;
                float stretch=Mathf.Clamp(Vector3.Distance(f.Hip.position,target)/Mathf.Max(.01f,reach)*1.002f,1,1.18f);
                if(stretch>1){f.Knee.localPosition=f.RestKnee*stretch;f.Sole.localPosition=f.RestSole*stretch;reach*=stretch;}
                Vector2 offset=new Vector2(target.x-f.Hip.position.x,target.z-f.Hip.position.z);
                float allowed=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-Mathf.Pow(f.Hip.position.y-target.y,2)));
                if(offset.magnitude>allowed){offset=offset.normalized*allowed;target.x=f.Hip.position.x+offset.x;target.z=f.Hip.position.z+offset.y;if(planted)f.Anchor=target;}
                TwoBonePose.Solve(f.Hip,f.Knee,f.Sole,target,f.Hip.position+avatar.transform.forward*.6f);
                f.Sole.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
                f.Shoe.position=f.Sole.position+Vector3.up*(.055f*transform.lossyScale.y);f.Shoe.rotation=transform.rotation*Quaternion.Euler(90,0,0);
                if(planted)
                {PlantedFootSamples++;PlantedFootError=Mathf.Max(PlantedFootError,Mathf.Abs(f.Sole.position.y-soleHeight));
                 if(f.WasPlanted)PlantedFootSlip=Mathf.Max(PlantedFootSlip,new Vector2(f.Sole.position.x-f.Previous.x,f.Sole.position.z-f.Previous.z).magnitude);}
                f.Previous=f.Sole.position;f.WasPlanted=planted;
            }
        }
    }
}
