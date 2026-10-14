using System;
using System.Collections.Generic;

namespace DigitalTennis
{
    public enum ServeSide { Right, Left }

    /// <summary>One-game scoring, independent of Unity scenes and physics.</summary>
    public readonly struct TennisScore
    {
        public readonly int NearPoints;
        public readonly int FarPoints;
        public long TotalPoints => (long)NearPoints + FarPoints;
        public bool IsDeuce => NearPoints >= 3 && NearPoints == FarPoints;
        public CourtSide? Winner => NearPoints >= 4 && (long)NearPoints - FarPoints >= 2 ? CourtSide.Near :
            FarPoints >= 4 && (long)FarPoints - NearPoints >= 2 ? CourtSide.Far : (CourtSide?)null;
        public CourtSide? Advantage => Winner.HasValue || NearPoints < 3 || FarPoints < 3 ? null :
            NearPoints == (long)FarPoints + 1 ? CourtSide.Near :
            FarPoints == (long)NearPoints + 1 ? CourtSide.Far : (CourtSide?)null;
        public ServeSide NextServeSide => TotalPoints % 2 == 0 ? ServeSide.Right : ServeSide.Left;

        public TennisScore(int nearPoints, int farPoints)
        {
            if (nearPoints < 0 || farPoints < 0) throw new ArgumentOutOfRangeException(nameof(nearPoints));
            NearPoints = nearPoints;
            FarPoints = farPoints;
        }

        public TennisScore Award(CourtSide player)
        {
            RequirePlayer(player);
            if (Winner.HasValue) return this;
            return player == CourtSide.Near ? new TennisScore(checked(NearPoints + 1), FarPoints) :
                new TennisScore(NearPoints, checked(FarPoints + 1));
        }

        public string Display(CourtSide player)
        {
            RequirePlayer(player);
            if (Winner == player) return "WIN";
            if (Advantage == player) return "AD";
            int points = player == CourtSide.Near ? NearPoints : FarPoints;
            return points >= 3 ? "40" : points == 2 ? "30" : points == 1 ? "15" : "0";
        }

        internal static void RequirePlayer(CourtSide player)
        {
            if (player != CourtSide.Near && player != CourtSide.Far)
                throw new ArgumentOutOfRangeException(nameof(player));
        }
    }

    public sealed class PointLedger
    {
        private readonly HashSet<uint> completedRallies = new HashSet<uint>();
        public TennisScore Score { get; private set; }

        public PointLedger(TennisScore initial = default) { Score = initial; }

        public bool TryAward(uint rallyId, CourtSide player)
        {
            if (rallyId == 0) throw new ArgumentOutOfRangeException(nameof(rallyId));
            TennisScore.RequirePlayer(player);
            if (Score.Winner.HasValue || completedRallies.Contains(rallyId)) return false;
            var next = Score.Award(player);
            completedRallies.Add(rallyId);
            Score = next;
            return true;
        }
    }
}
