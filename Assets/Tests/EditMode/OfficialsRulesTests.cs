using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class OfficialsRulesTests
    {
        private ulong id;
        private void Ball(TennisMatch m, BallEventKind kind, int bounce = 0, bool inside = false)
            => m.HandleBallEvent(new BallEvent(m.CurrentShot, ++id, kind, Vector3.zero, bounce, inside));

        [Test]
        public void RallyNetCordThatLandsGoodStaysLiveAndSecondBounceOutsideWinsForHitter()
        {
            var m = new TennisMatch();
            m.TryServe(CourtSide.Near); Ball(m, BallEventKind.Bounce, 1, true);
            m.TryReturn(CourtSide.Far); Ball(m, BallEventKind.Net);
            Ball(m, BallEventKind.Bounce, 1, true);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Rally));
            Assert.That(m.Score.TotalPoints, Is.Zero);
            Ball(m, BallEventKind.Out, 1); // Leaving the court after a good bounce is still playable.
            Assert.That(m.Score.TotalPoints, Is.Zero);
            Ball(m, BallEventKind.Bounce, 2, false);
            Assert.That(m.Score.FarPoints, Is.EqualTo(1));
            Assert.That(m.Call, Is.EqualTo(UmpireCall.SecondBounce));
        }

        [Test]
        public void ReturnCannotScoreFromASkippedFirstBounce()
        {
            var m = new TennisMatch();
            m.TryServe(CourtSide.Near); Ball(m, BallEventKind.Bounce, 1, true);
            m.TryReturn(CourtSide.Far);
            Ball(m, BallEventKind.Bounce, 2, true);
            Assert.That(m.Score.TotalPoints, Is.Zero);
            Assert.That(m.BounceCount, Is.Zero);
            Ball(m, BallEventKind.Bounce, 1, false);
            Assert.That(m.Score.NearPoints, Is.EqualTo(1));
        }

        [Test]
        public void SecondServiceLetsKeepFaultAndCallSequenceWhileDoubleFaultAwardsExactlyOnce()
        {
            var m = new TennisMatch();
            m.TryServe(CourtSide.Near); Ball(m, BallEventKind.Out);
            Assert.That(m.Call, Is.EqualTo(UmpireCall.Fault));
            uint call = m.CallId;
            for (int i = 0; i < 3; i++)
            {
                m.TryServe(CourtSide.Near); Ball(m, BallEventKind.Net); Ball(m, BallEventKind.Bounce, 1, true);
                Assert.That(m.Call, Is.EqualTo(UmpireCall.Let));
                Assert.That(m.CallText, Does.Contain("second serve again"));
                Assert.That(m.FaultCount, Is.EqualTo(1));
                Assert.That(m.CallId, Is.GreaterThan(call)); call = m.CallId;
            }
            m.TryServe(CourtSide.Near); Ball(m, BallEventKind.Net);
            m.ResolveStoppedBall(m.CurrentShot.ShotId);
            Assert.That(m.Call, Is.EqualTo(UmpireCall.DoubleFault));
            Assert.That(m.CallText, Does.Contain("0 - 15"));
            Assert.That(m.Score.FarPoints, Is.EqualTo(1));
            Assert.That(m.ResolveStoppedBall(m.CurrentShot.ShotId), Is.False);
        }

        [Test]
        public void UmpireCallsDeuceAdvantageAndGameInServerOrder()
        {
            foreach (var initial in new[] { new TennisScore(2, 3), new TennisScore(3, 3), new TennisScore(4, 3) })
            {
                var m = new TennisMatch(CourtSide.Far, initial);
                m.TryServe(CourtSide.Far); Ball(m, BallEventKind.Out);
                m.TryServe(CourtSide.Far); Ball(m, BallEventKind.Out);
                Assert.That(m.CallText, Does.Contain(initial.NearPoints == 2 ? "Deuce" : initial.NearPoints == 3 ? "Advantage Near" : "Game, Near"));
            }
        }

        [TestCase(-.2f)]
        [TestCase(.2f)]
        public void RecoveryMovesContinuouslyAroundNetAndReturnsWithoutDuplicatePickup(float z)
        {
            var r = new BallRecovery();
            Assert.That(r.Begin(1, new Vector3(0, .12f, z)), Is.True);
            Assert.That(r.Begin(2, Vector3.one), Is.False);
            bool picked = false, carried = false, done = false;
            for (int i = 0; i < 1000 && !done; i++)
            {
                Vector3 previous = r.Position;
                done = r.Tick(1f / 60);
                Assert.That(Vector3.Distance(previous, r.Position), Is.LessThanOrEqualTo(BallRecovery.RunSpeed / 60 + .0001f));
                if (Mathf.Abs(r.Position.z) < .16f) Assert.That(r.Position.x, Is.GreaterThan(4.8f));
                picked |= r.Phase == RecoveryPhase.PickingUp;
                carried |= r.Phase == RecoveryPhase.Returning;
                if (r.Phase == RecoveryPhase.Returning)
                    Assert.That(Vector3.Distance(r.BallPosition, r.Position), Is.InRange(.6f, .9f));
            }
            Assert.That(picked && carried && done, Is.True);
            Assert.That(Mathf.Abs(r.Position.x), Is.GreaterThan(CourtBounds.SinglesHalfWidth));
            Assert.That(r.Completed, Is.EqualTo(1));
            Assert.That(r.Begin(1, Vector3.one), Is.False);
            Assert.That(r.Tick(.1f), Is.False);
        }
    }
}
