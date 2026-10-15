using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Predicts a post-bounce contact using a detached copy of the real ball simulation.</summary>
    public static class AutomaticCourtMovement
    {
        public const float Speed = 5f;
        public static Vector3 Home(CourtSide player, float y) => new Vector3(0f, y, player == CourtSide.Near ? -8f : 8f);

        public static Vector3 ClampTarget(Vector3 target, CourtSide player, float y)
        {
            float facing = player == CourtSide.Near ? -1f : 1f;
            float depth = Mathf.Clamp(target.z * facing, 0.8f, CourtBounds.HalfLength);
            return new Vector3(Mathf.Clamp(target.x, -CourtBounds.SinglesHalfWidth, CourtBounds.SinglesHalfWidth),
                y, player == CourtSide.Near ? -depth : depth);
        }

        public static Vector3 Step(Vector3 position, Vector3 target, CourtSide player, float deltaTime, float speed = Speed)
        {
            if (!BallSimulation.IsFinite(position) || !BallSimulation.IsFinite(target) ||
                !BallSimulation.IsFinite(deltaTime) || deltaTime < 0f || !BallSimulation.IsFinite(speed) || speed < 0) return position;
            return Vector3.MoveTowards(position, ClampTarget(target, player, position.y), speed * deltaTime);
        }

        public static Vector3 PredictTarget(BallSimulation incoming, CourtSide player, float playerY)
        {
            var prediction = incoming.CreatePrediction();
            Vector3 candidate = Home(player, playerY);
            float bestHeightError = float.PositiveInfinity;
            for (int tick = 0; tick < 180 && !prediction.IsResting; tick++)
            {
                prediction.Step();
                if (prediction.BounceCount >= 2) break;
                Vector3 at = prediction.Position;
                bool ownSide = player == CourtSide.Near ? at.z < -0.2f : at.z > 0.2f;
                if (!ownSide || prediction.BounceCount != 1 || at.y < RacketInputGate.MinHeight ||
                    at.y > RacketInputGate.MaxHeight) continue;
                float error = Mathf.Abs(at.y - 1f);
                if (error < bestHeightError) { candidate = at; bestHeightError = error; }
                if (at.y >= 1f && prediction.Velocity.y > 0f) break;
            }
            return ClampTarget(candidate, player, playerY);
        }
    }
}
