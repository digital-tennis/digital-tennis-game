using DigitalTennis.AI;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Game-owned conversion; decisions remain in the independent AI assembly.</summary>
    public static class FullMatchObservation
    {
        public static AiState AiState(TennisMatchController c,CourtSide player,long tick,float dt)
        {
            var m=c.Match;var b=c.Ball.Simulation;var self=c.Characters.Player(player);
            bool rotate=m.Progress.EndsChanged;
            Vec3 Turn(Vector3 v)=>new Vec3(rotate?-v.x:v.x,v.y,rotate?-v.z:v.z);
            var own=player==CourtSide.Near?c.NearAvatar:c.FarAvatar;var other=player==CourtSide.Near?c.FarAvatar:c.NearAvatar;
            var phase=c.IsPaused||c.Characters.Cinematic?AiPhase.Paused:m.Phase==MatchPhase.InFlight?AiPhase.ServeInFlight:m.Phase==MatchPhase.Rally?AiPhase.Rally:m.Phase==MatchPhase.PointEnd?AiPhase.PointEnd:m.Phase==MatchPhase.MatchEnd?AiPhase.MatchEnd:AiPhase.WaitServe;
            return new AiState(tick,dt,player==CourtSide.Near?0:1,Turn(own.transform.position),Turn(other.transform.position),Turn(b.Position),Turn(b.Velocity),phase,b.BounceCount,
                ((ulong)m.RallyId<<32)|m.CurrentShot.ShotId,(int)m.LastHitter,b.HasShot,b.IsResting,c.Mode==GameMode.Online,b.Gravity,b.Radius,b.BounceRestitution,b.GroundSpeedRetention,self.Reach,self.ShotMultiplier(StrokeKind.Forehand));
        }
        public static float[] Raw(TennisMatchController c,CourtSide player)
        {
            var s=AiState(c,player,1,.1f);float side=player==CourtSide.Near?-1:1;
            var p=c.Match.Progress;bool near=player==CourtSide.Near;
            var self=c.Characters.Player(player);var other=c.Characters.Player(TennisMatch.Opponent(player));
            return new[]{s.BallPosition.X*side,s.BallPosition.Y,s.BallPosition.Z*side,s.BallVelocity.X*side,s.BallVelocity.Y,s.BallVelocity.Z*side,
                s.OpponentPosition.X*side,s.OpponentPosition.Z*side,s.PlayerPosition.X*side,s.PlayerPosition.Z*side,s.ShotMultiplier,s.Gravity,
                (float)(near?p.Points.NearPoints:p.Points.FarPoints),(float)(near?p.Points.FarPoints:p.Points.NearPoints),near?p.NearGames:p.FarGames,near?p.FarGames:p.NearGames,
                near?p.NearSets:p.FarSets,near?p.FarSets:p.NearSets,c.Match.Server==player?1f:0f,p.TieBreak?1f:0f,(float)c.Match.Phase,s.BounceCount,
                self.Energy,other.Energy,self.Meter.Stock,other.Meter.Stock,self.Remaining,other.Remaining,self.Index,other.Index,c.Match.EndOf(player)==CourtSide.Far?1f:0f,c.Match.FaultCount};
        }
        public static float[] Observe(TennisMatchController c,CourtSide player)=>FullMatchActor.Normalize(Raw(c,player));
    }
}
