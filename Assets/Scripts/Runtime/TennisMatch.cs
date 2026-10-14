using System.Collections.Generic;

namespace DigitalTennis
{
    public enum MatchPhase { WaitServe, InFlight, Rally, PointEnd, MatchEnd }

    /// <summary>Rules for one game. Inputs and ball events are accepted only in their valid phase.</summary>
    public sealed class TennisMatch
    {
        private readonly PointLedger ledger;
        private readonly HashSet<ulong> handledEvents = new HashSet<ulong>();
        private uint shotSequence;
        public MatchPhase Phase { get; private set; }
        public CourtSide Server { get; }
        public CourtSide LastHitter { get; private set; }
        public uint RallyId { get; private set; } = 1;
        public ShotContext CurrentShot { get; private set; }
        public int FaultCount { get; private set; }
        public int BounceCount { get; private set; }
        public bool NetTouched { get; private set; }
        public string Status { get; private set; } = "Ready for first serve";
        public TennisScore Score => ledger.Score;
        public ServeSide ServeSide => Score.NextServeSide;
        public bool ServeFromPositiveX => (Server == CourtSide.Near) == (ServeSide == ServeSide.Right);

        public TennisMatch(CourtSide server = CourtSide.Near, TennisScore initial = default)
        {
            TennisScore.RequirePlayer(server);
            Server = server;
            LastHitter = server;
            ledger = new PointLedger(initial);
            Phase = initial.Winner.HasValue ? MatchPhase.MatchEnd : MatchPhase.WaitServe;
        }

        public static CourtSide Opponent(CourtSide player) => player == CourtSide.Near ? CourtSide.Far : CourtSide.Near;

        public bool TryServe(CourtSide player)
        {
            if (Phase != MatchPhase.WaitServe || player != Server) return false;
            NewShot(player, LandingArea.ServiceBox);
            Phase = MatchPhase.InFlight;
            Status = FaultCount == 0 ? "First serve in flight" : "Second serve in flight";
            return true;
        }

        public bool CanReturn(CourtSide player) => Phase == MatchPhase.Rally && player == Opponent(LastHitter);

        public bool TryReturn(CourtSide player)
        {
            if (!CanReturn(player)) return false;
            NewShot(player, LandingArea.SinglesCourt);
            Status = player + " returned the ball";
            return true;
        }

        private void NewShot(CourtSide player, LandingArea area)
        {
            LastHitter = player;
            BounceCount = 0;
            NetTouched = false;
            handledEvents.Clear();
            var box = ServeFromPositiveX ? ServiceBoxSide.NegativeX : ServiceBoxSide.PositiveX;
            CurrentShot = new ShotContext(RallyId, player, area, box, checked(++shotSequence));
        }

        public bool HandleBallEvent(BallEvent item)
        {
            if ((Phase != MatchPhase.InFlight && Phase != MatchPhase.Rally) || item.RallyId != RallyId ||
                item.ShotId != CurrentShot.ShotId || item.LastHitter != LastHitter || item.EventId == 0 ||
                !handledEvents.Add(item.EventId)) return false;
            if (item.Kind == BallEventKind.Net)
            {
                NetTouched = true;
                Status = "Net touched - waiting for landing";
                return true;
            }
            if (item.Kind == BallEventKind.Out)
            {
                if (Phase == MatchPhase.InFlight) ServiceFault();
                else AwardPoint(Opponent(LastHitter), "Ball out");
                return true;
            }
            if (item.Kind != BallEventKind.Bounce || item.BounceCount <= BounceCount) return false;
            if (Phase == MatchPhase.InFlight && item.BounceCount != 1) return false;
            BounceCount = item.BounceCount;
            if (Phase == MatchPhase.InFlight)
            {
                // An out landing is a fault even when this serve touched the net.
                if (!item.IsInBounds) ServiceFault();
                else if (NetTouched)
                {
                    Phase = MatchPhase.WaitServe;
                    Status = "Let - repeat the same serve";
                }
                else
                {
                    FaultCount = 0;
                    Phase = MatchPhase.Rally;
                    Status = "Valid serve - rally";
                }
            }
            else if (BounceCount >= 2) AwardPoint(LastHitter, "Second bounce");
            else if (!item.IsInBounds) AwardPoint(Opponent(LastHitter), "Ball out");
            return true;
        }

        public bool ResolveStoppedBall(uint shotId)
        {
            if (shotId != CurrentShot.ShotId || CurrentShot.ShotId == 0) return false;
            if (Phase == MatchPhase.InFlight) { ServiceFault(); return true; }
            if (Phase != MatchPhase.Rally) return false;
            AwardPoint(BounceCount >= 1 ? LastHitter : Opponent(LastHitter), "Ball stopped");
            return true;
        }

        private void ServiceFault()
        {
            if (FaultCount == 0)
            {
                FaultCount = 1;
                Phase = MatchPhase.WaitServe;
                Status = "First fault - second serve";
            }
            else
            {
                FaultCount = 0;
                AwardPoint(Opponent(Server), "Double fault");
            }
        }

        private void AwardPoint(CourtSide player, string reason)
        {
            if (!ledger.TryAward(RallyId, player)) return;
            FaultCount = 0;
            Phase = Score.Winner.HasValue ? MatchPhase.MatchEnd : MatchPhase.PointEnd;
            Status = player + " wins the point: " + reason;
        }

        public bool AdvanceToNextPoint()
        {
            if (Phase != MatchPhase.PointEnd) return false;
            RallyId = checked(RallyId + 1);
            FaultCount = 0;
            BounceCount = 0;
            NetTouched = false;
            LastHitter = Server;
            Phase = MatchPhase.WaitServe;
            Status = "Ready for next serve";
            return true;
        }
    }
}
