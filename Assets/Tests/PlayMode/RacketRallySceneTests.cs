using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class RacketRallySceneTests
    {
        private static IEnumerator Until(Func<bool> condition, float seconds = 8f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "Expected racket/rally condition was not reached.");
        }

        [UnityTest]
        public IEnumerator BothPlayersMoveAndUseBothStrokesThenAdvanceToNextServe()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");
            yield return null;
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            controller.AutoServe = false;
            controller.DemoReturns = false;
            Assert.That(controller.Serve(), Is.True);
            var strokes = new[] { StrokeKind.Forehand, StrokeKind.Backhand, StrokeKind.Backhand,
                StrokeKind.Forehand, StrokeKind.Forehand, StrokeKind.Backhand };
            for (int i = 0; i < strokes.Length; i++)
            {
                CourtSide receiver = i % 2 == 0 ? CourtSide.Far : CourtSide.Near;
                yield return Until(() => controller.CanReturn(receiver) && controller.Ball.Simulation.BounceCount == 1 &&
                    controller.Ball.Simulation.Position.y >= 0.95f);
                Assert.That(controller.Match.Score.TotalPoints, Is.EqualTo(0));
                var input = controller.CreateVirtualInput(receiver, strokes[i]);
                Vector3 before = controller.Ball.Simulation.Position;
                Assert.That(controller.SubmitInput(input), Is.EqualTo(SwingResult.Hit));
                uint shot = controller.Match.CurrentShot.ShotId;
                Assert.That(controller.SubmitInput(input), Is.EqualTo(SwingResult.Duplicate));
                Assert.That(controller.Match.CurrentShot.ShotId, Is.EqualTo(shot));
                Assert.That(controller.Ball.Simulation.Position, Is.EqualTo(before), "A hit must not teleport the ball.");
                Assert.That(Mathf.Sign(controller.Ball.Simulation.Velocity.z), Is.EqualTo(receiver == CourtSide.Near ? 1 : -1));
                Assert.That(controller.Ball.Simulation.Velocity.magnitude, Is.EqualTo(14.2f).Within(0.001f));
            }
            Assert.That(controller.Hits, Is.EqualTo(6));
            Assert.That(controller.ForehandHits, Is.EqualTo(3));
            Assert.That(controller.BackhandHits, Is.EqualTo(3));
            Assert.That(controller.NearHits, Is.EqualTo(3));
            Assert.That(controller.FarHits, Is.EqualTo(3));
            Assert.That(controller.NearAvatar.DistanceTravelled, Is.GreaterThan(2f));
            Assert.That(controller.FarAvatar.DistanceTravelled, Is.GreaterThan(2f));
            Assert.That(controller.NearAvatar.transform.position.z, Is.LessThan(0));
            Assert.That(controller.FarAvatar.transform.position.z, Is.GreaterThan(0));
            yield return Until(() => controller.Match.Phase == MatchPhase.PointEnd);
            Assert.That(controller.Match.Score.NearPoints, Is.EqualTo(1));
            yield return Until(() => controller.Match.Phase == MatchPhase.WaitServe);
            Assert.That(controller.Match.RallyId, Is.EqualTo(2));
            Assert.That(controller.Match.ServeSide, Is.EqualTo(ServeSide.Left));
            Assert.That(controller.Serve(), Is.True);
            yield return Until(() => controller.CanReturn(CourtSide.Far));
            Assert.That(controller.VirtualSwing(CourtSide.Far, StrokeKind.Backhand), Is.EqualTo(SwingResult.Hit));
        }

        [UnityTest]
        public IEnumerator EarlySwingAndOldInputCannotHitLaterOrMoveNonAuthoritativePlayers()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");
            yield return null;
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            controller.AutoServe = false;
            controller.DemoReturns = false;
            controller.Serve();
            var early = controller.CreateVirtualInput(CourtSide.Far, StrokeKind.Forehand);
            Assert.That(controller.SubmitInput(early), Is.EqualTo(SwingResult.WrongPhase));
            yield return Until(() => controller.CanReturn(CourtSide.Far));
            Assert.That(controller.SubmitInput(early), Is.EqualTo(SwingResult.Duplicate));
            var delayed = controller.CreateVirtualInput(CourtSide.Far, StrokeKind.Backhand);
            var hit = controller.CreateVirtualInput(CourtSide.Far, StrokeKind.Forehand);
            Assert.That(controller.SubmitInput(hit), Is.EqualTo(SwingResult.Hit));
            Assert.That(controller.SubmitInput(delayed), Is.EqualTo(SwingResult.Stale));
            controller.Ball.IsAuthoritative = false;
            Vector3 near = controller.NearAvatar.transform.position, far = controller.FarAvatar.transform.position;
            var remote = controller.CreateVirtualInput(CourtSide.Near, StrokeKind.Backhand);
            Assert.That(controller.SubmitInput(remote), Is.EqualTo(SwingResult.NotAuthority));
            yield return new WaitForSeconds(0.2f);
            Assert.That(controller.NearAvatar.transform.position, Is.EqualTo(near));
            Assert.That(controller.FarAvatar.transform.position, Is.EqualTo(far));
        }
    }
}
