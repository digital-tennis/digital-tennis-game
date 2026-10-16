using UnityEngine;
namespace DigitalTennis
{
    /// <summary>Cosmetic cadence from the displayed avatar, including the guest's buffered positions.</summary>
    public sealed class TennisFootstepTrack
    {
        private Vector3 previous,direction;
        private bool baseline;
        private float distance,stepAt,skidAt,speed;
        public int Observe(Vector3 position,float dt,float now,bool movingAllowed)
        {
            if(!baseline||!movingAllowed||dt<=0){previous=position;baseline=true;distance=speed=0;return 0;}
            Vector3 delta=position-previous;delta.y=0;previous=position;
            float length=delta.magnitude;
            if(length>.8f){distance=speed=0;return 0;} // End changes/reconnect corrections are not footsteps.
            float nextSpeed=length/dt;
            bool braking=speed>2.5f&&(nextSpeed<speed*.45f||(length>.005f&&Vector3.Dot(direction,delta.normalized)<-.2f));
            speed=nextSpeed;if(length>.005f)direction=delta.normalized;
            distance+=length;
            if(braking&&now>=skidAt){skidAt=now+.7f;distance=0;stepAt=now+.16f;return 2;}
            if(distance>=.85f&&now>=stepAt){distance%=.85f;stepAt=now+.16f;return 1;}
            return 0;
        }
    }
}
