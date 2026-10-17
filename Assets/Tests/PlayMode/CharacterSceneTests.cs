using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class CharacterSceneTests
    {
        private static IEnumerator Until(Func<bool> condition, float seconds = 25)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(condition(), Is.True, "Character scene transition timed out.");
        }
        [UnityTest]
        public IEnumerator RealRacketReturnsChargeSkillAndCinematicStopsBallWithoutAwardingPoints()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.DemoReturnLimit = 10;
            yield return Until(() => c.Characters.Player(CourtSide.Near).Meter.Stock > 0 || c.Characters.Player(CourtSide.Far).Meter.Stock > 0);
            var side = c.Characters.Player(CourtSide.Near).Meter.Stock > 0 ? CourtSide.Near : CourtSide.Far;
            var ability = c.Characters.Player(side); int previous = ability.Meter.Stock;
            c.InputAdapter.Press(PlayerAction.Skill, side == CourtSide.Far);
            Assert.That(ability.Meter.Stock, Is.EqualTo(previous - 1)); Assert.That(c.Characters.Cinematic, Is.True);
            var position = c.Ball.transform.position; long points = c.Match.Score.TotalPoints; ulong ticks = c.Ball.PhysicsTicks;
            yield return new WaitForSeconds(.25f);
            Assert.That(c.Ball.transform.position, Is.EqualTo(position)); Assert.That(c.Ball.PhysicsTicks, Is.EqualTo(ticks));
            Assert.That(c.Match.Score.TotalPoints, Is.EqualTo(points));
            yield return Until(() => !c.Characters.Cinematic, 2);
            Assert.That(ability.Buff, Is.True);
            c.InputAdapter.Press(PlayerAction.Skill, side == CourtSide.Far);
            Assert.That(ability.Meter.Stock, Is.EqualTo(previous - 1));
            yield return Until(() => c.Ball.PhysicsTicks > ticks, 2);
        }
        [UnityTest]
        public IEnumerator NamedTechniquesPassTheRealShotGateAndKeepScoringSeparate()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>(); c.AutoServe = c.DemoReturns = false;
            foreach (var stroke in new[] { StrokeKind.Smash, StrokeKind.Volley, StrokeKind.Slice, StrokeKind.Lob })
            {
                c.ResetMatch(); c.Match.TryServe(CourtSide.Near);
                c.Match.HandleBallEvent(new BallEvent(c.Match.CurrentShot, 1, BallEventKind.Bounce, Vector3.forward * 4, 1, true));
                c.Match.TryReturn(CourtSide.Far);
                Vector3 at = c.NearAvatar.transform.position; at.y = stroke == StrokeKind.Smash ? 2.2f : 1.2f;
                c.Ball.Launch(at, Vector3.back * 8, c.Match.CurrentShot);
                Assert.That(c.SubmitInput(c.CreateVirtualInput(CourtSide.Near, stroke)), Is.EqualTo(SwingResult.Hit));
                Assert.That(c.Match.LastHitter, Is.EqualTo(CourtSide.Near)); Assert.That(c.Match.Score.TotalPoints, Is.Zero);
                if (stroke == StrokeKind.Smash) Assert.That(c.Ball.Simulation.Velocity.y, Is.LessThan(0));
                if (stroke == StrokeKind.Lob) Assert.That(c.Ball.Simulation.Velocity.y, Is.GreaterThan(7));
            }
        }

        [UnityTest]
        public IEnumerator EveryRosterSelectionKeepsTheGripAttachedOnItsFirstPresentedFrame()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.Configure(GameMode.Practice);c.AutoServe=c.DemoReturns=false;
            var observer=c.gameObject.AddComponent<RosterGripFrameObserver>();
            for(int index=0;index<15;index++)
            {
                int selectedAt=Time.frameCount;
                c.InputAdapter.Press(PlayerAction.SelectCharacter,false,index);
                c.InputAdapter.Press(PlayerAction.SelectCharacter,true,14-index);
                yield return null;
                Assert.That(observer.Frame,Is.GreaterThanOrEqualTo(selectedAt));
                Assert.That(observer.Near,Is.EqualTo(index));Assert.That(observer.Far,Is.EqualTo(14-index));
                Assert.That(observer.Error,Is.LessThan(.035f),CharacterCatalog.Ids[index]+" first-frame grip");
                Assert.That(observer.MeshesMatch,Is.True,"The displayed skins must match the selected identities immediately");
            }
            UnityEngine.Object.Destroy(observer);
        }

        [UnityTest]
        public IEnumerator CharacterSelectionLocksDuringPlayAndResetClearsStocksPreservingSelection()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.InputAdapter.Press(PlayerAction.SelectCharacter, false, 14);
            Assert.That(c.Characters.Player(CourtSide.Near).Index, Is.EqualTo(14));
            Assert.That(c.Characters.SelectCourt(13), Is.True);
            c.Serve(); Assert.That(c.Characters.Select(CourtSide.Near, 2), Is.False);
            Assert.That(c.Characters.SelectCourt(2), Is.False);
            c.ResetMatch(); Assert.That(c.Characters.Player(CourtSide.Near).Index, Is.EqualTo(14));
            Assert.That(c.Characters.Player(CourtSide.Near).Meter.Stock, Is.Zero);
            Assert.That(c.Characters.CourtIndex, Is.EqualTo(13));
        }
    }
    // Inspect after production LateUpdate, with no pose or state setters.
    [DefaultExecutionOrder(10000)]
    public sealed class RosterGripFrameObserver:MonoBehaviour
    {
        public int Frame,Near,Far;public float Error;public bool MeshesMatch;
        private void LateUpdate()
        {
            var c=GetComponent<TennisMatchController>();Frame=Time.frameCount;Error=0;MeshesMatch=true;
            Near=c.Characters.Player(CourtSide.Near).Index;Far=c.Characters.Player(CourtSide.Far).Index;
            foreach(var a in new[]{c.NearAvatar,c.FarAvatar})
            {
                var rig=a.GetComponentInChildren<AthleteMotion>();
                Error=Mathf.Max(Error,Vector3.Distance(rig.GripPosition,a.RacketPivot.TransformPoint(new Vector3(0,.12f,0))));
                int index=a==c.NearAvatar?Near:Far;
                MeshesMatch&=rig.GetComponent<AthleteSkin>().Surface.sharedMesh.name.StartsWith(CharacterCatalog.Ids[index]+"-");
            }
        }
    }

}
