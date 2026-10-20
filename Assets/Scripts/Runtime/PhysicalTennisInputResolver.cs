namespace DigitalTennis
{
    /// <summary>Game policy. The transport, SDK and firmware never need match context.</summary>
    public static class PhysicalTennisInputResolver
    {
        public static bool IsPhysical(PlayerAction action)=>action>=PlayerAction.ControllerPrimary&&action<=PlayerAction.MotionObliqueUp;
        public static bool TryPhysicalAction(ControllerMotion motion,out PlayerAction action)
        {
            switch(motion)
            {
                case ControllerMotion.HorizontalPositive:action=PlayerAction.MotionHorizontalPositive;return true;
                case ControllerMotion.HorizontalNegative:action=PlayerAction.MotionHorizontalNegative;return true;
                case ControllerMotion.OverheadDown:action=PlayerAction.MotionOverheadDown;return true;
                case ControllerMotion.ShortRotation:action=PlayerAction.MotionShortRotation;return true;
                case ControllerMotion.ObliqueDown:action=PlayerAction.MotionObliqueDown;return true;
                case ControllerMotion.ObliqueUp:action=PlayerAction.MotionObliqueUp;return true;
                default:action=default;return false;
            }
        }
        public static bool TryResolve(PlayerAction physical,MatchPhase phase,bool ownServe,int bounceCount,float aimX,out PlayerAction action)
        {
            action=default;
            if(!IsPhysical(physical))return false;
            if(phase==MatchPhase.WaitServe)
            {
                if(!ownServe||(physical!=PlayerAction.MotionOverheadDown&&physical!=PlayerAction.ControllerPrimary))return false;
                action=PlayerAction.Serve;return true;
            }
            if(phase!=MatchPhase.Rally&&phase!=MatchPhase.InFlight)return false;
            switch(physical)
            {
                case PlayerAction.ControllerPrimary:return false;
                case PlayerAction.MotionHorizontalPositive:action=PlayerAction.Forehand;break;
                case PlayerAction.MotionHorizontalNegative:action=PlayerAction.Backhand;break;
                case PlayerAction.MotionOverheadDown:action=PlayerAction.Smash;break; // Existing gate still checks ball height/reach.
                case PlayerAction.MotionShortRotation:action=bounceCount==0?PlayerAction.Volley:aimX<0?PlayerAction.Backhand:PlayerAction.Forehand;break;
                case PlayerAction.MotionObliqueDown:action=PlayerAction.Slice;break;
                case PlayerAction.MotionObliqueUp:action=PlayerAction.Lob;break;
                default:return false;
            }
            return true;
        }
    }
}
