using System;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class RacketPlayTests
    {
        private static RacketInput Input(CourtSide player = CourtSide.Near, StrokeKind stroke = StrokeKind.Forehand,
            uint eventId = 1, uint session = 1, float power = 0.5f, float x = 0f, float z = 0f,
            double received = 10, uint shot = 1) => new RacketInput(player, stroke, session, eventId, power, x, z, received, 1, shot);

        private static TennisMatch Rally()
        {
            var match = new TennisMatch(CourtSide.Far);
            match.TryServe(CourtSide.Far);
            match.HandleBallEvent(new BallEvent(match.CurrentShot, 1, BallEventKind.Bounce, new Vector3(0, 0, -4), 1, true));
            return match;
        }
        private static BallSimulation Incoming(TennisMatch match, Vector3? at = null)
        {
            var ball = new BallSimulation();
            ball.Launch(at ?? new Vector3(0, 1, -7), new Vector3(0, 2, -10), match.CurrentShot);
            return ball;
        }

        [TestCase(0f, 10f)]
        [TestCase(0.5f, 16f)]
        [TestCase(1f, 22f)]
        public void ShotUsesContractSpeedAndTwentyDegreeNeutralElevation(float power, float speed)
        {
            Vector3 velocity = RacketShot.Velocity(Input(power: power));
            Assert.That(velocity.magnitude, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(velocity.x, Is.EqualTo(0).Within(0.0001f));
            Assert.That(Mathf.Atan2(velocity.y, velocity.z) * Mathf.Rad2Deg, Is.EqualTo(20).Within(0.0001f));
        }

        [TestCase(-1f, -25f, 10f)]
        [TestCase(1f, 25f, 30f)]
        public void AimingAndFarCourtRotationAreAppliedOnce(float aim, float yaw, float pitch)
        {
            Vector3 near = RacketShot.Velocity(Input(x: aim, z: aim));
            Vector3 far = RacketShot.Velocity(Input(CourtSide.Far, StrokeKind.Backhand, x: aim, z: aim));
            Assert.That(Mathf.Atan2(near.x, near.z) * Mathf.Rad2Deg, Is.EqualTo(yaw).Within(0.0001f));
            Assert.That(Mathf.Asin(near.y / near.magnitude) * Mathf.Rad2Deg, Is.EqualTo(pitch).Within(0.0001f));
            Assert.That(far.x, Is.EqualTo(-near.x).Within(0.0001f));
            Assert.That(far.z, Is.EqualTo(-near.z).Within(0.0001f));
            Assert.That(far.y, Is.EqualTo(near.y).Within(0.0001f));
        }

        [Test]
        public void BothStrokesPreserveTheSameAimAndRejectInvalidValues()
        {
            Assert.That(RacketShot.Velocity(Input(stroke: StrokeKind.Backhand)), Is.EqualTo(RacketShot.Velocity(Input())));
            foreach (var input in new[] { Input(power: float.NaN), Input(x: float.PositiveInfinity), Input(z: 1.01f),
                Input(power: -0.01f), Input(eventId: 0), Input(session: 0), Input(stroke: (StrokeKind)3) })
                Assert.Throws<ArgumentException>(() => RacketShot.Velocity(input));
        }

        [Test]
        public void MissIsConsumedAndCannotTurnIntoHitOnRetransmission()
        {
            var match = Rally(); var ball = Incoming(match); var gate = new RacketInputGate();
            Assert.That(gate.Evaluate(Input(), 10, match, ball, new Vector3(3, 0.85f, -7), true), Is.EqualTo(SwingResult.OutOfReach));
            Assert.That(gate.Evaluate(Input(), 10, match, ball, new Vector3(0, 0.85f, -7), true), Is.EqualTo(SwingResult.Duplicate));
            Assert.That(gate.Evaluate(Input(eventId: 2), 10, match, ball, new Vector3(0, 0.85f, -7), true), Is.EqualTo(SwingResult.Hit));
            Assert.That(gate.Evaluate(Input(session: 2), 10, match, ball, new Vector3(0, 0.85f, -7), true), Is.EqualTo(SwingResult.Hit));
        }

        [Test]
        public void LateInputOldShotWrongPhaseAndNonAuthorityCannotHit()
        {
            var match = Rally(); var ball = Incoming(match); var gate = new RacketInputGate();
            Vector3 player = new Vector3(0, 0.85f, -7);
            Assert.That(gate.Evaluate(Input(), 10.251, match, ball, player, true), Is.EqualTo(SwingResult.Stale));
            Assert.That(gate.Evaluate(Input(eventId: 2, shot: 2), 10, match, ball, player, true), Is.EqualTo(SwingResult.Stale));
            Assert.That(gate.Evaluate(Input(eventId: 3, received: 11), 10, match, ball, player, true), Is.EqualTo(SwingResult.Stale));
            Assert.That(gate.Evaluate(Input(eventId: 4), 10, match, ball, player, false), Is.EqualTo(SwingResult.NotAuthority));
            match.TryReturn(CourtSide.Near);
            Assert.That(gate.Evaluate(Input(eventId: 5, shot: 2), 10, match, ball, player, true), Is.EqualTo(SwingResult.WrongPhase));
        }

        [TestCase(0.2f, 1.5f, SwingResult.Hit)]
        [TestCase(2.5f, 1.5f, SwingResult.Hit)]
        [TestCase(0.199f, 0f, SwingResult.Height)]
        [TestCase(2.501f, 0f, SwingResult.Height)]
        [TestCase(1f, 1.501f, SwingResult.OutOfReach)]
        public void HitWindowIncludesExactReachAndHeightBoundaries(float height, float distance, SwingResult result)
        {
            var match = Rally(); var ball = Incoming(match, new Vector3(0, height, -7));
            Assert.That(RacketInputGate.CanHit(CourtSide.Near, match, ball, new Vector3(distance, 0.85f, -7)), Is.EqualTo(result));
        }

        [Test]
        public void MissingBallAndBallOnWrongHalfCannotBeHit()
        {
            var match = Rally();
            Assert.That(RacketInputGate.CanHit(CourtSide.Near, match, new BallSimulation(), Vector3.zero), Is.EqualTo(SwingResult.NoBall));
            Assert.That(RacketInputGate.CanHit(CourtSide.Near, match, Incoming(match, new Vector3(0, 1, 5)), Vector3.zero), Is.EqualTo(SwingResult.WrongSide));
        }

        [Test]
        public void MovementHasFiveMetreSpeedLimitAndClampsItsOwnCourt()
        {
            Vector3 start = new Vector3(0, 0.85f, -8);
            Vector3 target = AutomaticCourtMovement.ClampTarget(new Vector3(100, 1, -100), CourtSide.Near, start.y);
            Assert.That(target, Is.EqualTo(new Vector3(CourtBounds.SinglesHalfWidth, start.y, -CourtBounds.HalfLength)));
            Vector3 next = AutomaticCourtMovement.Step(start, target, CourtSide.Near, 0.1f);
            Assert.That(Vector3.Distance(start, next), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(AutomaticCourtMovement.Step(start, target, CourtSide.Near, 100), Is.EqualTo(target));
            Assert.That(AutomaticCourtMovement.Step(start, target, CourtSide.Near, float.NaN), Is.EqualTo(start));
            Assert.That(AutomaticCourtMovement.ClampTarget(new Vector3(-100, 0, -100), CourtSide.Far, 1).z, Is.EqualTo(0.8f));
        }

        [Test]
        public void PredictionUsesRealBounceAndDoesNotAdvanceAuthoritativeBall()
        {
            var ball = new BallSimulation();
            Vector3 start = new Vector3(1.5f, 2.2f, -12.6f), end = new Vector3(-1.5f, 0.12f, 4);
            ball.Launch(start, (end - start) / 1.2f + Vector3.up * (0.5f * ball.Gravity * 1.2f), new ShotContext(1, CourtSide.Near));
            Vector3 target = AutomaticCourtMovement.PredictTarget(ball, CourtSide.Far, 0.85f);
            Assert.That(target.z, Is.InRange(5f, 8f));
            Assert.That(target.x, Is.InRange(-3f, -1f));
            Assert.That(ball.Position, Is.EqualTo(start));
            Assert.That(ball.Tick, Is.EqualTo(0));
            Assert.That(ball.BounceCount, Is.EqualTo(0));
        }
    }
}
