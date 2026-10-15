using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class CourtOfficialsSceneTests
    {
        private static IEnumerator Until(Func<bool> condition, float seconds = 12)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(condition(), Is.True, "Officials transition timed out.");
        }

        [UnityTest]
        public IEnumerator NetFaultIsRetrievedBeforeSecondServeThenDoubleFaultBeforeNextPoint()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.AutoServe = c.DemoReturns = false;
            for (int i = 0; i < 2; i++)
            {
                Assert.That(c.Serve(ServeVariant.NetFault), Is.True);
                yield return Until(() => c.Recovery.Active);
                Assert.That(c.Officials.Call, Is.EqualTo(i == 0 ? UmpireCall.Fault : UmpireCall.DoubleFault));
                Assert.That(c.Match.Score.FarPoints, Is.EqualTo(i));
                Assert.That(c.Serve(), Is.False, "Do not serve while the ball boy is on court.");
                yield return Until(() => c.Recovery.Phase == RecoveryPhase.PickingUp);
                Assert.That(c.Officials.RunnerPosition, Is.EqualTo(c.Recovery.Position));
                yield return Until(() => c.Recovery.Phase == RecoveryPhase.Returning);
                Assert.That(Vector3.Distance(c.Ball.transform.position, c.Officials.RunnerPosition), Is.LessThan(1));
                uint shot = c.Match.CurrentShot.ShotId;
                yield return Until(() => !c.Recovery.Active);
                Assert.That(c.Recovery.Completed, Is.EqualTo(i + 1));
                Assert.That(c.Match.CurrentShot.ShotId, Is.EqualTo(shot));
                Assert.That(c.Match.Score.FarPoints, Is.EqualTo(i));
            }
            yield return Until(() => c.ReadyToServe);
            Assert.That(c.Match.RallyId, Is.EqualTo(2));
            Assert.That(c.Match.ServeSide, Is.EqualTo(ServeSide.Left));
            Assert.That(c.Serve(), Is.True);
            yield return Until(() => c.Match.Phase == MatchPhase.Rally);
            Assert.That(c.Recovery.Active, Is.False);
            Assert.That(c.Officials.Call, Is.EqualTo(UmpireCall.Play));
        }

        [UnityTest]
        public IEnumerator GuestPresentsRecoveryAndUmpireWithoutAdvancingItsOwnRulesOrPhysics()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.EnterOnline(false);
            var state = GamePayload.Capture(c, "guest-officials", 0);
            state.phase = "WaitServe";
            state.officials = new OfficialsState { callId = 7, call = (int)UmpireCall.Fault, text = "Fault - second serve",
                recovery = (int)RecoveryPhase.Returning, runner = new[] { 3f, 0f, -.4f }, completed = 0 };
            state.ball.pos = new[] { 3f, .65f, -.4f };
            var buffer = new SnapshotBuffer();
            Assert.That(buffer.Push(new GameMessage { type = "snapshot", tick = 1, payload = state }, 1), Is.True);
            buffer.Present(1.1, c);
            yield return new WaitForSeconds(.2f);
            Assert.That(c.Officials.CallText, Is.EqualTo(state.officials.text));
            Assert.That(c.Officials.RunnerPosition, Is.EqualTo(new Vector3(3, 0, -.4f)));
            Assert.That(c.Officials.RecoveryPhase, Is.EqualTo(RecoveryPhase.Returning));
            Assert.That(c.Ball.transform.position, Is.EqualTo(new Vector3(3, .65f, -.4f)));
            Assert.That(c.Ball.PhysicsTicks, Is.Zero);
            Assert.That(c.Match.Score.TotalPoints, Is.Zero);
            Assert.That(c.Recovery.Active, Is.False, "Guest must not run authoritative recovery timers.");
        }

        [UnityTest]
        public IEnumerator LetIsCollectedWithoutPointAndRestartCancelsRecovery()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            Assert.That(c.Officials, Is.Not.Null);
            c.Serve(ServeVariant.Let);
            yield return Until(() => c.Match.NetTouched);
            Assert.That(c.Recovery.Active, Is.False, "Live net contact alone must not send the ball boy in.");
            yield return Until(() => c.Recovery.Active);
            Assert.That(c.Match.Call, Is.EqualTo(UmpireCall.Let));
            Assert.That(c.Match.Score.TotalPoints, Is.Zero);
            c.IsPaused = true;
            var runner = c.Recovery.Position;
            yield return new WaitForSeconds(.15f);
            Assert.That(c.Recovery.Position, Is.EqualTo(runner));
            c.IsPaused = false;
            c.ResetMatch();
            Assert.That(c.Recovery.Active, Is.False);
            Assert.That(c.Recovery.Completed, Is.Zero);
            Assert.That(c.ReadyToServe, Is.True);
        }
    }
}
