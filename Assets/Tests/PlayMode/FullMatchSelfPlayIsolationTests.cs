using System.Collections;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class FullMatchSelfPlayIsolationTests
    {
        [UnityTest]
        public IEnumerator ActiveBallPauseAndOnlineObservationLeaveBothPoliciesInactive()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");yield return null;
            var c=Object.FindFirstObjectByType<TennisMatchController>();
            c.Configure(GameMode.Practice,Difficulty.Hard,771);c.AutoServe=c.DemoReturns=false;
            var policies=new[]{new AiPolicy(c.Tuning.Difficulty(Difficulty.Hard),c.Tuning.AiTuning(),771),new AiPolicy(c.Tuning.Difficulty(Difficulty.Hard),c.Tuning.AiTuning(),8690)};
            c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Serve,1,Time.realtimeSinceStartupAsDouble),9300,CourtSide.Near);
            Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.InFlight));
            var before=c.Ball.PhysicsTicks;
            for(int frame=0;frame<3;frame++)yield return new WaitForFixedUpdate();
            Assert.That(c.Ball.PhysicsTicks,Is.GreaterThan(before));
            c.IsPaused=true;var pausedTicks=c.Ball.PhysicsTicks;var position=c.Ball.Simulation.Position;var points=c.Match.Progress.TotalPoints;
            for(int frame=0;frame<3;frame++)
            {
                for(int side=0;side<2;side++)
                {
                    var d=policies[side].Decide(FullMatchObservation.AiState(c,(CourtSide)side,frame+1,Time.deltaTime));
                    Assert.That(d.ShouldSwing,Is.False);Assert.That(policies[side].Samples,Is.Zero);Assert.That(policies[side].ReactionElapsed,Is.Zero);
                    var result=c.SubmitInput(new RacketInput((CourtSide)side,StrokeKind.Forehand,(uint)(9300+side),(uint)(frame+2),.5f,0,0,Time.realtimeSinceStartupAsDouble,c.Match.RallyId,c.Match.CurrentShot.ShotId));
                    Assert.That(result,Is.EqualTo(SwingResult.WrongPhase));
                }
                yield return new WaitForFixedUpdate();
            }
            Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(pausedTicks));Assert.That(c.Ball.Simulation.Position,Is.EqualTo(position));Assert.That(c.Match.Progress.TotalPoints,Is.EqualTo(points));
            c.IsPaused=false;yield return new WaitForFixedUpdate();
            Assert.That(c.Ball.PhysicsTicks,Is.GreaterThan(pausedTicks));
            c.Configure(GameMode.Online);
            for(int side=0;side<2;side++)
            {
                var state=FullMatchObservation.AiState(c,(CourtSide)side,100,.1f);Assert.That(state.IsOnline,Is.True);
                Assert.That(policies[side].Decide(state).ShouldSwing,Is.False);Assert.That(policies[side].Samples,Is.Zero);
            }
            Assert.That(c.Opponent,Is.Null);
        }
    }
}
