using System.Collections;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class SinglePlayerSceneTests
    {
        private static IEnumerator PlayOneGame(Difficulty difficulty)
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");
            yield return null;
            var controller = Object.FindFirstObjectByType<TennisMatchController>();
            controller.Configure(GameMode.SinglePlayer, difficulty, 42);
            Assert.That(controller.AutoServe, Is.False);
            Assert.That(controller.DemoReturns, Is.False);
            Assert.That(controller.VirtualSwing(CourtSide.Far, StrokeKind.Forehand), Is.EqualTo(SwingResult.WrongPhase));
            float previousScale = Time.timeScale;
            float maxFarMovement = 0;
            int serves = 0;
            Time.timeScale = 2f;
            try
            {
                float deadline = Time.realtimeSinceStartup + 100f;
                while (controller.Match.Phase != MatchPhase.MatchEnd && Time.realtimeSinceStartup < deadline)
                {
                    if (controller.Match.Phase == MatchPhase.WaitServe)
                    { Assert.That(controller.Serve(), Is.True); serves++; }
                    if (controller.CanReturn(CourtSide.Near) && controller.Ball.Simulation.BounceCount == 1 &&
                        controller.Ball.Simulation.Position.y >= .95f)
                    {
                        // Reproducible human-side virtual input fixture; no automatic player demo is enabled.
                        float horizontal = Mathf.Atan2(-controller.Ball.Simulation.Position.x,17f)*Mathf.Rad2Deg/25f;
                        var input = controller.CreateVirtualInput(CourtSide.Near,
                            controller.NearHits % 2 == 0 ? StrokeKind.Forehand : StrokeKind.Backhand,
                            .35f,Mathf.Clamp(horizontal,-1f,1f));
                        Assert.That(controller.SubmitInput(input), Is.EqualTo(SwingResult.Hit));
                    }
                    maxFarMovement = Mathf.Max(maxFarMovement,controller.FarAvatar.DistanceTravelled);
                    yield return null;
                }
                Assert.That(controller.Match.Phase, Is.EqualTo(MatchPhase.MatchEnd), "The one-player game must complete.");
                Assert.That(controller.Match.Score.Winner.HasValue, Is.True);
                Assert.That(controller.Match.Score.TotalPoints, Is.GreaterThanOrEqualTo(4));
                Assert.That(serves, Is.GreaterThanOrEqualTo(4), "Points must return to a playable next serve.");
                Assert.That(controller.Opponent.AcceptedHits, Is.GreaterThan(0), "AI must physically return a ball.");
                Assert.That(controller.NearHits, Is.GreaterThan(0), "Human virtual input must return AI shots.");
                Assert.That(controller.Opponent.Attempts, Is.EqualTo(controller.Opponent.AcceptedHits + controller.Opponent.RejectedHits));
                Assert.That(controller.FarHits, Is.EqualTo(controller.Opponent.AcceptedHits), "Character reach or fatigue may reject an AI attempt; only accepted hits affect the rally.");
                Assert.That(maxFarMovement, Is.GreaterThan(1f));
                int attempts = controller.Opponent.Attempts;
                long points = controller.Match.Score.TotalPoints;
                yield return new WaitForSeconds(.5f);
                Assert.That(controller.Opponent.Attempts, Is.EqualTo(attempts));
                Assert.That(controller.Match.Score.TotalPoints, Is.EqualTo(points));
                Assert.That(controller.Serve(), Is.False);
                Debug.Log($"SINGLE_PLAYER_TEST {difficulty} winner={controller.Match.Score.Winner} points={points} serves={serves} human={controller.NearHits} ai={attempts}");
            }
            finally { Time.timeScale = previousScale; }
        }

        [UnityTest] public IEnumerator EasyOpponentCompletesOneGame() => PlayOneGame(Difficulty.Easy);
        [UnityTest] public IEnumerator MediumOpponentCompletesOneGame() => PlayOneGame(Difficulty.Medium);
        [UnityTest] public IEnumerator HardOpponentCompletesOneGame() => PlayOneGame(Difficulty.Hard);

        [UnityTest]
        public IEnumerator PracticeAndOnlineModesCannotReceiveAiInputs()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");
            yield return null;
            var controller = Object.FindFirstObjectByType<TennisMatchController>();
            controller.Configure(GameMode.SinglePlayer,Difficulty.Hard,1);
            controller.DemoReturns = true; // Even a stale practice toggle must not drive the human player.
            controller.Serve();
            float deadline = Time.realtimeSinceStartup + 5f;
            while(controller.Opponent.AcceptedHits==0 && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(controller.Opponent.AcceptedHits, Is.EqualTo(1));
            Assert.That(controller.NearHits, Is.EqualTo(0));
            var onlineProbe = new TennisAiOpponent(Difficulty.Hard,1,999);
            uint shot = controller.Match.CurrentShot.ShotId;
            for(int i=0;i<30;i++)onlineProbe.Tick(controller,.02f,true);
            Assert.That(onlineProbe.Attempts, Is.EqualTo(0));
            Assert.That(onlineProbe.Policy.Samples, Is.EqualTo(0));
            Assert.That(controller.Match.CurrentShot.ShotId, Is.EqualTo(shot));
            controller.Configure(GameMode.Online);
            Assert.That(controller.Opponent, Is.Null);
            controller.Configure(GameMode.Practice);
            Assert.That(controller.Opponent, Is.Null);
            Assert.That(controller.AutoServe && controller.DemoReturns, Is.True);
        }
    }
}
