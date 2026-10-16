using System.Collections.Generic;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Articulated presentation only. Never moves a player root, racket hit gate or ball.</summary>
    public sealed partial class AthleteMotion : MonoBehaviour
    {
        private Transform torso, head, leftArm, rightArm, leftElbow, rightElbow, leftLeg, rightLeg, leftKnee, rightKnee;
        private RacketAvatar avatar;
        private TennisMatchController match;
        private Renderer[] surfaces;
        private MaterialPropertyBlock block;
        private int profile = -1;
        private Vector3 previous;
        private float stride, speed, time;
        private long nearPoints, farPoints;
        private bool baseline;
        public bool Showcase {get;set;}
        private float celebration;
        private bool won;
        public int BoneCount => GetComponent<AthleteSkin>()!=null?15:10;
        public float Stride => stride;
        public void Present(CharacterState state)
        {
            if(torso==null) Bind();
            if(profile==state.index)return;
            profile=state.index; var p=CharacterCatalog.Get(profile);
            foreach(var r in surfaces)
            {
                if(r is SkinnedMeshRenderer)continue;
                string n=r.name;
                Color color=n.StartsWith("Skin_")?CharacterProfile.ColorOf(p.skin):
                    n.StartsWith("Primary_")?CharacterProfile.ColorOf(p.primary):
                    n.StartsWith("Secondary_")?CharacterProfile.ColorOf(p.secondary):
                    n.StartsWith("Accent_")?CharacterProfile.ColorOf(p.accent):
                    n.StartsWith("Hair_")?Color.Lerp(CharacterProfile.ColorOf(p.secondary),Color.black,.72f):
                    n.StartsWith("Dark_")?new Color(.035f,.05f,.065f):new Color(.94f,.96f,.94f);
                block.SetColor("_Color",color);r.SetPropertyBlock(block);
            }
            for(int i=0;i<5;i++)head.Find("HairStyle"+i).gameObject.SetActive(i==profile%5);
            head.Find("Headband").gameObject.SetActive(profile%3==0);
            torso.localScale=new Vector3(.93f+(profile%4)*.045f,1,1);
            GetComponent<AthleteSkin>()?.Present(profile);
            foreach(var label in GetComponentsInChildren<TextMesh>())label.text=p.id;
        }
        public void LockGrip()
        {
            if(rightElbow==null)return;
            var pivot=avatar.transform.Find("Racket Pivot");
            var hand=rightElbow.Find("Skin_Hand");
            pivot.position=hand.position-pivot.TransformVector(new Vector3(0,.12f,0));
        }
        private void Bind()
        {
            Transform T(string name)=>transform.Find(name);
            torso=T("Torso");head=torso.Find("Head");leftArm=torso.Find("LeftArm");rightArm=torso.Find("RightArm");
            leftElbow=leftArm.Find("Elbow");rightElbow=rightArm.Find("Elbow");
            leftLeg=T("LeftLeg");rightLeg=T("RightLeg");leftKnee=leftLeg.Find("Knee");rightKnee=rightLeg.Find("Knee");
            avatar=GetComponentInParent<RacketAvatar>();match=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            surfaces=GetComponentsInChildren<Renderer>(true);block=new MaterialPropertyBlock();previous=avatar.transform.position;BindDetailedMotion();
        }
        public void Pose(CharacterState state)
        {
            if(torso==null)Bind();
            ResetLimbLengths();
            bool preview=gameObject.layer==30;
            bool paused=!preview&&!Showcase&&(match?.IsPaused??false);
            float dt=paused?0:Time.deltaTime;time+=dt;
            Vector3 delta=avatar.transform.position-previous;previous=avatar.transform.position;
            float measured=dt>.0001f?new Vector2(delta.x,delta.z).magnitude/dt:0;
            speed=Mathf.MoveTowards(speed,Mathf.Clamp01(measured/5),dt*8);
            stride+=Mathf.Min(delta.magnitude,.25f)*7;
            float run=Mathf.Sin(stride)*speed;
            float bob=Mathf.Abs(Mathf.Sin(stride))*speed*.035f;
            torso.localPosition=new Vector3(0,1.02f+bob,.015f);
            torso.localRotation=Quaternion.Euler(6+speed*10,Mathf.Sin(stride)*speed*8,0);
            head.localRotation=Quaternion.Euler(-6,Mathf.Sin(time*.65f+profile)*2,0)*AcquiredAthleteIdle.Sample(time+profile);
            leftLeg.localRotation=Quaternion.Euler(run*35,0,-3);
            rightLeg.localRotation=Quaternion.Euler(-run*35,0,3);
            leftKnee.localRotation=Quaternion.Euler(Mathf.Max(0,-run)*55+4,0,0);
            rightKnee.localRotation=Quaternion.Euler(Mathf.Max(0,run)*55+4,0,0);
            leftArm.localRotation=Quaternion.Euler(-12-run*23,0,-12);
            rightArm.localRotation=Quaternion.Euler(-20+run*23,0,16);
            leftElbow.localRotation=Quaternion.Euler(-25,0,0);rightElbow.localRotation=Quaternion.Euler(-38,0,0);
            if(!preview && !Showcase && match?.Match!=null)
            {
                var remote=match.Ball.IsAuthoritative?null:match.GetComponent<OnlineTennisSession>()?.PresentedState;
                long near=remote?.score?.totalPoints[0]??match.Match.Progress.NearTotalPoints;
                long far=remote?.score?.totalPoints[1]??match.Match.Progress.FarTotalPoints;
                if(baseline && (near>nearPoints || far>farPoints))
                {won=avatar==match.NearAvatar?near>nearPoints:far>farPoints;celebration=1.1f;}
                nearPoints=near;farPoints=far;baseline=true;
            }
            celebration=Mathf.Max(0,celebration-dt);
            if(celebration>0 && !avatar.IsSwinging)
            {
                float power=Mathf.Sin((1-celebration/1.1f)*Mathf.PI);
                rightArm.localRotation=Quaternion.Euler(won?-105*power:12*power,0,won?30*power:8);
                rightElbow.localRotation=Quaternion.Euler(won?-80*power:-10,0,0);
                head.localRotation=Quaternion.Euler(won?-10*power:18*power,0,0);
            }
            if(!preview&&avatar.IsSwinging)
            {
                float t=Mathf.Clamp01(avatar.SwingAge/RacketAvatar.SwingDuration), arc=avatar.LastSwingContact?Mathf.Cos(t*Mathf.PI*.5f):Mathf.Sin(t*Mathf.PI);
                bool back=avatar.LastStroke==StrokeKind.Backhand;
                float direction=back?-1:1;
                torso.localRotation=Quaternion.Euler(8,direction*Mathf.Lerp(-32,45,t)*arc,0);
                rightArm.localRotation=Quaternion.Euler(-35-arc*35,-direction*55*arc,25+direction*50*arc);
                rightElbow.localRotation=Quaternion.Euler(-55+35*arc,0,0);
                leftArm.localRotation=Quaternion.Euler(-25,back?-35*arc:15*arc,-25-30*arc);
                if(avatar.LastStroke==StrokeKind.Smash || avatar.IsServe)
                {rightArm.localRotation=Quaternion.Euler(-145*arc,0,15);leftArm.localRotation=Quaternion.Euler(-110*arc,0,-15);torso.localRotation=Quaternion.Euler(-18*arc,25*arc,0);}
                if(avatar.LastStroke==StrokeKind.Volley)rightArm.localRotation=Quaternion.Euler(-60*arc,0,20);
                if(avatar.LastStroke==StrokeKind.Lob)rightArm.localRotation=Quaternion.Euler(-95*arc,25*arc,20);
                if(avatar.LastStroke==StrokeKind.Slice)rightArm.localRotation=Quaternion.Euler(15-90*t,50*arc,50*arc);
            }
            if(state.cinematicAge<CharacterAbility.CinematicSeconds)ApplyAuthoredSkill(state);
        }
    }
}
