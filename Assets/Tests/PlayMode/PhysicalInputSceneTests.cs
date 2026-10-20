using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace DigitalTennis.Tests
{
    public sealed class PhysicalInputSceneTests
    {
        [UnityTest] public IEnumerator PhysicalPatternsUseRealServeHeightBounceAndDuplicateGates()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=Object.FindFirstObjectByType<TennisMatchController>();c.Configure(GameMode.Practice);c.AutoServe=false;c.DemoReturns=false;c.IsPaused=false;c.InputAdapter.UseSource(new BufferedPlayerInputSource("physical policy fixture"));
            PlayerInputSample Physical(PlayerAction action,uint id,float aim=0)=>new PlayerInputSample(action,id,Time.realtimeSinceStartupAsDouble,.7f,aim);
            Assert.That(c.ApplyPlayerInput(Physical(PlayerAction.MotionOverheadDown,1),77,CourtSide.Far),Is.EqualTo(SwingResult.WrongPhase));Assert.That(c.Match.CurrentShot.ShotId,Is.Zero);
            c.ApplyPlayerInput(Physical(PlayerAction.MotionOverheadDown,2),77,CourtSide.Near);Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.InFlight));
            Assert.That(c.Match.HandleBallEvent(new BallEvent(c.Match.CurrentShot,1,BallEventKind.Bounce,new Vector3(0,0,4),1,true)),Is.True);
            Assert.That(c.Match.TryReturn(CourtSide.Far),Is.True);
            var at=c.NearAvatar.transform.position;float high=c.Tuning["hit.smash_height"]+.1f;
            c.Ball.Launch(new Vector3(at.x,high,at.z),Vector3.zero,c.Match.CurrentShot);
            var smash=Physical(PlayerAction.MotionOverheadDown,3);
            Assert.That(c.ApplyPlayerInput(smash,77,CourtSide.Near),Is.EqualTo(SwingResult.Hit));Assert.That(c.LastHit.Value.Stroke,Is.EqualTo(StrokeKind.Smash));
            Assert.That(c.ApplyPlayerInput(smash,77,CourtSide.Near),Is.EqualTo(SwingResult.Duplicate));
            Assert.That(c.Match.TryReturn(CourtSide.Far),Is.True);c.Ball.Launch(new Vector3(at.x,1,at.z),Vector3.zero,c.Match.CurrentShot);
            var low=Physical(PlayerAction.MotionOverheadDown,4);int hits=c.NearHits;
            Assert.That(c.ApplyPlayerInput(low,77,CourtSide.Near),Is.EqualTo(SwingResult.Height));Assert.That(c.NearHits,Is.EqualTo(hits));
            c.Ball.Launch(new Vector3(at.x,high,at.z),Vector3.zero,c.Match.CurrentShot);
            Assert.That(c.ApplyPlayerInput(low,77,CourtSide.Near),Is.EqualTo(SwingResult.Duplicate),"a missed physical event cannot later hit");
            c.Ball.Launch(new Vector3(at.x,1,at.z),Vector3.zero,c.Match.CurrentShot);
            Assert.That(c.ApplyPlayerInput(Physical(PlayerAction.MotionShortRotation,5),77,CourtSide.Near),Is.EqualTo(SwingResult.Hit));Assert.That(c.LastHit.Value.Stroke,Is.EqualTo(StrokeKind.Volley));
            Assert.That(c.Match.TryReturn(CourtSide.Far),Is.True);
            // Arrange a legal in-court receiver bounce. The default avatar can stand
            // beyond the baseline; bouncing at its feet would correctly end the point.
            var bounceAt=new Vector3(at.x,1,Mathf.Max(at.z,-10.5f));
            c.NearAvatar.transform.position=new Vector3(bounceAt.x,at.y,bounceAt.z);
            c.Ball.Launch(bounceAt,Vector3.down,c.Match.CurrentShot);
            // Wait until the real rebound rises into the legal hit-height window.
            for(int i=0;i<100&&(c.Ball.Simulation.BounceCount==0||c.Ball.Simulation.Position.y<c.Tuning["hit.min_height"]+.1f);i++)
                foreach(var e in c.Ball.Simulation.Step())c.Match.HandleBallEvent(e);
            Assert.That(c.Ball.Simulation.BounceCount,Is.EqualTo(1));
            Assert.That(c.ApplyPlayerInput(Physical(PlayerAction.MotionShortRotation,6,-.2f),77,CourtSide.Near),Is.EqualTo(SwingResult.Hit));Assert.That(c.LastHit.Value.Stroke,Is.EqualTo(StrokeKind.Backhand));
        }
    }
}
