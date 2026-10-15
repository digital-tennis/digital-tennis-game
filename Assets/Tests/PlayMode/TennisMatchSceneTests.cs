using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class TennisMatchSceneTests
    {
        private static IEnumerator Until(Func<bool> condition, float timeout = 18f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for match transition.");
        }

        private static IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealRallyScoresAndRepositionsForNextDiagonalServe()
        {
            yield return LoadMatch();
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            Assert.That(controller, Is.Not.Null);
            yield return Until(() => controller.Match.Phase == MatchPhase.PointEnd);
            controller.AutoServe = false;
            Assert.That(controller.Match.Score.NearPoints, Is.EqualTo(1));
            Assert.That(controller.Match.CurrentShot.ShotId, Is.EqualTo(5), "Serve plus four actual racket hits must precede this point.");
            Assert.That(controller.Match.Score.Display(CourtSide.Near), Is.EqualTo("15"));
            Assert.That(controller.Ball.Simulation.IsResting, Is.True);
            yield return Until(() => controller.Match.Phase == MatchPhase.WaitServe);
            Assert.That(controller.Match.RallyId, Is.EqualTo(2));
            Assert.That(controller.Match.Server, Is.EqualTo(CourtSide.Near));
            Assert.That(controller.Match.ServeSide, Is.EqualTo(ServeSide.Left));
            Assert.That(controller.Ball.Simulation.HasShot, Is.False);
            Assert.That(controller.Ball.transform.position.x, Is.LessThan(0f));
            Assert.That(controller.Serve(), Is.True);
            Assert.That(controller.Serve(), Is.False);
            yield return Until(() => controller.Match.Phase == MatchPhase.Rally);
            Assert.That(controller.Match.Score.NearPoints, Is.EqualTo(1));
            Assert.That(controller.Match.CurrentShot.ServiceBox, Is.EqualTo(ServiceBoxSide.PositiveX));
        }

        [UnityTest]
        public IEnumerator PhysicalLetPreservesFaultAndDoubleFaultAdvancesPoint()
        {
            yield return LoadMatch();
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            controller.AutoServe = false;
            controller.DemoReturns = false;
            Assert.That(controller.Serve(ServeVariant.Fault), Is.True);
            yield return Until(() => controller.Match.Phase == MatchPhase.WaitServe);
            Assert.That(controller.Match.FaultCount, Is.EqualTo(1));
            Assert.That(controller.Serve(ServeVariant.Let), Is.True);
            yield return Until(() => controller.Match.Phase == MatchPhase.WaitServe);
            Assert.That(controller.Match.Status, Does.Contain("Let"));
            Assert.That(controller.Match.FaultCount, Is.EqualTo(1));
            Assert.That(controller.Match.Score.TotalPoints, Is.EqualTo(0));
            yield return Until(() => controller.ReadyToServe);
            Assert.That(controller.Serve(ServeVariant.Fault), Is.True);
            yield return Until(() => controller.Match.Phase == MatchPhase.PointEnd);
            Assert.That(controller.Match.Score.FarPoints, Is.EqualTo(1));
            Assert.That(controller.Match.Status, Does.Contain("Double fault"));
            yield return Until(() => controller.Match.Phase == MatchPhase.WaitServe);
            Assert.That(controller.Match.FaultCount, Is.EqualTo(0));
            Assert.That(controller.Match.ServeSide, Is.EqualTo(ServeSide.Left));
        }

        [UnityTest]
        public IEnumerator AutomaticMatchReachesWinAndDoesNotRestartItself()
        {
            yield return LoadMatch();
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            yield return Until(() => controller.Match.Phase == MatchPhase.MatchEnd, 50f);
            Assert.That(controller.Match.Score.NearPoints, Is.EqualTo(4));
            Assert.That(controller.Match.Score.FarPoints, Is.EqualTo(0));
            Assert.That(controller.Match.Score.Winner, Is.EqualTo(CourtSide.Near));
            uint rally = controller.Match.RallyId;
            yield return new WaitForSeconds(2f);
            Assert.That(controller.Match.Phase, Is.EqualTo(MatchPhase.MatchEnd));
            Assert.That(controller.Match.RallyId, Is.EqualTo(rally));
            Assert.That(controller.Serve(), Is.False);
        }
    }
}
