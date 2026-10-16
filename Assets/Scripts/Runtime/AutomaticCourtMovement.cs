using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Predicts a post-bounce contact using a detached copy of the real ball simulation.</summary>
    public static class AutomaticCourtMovement
    {
        public static float Speed => GameplayBalance.Defaults["move.speed"];
        public static Vector3 Home(CourtSide player, float y, GameplayBalance settings = null) => new Vector3(0f, y,
            (player == CourtSide.Near ? -1 : 1) * (settings ?? GameplayBalance.Defaults)["move.home_depth"]);

        public static Vector3 ClampTarget(Vector3 target, CourtSide player, float y, GameplayBalance settings = null)
        {
            var c=settings ?? GameplayBalance.Defaults;
            float facing = player == CourtSide.Near ? -1f : 1f;
            float depth = Mathf.Clamp(target.z * facing, c["move.min_depth"], CourtBounds.HalfLength);
            return new Vector3(Mathf.Clamp(target.x, -CourtBounds.SinglesHalfWidth, CourtBounds.SinglesHalfWidth),
                y, player == CourtSide.Near ? -depth : depth);
        }

        public static Vector3 Step(Vector3 position, Vector3 target, CourtSide player, float deltaTime, float speed = float.NaN, GameplayBalance settings = null)
        {
            if(float.IsNaN(speed)) speed=(settings ?? GameplayBalance.Defaults)["move.speed"];
            if (!BallSimulation.IsFinite(position) || !BallSimulation.IsFinite(target) ||
                !BallSimulation.IsFinite(deltaTime) || deltaTime < 0f || !BallSimulation.IsFinite(speed) || speed < 0) return position;
            return Vector3.MoveTowards(position, ClampTarget(target, player, position.y,settings), speed * deltaTime);
        }

        public static Vector3 PredictTarget(BallSimulation incoming, CourtSide player, float playerY, GameplayBalance settings = null, bool overhead = false)
        {
            var c=settings ?? GameplayBalance.Defaults;
            if(overhead)
            {
                var high=incoming.CreatePrediction();Vector3 target=Vector3.zero;float error=float.PositiveInfinity;
                float desired=(c["hit.smash_height"]+c["hit.max_height"])*.5f;
                for(int tick=0;tick<180&&!high.IsResting;tick++)
                {
                    high.Step();if(high.BounceCount>0)break;Vector3 at=high.Position;
                    bool own=player==CourtSide.Near?at.z<-c["hit.net_gap"]:at.z>c["hit.net_gap"];
                    if(!own||high.Velocity.y>=0||at.y<c["hit.smash_height"]||at.y>c["hit.max_height"])continue;
                    float next=Mathf.Abs(at.y-desired);if(next<error){target=at;error=next;}
                }
                if(!float.IsPositiveInfinity(error))return ClampTarget(target,player,playerY,c);
            }
            var prediction = incoming.CreatePrediction();
            Vector3 candidate = Home(player, playerY,c);
            float bestHeightError = float.PositiveInfinity;
            for (int tick = 0; tick < 180 && !prediction.IsResting; tick++)
            {
                prediction.Step();
                if (prediction.BounceCount >= 2) break;
                Vector3 at = prediction.Position;
                bool ownSide = player == CourtSide.Near ? at.z < -c["hit.net_gap"] : at.z > c["hit.net_gap"];
                if (!ownSide || prediction.BounceCount != 1 || at.y < c["hit.min_height"] ||
                    at.y > c["hit.max_height"]) continue;
                float error = Mathf.Abs(at.y - c["move.contact_height"]);
                if (error < bestHeightError) { candidate = at; bestHeightError = error; }
                if (at.y >= c["move.contact_height"] && prediction.Velocity.y > 0f) break;
            }
            return ClampTarget(candidate, player, playerY,c);
        }
    }
}
