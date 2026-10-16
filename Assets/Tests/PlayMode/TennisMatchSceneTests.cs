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
        public IEnumerator AutomaticGameContinuesWithChangedServerAndEnds()
        {
            yield return LoadMatch();
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            yield return Until(() => controller.Match.Progress.GameComplete, 50f);
            controller.AutoServe=false;
            Assert.That(controller.Match.Score.NearPoints, Is.EqualTo(4));
            Assert.That(controller.Match.Score.FarPoints, Is.EqualTo(0));
            Assert.That(controller.Match.Score.Winner, Is.EqualTo(CourtSide.Near));
            Assert.That(controller.Match.Winner,Is.Null);Assert.That(controller.Match.Progress.NearGames,Is.EqualTo(1));
            yield return Until(()=>controller.ReadyToServe);
            Assert.That(controller.Match.Score.TotalPoints,Is.Zero);Assert.That(controller.Match.Server,Is.EqualTo(CourtSide.Far));
            Assert.That(controller.FarAvatar.transform.position.z,Is.LessThan(0));Assert.That(controller.NearAvatar.transform.position.z,Is.GreaterThan(0));
            Assert.That(controller.View.Camera.transform.position.z,Is.GreaterThan(0));
            Assert.That(controller.Serve(),Is.True);
            yield return Until(()=>controller.Match.Phase==MatchPhase.Rally);
            Assert.That(controller.Match.CurrentShot.HitterEnd,Is.EqualTo(CourtSide.Near));
        }

        [UnityTest]
        public IEnumerator PhysicalPointsCompleteTwoSetsAndStopWithoutRestarting()
        {
            yield return LoadMatch();var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.AutoServe=c.DemoReturns=false;float previous=Time.timeScale;Time.timeScale=4;
            int serves=0;uint lastServeRally=0;bool sawSecondSet=false;
            try
            {
                float end=Time.realtimeSinceStartup+150;
                while(c.Match.Phase!=MatchPhase.MatchEnd&&Time.realtimeSinceStartup<end)
                {
                    if(c.ReadyToServe&&lastServeRally!=c.Match.RallyId){c.InputAdapter.Press(PlayerAction.Serve,c.Match.Server==CourtSide.Far);lastServeRally=c.Match.RallyId;serves++;}
                    if(c.CanReturn(CourtSide.Near))
                    {
                        float facing=c.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                        float aim=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*1.6f*facing,17)*Mathf.Rad2Deg/c.Tuning["shot.yaw"],-1,1);
                        Assert.That(c.SubmitInput(c.CreateVirtualInput(CourtSide.Near,StrokeKind.Forehand,.35f,aim)),Is.EqualTo(SwingResult.Hit));
                    }
                    if(c.Match.Progress.NearSets==1&&!c.Match.Progress.SetComplete)sawSecondSet=true;
                    // At 4x speed a rendered frame may span the entire legal return window.
                    // Poll on physics ticks, preserving the actual reach/contact gate.
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.MatchEnd));Assert.That(c.Match.Winner,Is.EqualTo(CourtSide.Near));
                Assert.That(c.Match.Progress.NearSets,Is.EqualTo(2));Assert.That(c.Match.Progress.FarSets,Is.Zero);
                Assert.That(c.Match.Progress.CompletedSets.Length,Is.EqualTo(2));Assert.That(sawSecondSet,Is.True);
                Assert.That(c.Match.Progress.TotalPoints,Is.EqualTo(48));Assert.That(serves,Is.EqualTo(48));
                Assert.That(c.Match.Progress.CompletedSets[0].Display(),Is.EqualTo("6–0"));Assert.That(c.Match.Progress.CompletedSets[1].Display(),Is.EqualTo("6–0"));
                uint rally=c.Match.RallyId;yield return new WaitForSeconds(2);
                Assert.That(c.Match.RallyId,Is.EqualTo(rally));Assert.That(c.Serve(),Is.False);
            }finally{Time.timeScale=previous;}
        }
    }
}
