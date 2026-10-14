using System;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class TennisRulesTests
    {
        private ulong eventId;
        private BallEvent Event(TennisMatch match, BallEventKind kind, int bounces = 0, bool isIn = false)
            => new BallEvent(match.CurrentShot, ++eventId, kind, new Vector3(0f, 0f, 4f), bounces, isIn);

        [TestCase(0, "0")]
        [TestCase(1, "15")]
        [TestCase(2, "30")]
        [TestCase(3, "40")]
        public void DisplaysTraditionalPointValues(int points, string display)
        {
            Assert.That(new TennisScore(points, 0).Display(CourtSide.Near), Is.EqualTo(display));
        }

        [Test]
        public void DeuceAdvantageAndWinUseTwoPointMargin()
        {
            var score = new TennisScore(3, 3);
            Assert.That(score.IsDeuce, Is.True);
            score = score.Award(CourtSide.Near);
            Assert.That(score.Advantage, Is.EqualTo(CourtSide.Near));
            Assert.That(score.Display(CourtSide.Near), Is.EqualTo("AD"));
            Assert.That(score.Winner, Is.Null);
            score = score.Award(CourtSide.Far);
            Assert.That(score.IsDeuce, Is.True);
            score = score.Award(CourtSide.Near).Award(CourtSide.Near);
            Assert.That(score.NearPoints, Is.EqualTo(6));
            Assert.That(score.FarPoints, Is.EqualTo(4));
            Assert.That(score.Winner, Is.EqualTo(CourtSide.Near));
            Assert.That(score.Award(CourtSide.Far).FarPoints, Is.EqualTo(4));
        }

        [Test]
        public void LongDeuceAndFarPlayerWinAreSupported()
        {
            var score = new TennisScore(3, 3);
            for (int i = 0; i < 1000; i++) score = score.Award(CourtSide.Near).Award(CourtSide.Far);
            Assert.That(score.IsDeuce, Is.True);
            Assert.That(score.Display(CourtSide.Far), Is.EqualTo("40"));
            Assert.That(score.Award(CourtSide.Far).Award(CourtSide.Far).Winner, Is.EqualTo(CourtSide.Far));
        }

        [Test]
        public void RallyLedgerKeepsFirstJudgmentAndIgnoresResultsAfterWin()
        {
            var ledger = new PointLedger();
            Assert.That(ledger.TryAward(1, CourtSide.Near), Is.True);
            Assert.That(ledger.TryAward(1, CourtSide.Far), Is.False);
            Assert.That(ledger.Score.FarPoints, Is.EqualTo(0));
            for (uint id = 2; id <= 4; id++) ledger.TryAward(id, CourtSide.Near);
            Assert.That(ledger.Score.Winner, Is.EqualTo(CourtSide.Near));
            Assert.That(ledger.TryAward(5, CourtSide.Far), Is.False);
            Assert.That(ledger.Score.NearPoints, Is.EqualTo(4));
        }

        [Test]
        public void InvalidScoreAndPlayerInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TennisScore(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TennisScore().Award((CourtSide)42));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PointLedger().TryAward(0, CourtSide.Near));
        }

        [Test]
        public void OnlyServerCanServeAndServiceCannotBeReturnedBeforeLanding()
        {
            var match = new TennisMatch();
            Assert.That(match.TryServe(CourtSide.Far), Is.False);
            Assert.That(match.TryReturn(CourtSide.Far), Is.False);
            Assert.That(match.TryServe(CourtSide.Near), Is.True);
            Assert.That(match.CurrentShot.ServiceBox, Is.EqualTo(ServiceBoxSide.NegativeX));
            Assert.That(match.TryServe(CourtSide.Near), Is.False);
            Assert.That(match.TryReturn(CourtSide.Far), Is.False);
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Rally));
            Assert.That(match.TryReturn(CourtSide.Near), Is.False);
            Assert.That(match.TryReturn(CourtSide.Far), Is.True);
            Assert.That(match.CurrentShot.Area, Is.EqualTo(LandingArea.SinglesCourt));
            Assert.That(match.BounceCount, Is.EqualTo(0));
        }

        [Test]
        public void DoubleFaultAwardsReceiverAndNextPointFlipsSideWithSameServer()
        {
            var match = new TennisMatch();
            match.TryServe(CourtSide.Near);
            var oldShot = match.CurrentShot;
            var firstFault = Event(match, BallEventKind.Bounce, 1, false);
            match.HandleBallEvent(firstFault);
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.WaitServe));
            Assert.That(match.FaultCount, Is.EqualTo(1));
            Assert.That(match.Score.TotalPoints, Is.EqualTo(0));
            match.TryServe(CourtSide.Near);
            Assert.That(match.HandleBallEvent(firstFault), Is.False);
            Assert.That(match.HandleBallEvent(new BallEvent(oldShot, ++eventId, BallEventKind.Out, Vector3.zero, 1, false)), Is.False);
            var fault = Event(match, BallEventKind.Bounce, 1, false);
            match.HandleBallEvent(fault);
            match.HandleBallEvent(fault);
            match.HandleBallEvent(Event(match, BallEventKind.Out));
            Assert.That(match.Score.FarPoints, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.PointEnd));
            Assert.That(match.FaultCount, Is.EqualTo(0));
            Assert.That(match.TryServe(CourtSide.Near), Is.False);
            Assert.That(match.AdvanceToNextPoint(), Is.True);
            Assert.That(match.RallyId, Is.EqualTo(2));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Near));
            Assert.That(match.ServeSide, Is.EqualTo(ServeSide.Left));
            Assert.That(match.ServeFromPositiveX, Is.False);
            match.TryServe(CourtSide.Near);
            Assert.That(match.CurrentShot.ServiceBox, Is.EqualTo(ServiceBoxSide.PositiveX));
        }

        [Test]
        public void LetPreservesFirstFaultButNetPlusOutIsDoubleFault()
        {
            var match = new TennisMatch();
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Out));
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Net));
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.WaitServe));
            Assert.That(match.FaultCount, Is.EqualTo(1));
            Assert.That(match.Score.TotalPoints, Is.EqualTo(0));
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Net));
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, false));
            Assert.That(match.Score.FarPoints, Is.EqualTo(1));
            Assert.That(match.Status, Does.Contain("Double fault"));
        }

        [Test]
        public void SecondBounceScoresOnceAndOldRallyCannotScoreAgain()
        {
            var match = new TennisMatch();
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            var repeatedFirst = Event(match, BallEventKind.Bounce, 1, true);
            Assert.That(match.HandleBallEvent(repeatedFirst), Is.False);
            var second = Event(match, BallEventKind.Bounce, 2, false);
            match.HandleBallEvent(second);
            Assert.That(match.Score.Display(CourtSide.Near), Is.EqualTo("15"));
            Assert.That(match.HandleBallEvent(second), Is.False);
            match.AdvanceToNextPoint();
            match.TryServe(CourtSide.Near);
            Assert.That(match.HandleBallEvent(second), Is.False);
            Assert.That(match.Score.NearPoints, Is.EqualTo(1));
        }

        [Test]
        public void RallyNetContactWaitsForLandingAndOutAwardsOppositeHitter()
        {
            var match = new TennisMatch();
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            match.TryReturn(CourtSide.Far);
            match.HandleBallEvent(Event(match, BallEventKind.Net));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.Rally));
            Assert.That(match.Score.TotalPoints, Is.EqualTo(0));
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, false));
            Assert.That(match.Score.NearPoints, Is.EqualTo(1));
        }

        [Test]
        public void StoppedServeFaultsAndStoppedValidRallyScores()
        {
            var match = new TennisMatch();
            match.TryServe(CourtSide.Near);
            match.ResolveStoppedBall(match.CurrentShot.ShotId);
            Assert.That(match.FaultCount, Is.EqualTo(1));
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            Assert.That(match.FaultCount, Is.EqualTo(0));
            match.ResolveStoppedBall(match.CurrentShot.ShotId);
            Assert.That(match.Score.NearPoints, Is.EqualTo(1));
        }

        [Test]
        public void FarServerUsesMirroredDiagonalBox()
        {
            var match = new TennisMatch(CourtSide.Far);
            Assert.That(match.ServeFromPositiveX, Is.False);
            match.TryServe(CourtSide.Far);
            Assert.That(match.CurrentShot.ServiceBox, Is.EqualTo(ServiceBoxSide.PositiveX));
        }

        [Test]
        public void WinningPointEndsGameAndDisablesFurtherInputs()
        {
            var match = new TennisMatch(CourtSide.Near, new TennisScore(3, 0));
            match.TryServe(CourtSide.Near);
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 1, true));
            match.HandleBallEvent(Event(match, BallEventKind.Bounce, 2, true));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.MatchEnd));
            Assert.That(match.Score.Winner, Is.EqualTo(CourtSide.Near));
            Assert.That(match.AdvanceToNextPoint(), Is.False);
            Assert.That(match.TryServe(CourtSide.Near), Is.False);
            Assert.That(match.TryReturn(CourtSide.Far), Is.False);
        }
    }
}
