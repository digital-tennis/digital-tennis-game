using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class SinglePlayerSceneTests
    {
        private static IEnumerator PlayServiceChanges(Difficulty difficulty,uint seed=42,List<EvaluationCase> cases=null,float timeScale=2f)
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");
            yield return null;
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            controller.Configure(GameMode.SinglePlayer, difficulty, seed);
            if(cases!=null){Assert.That(controller.Opponent.BackendName,Is.EqualTo("학습 AI"),"Evaluation requires bundled model, without a user override");Assert.That(controller.Tuning.Hash,Is.EqualTo(GameplayBalance.Defaults.Hash));}
            Assert.That(controller.AutoServe, Is.False);
            Assert.That(controller.DemoReturns, Is.False);
            StringAssert.Contains("학습 AI",controller.Opponent.BackendName);
            Assert.That(controller.VirtualSwing(CourtSide.Far, StrokeKind.Forehand), Is.EqualTo(SwingResult.WrongPhase));
            float previousScale = Time.timeScale;
            float maxFarMovement = 0;
            int serves = 0;
            Time.timeScale = timeScale;
            try
            {
                float deadline = Time.realtimeSinceStartup + 100f;
                while (controller.Match.Progress.NearGames+controller.Match.Progress.FarGames<2 && Time.realtimeSinceStartup < deadline)
                {
                    if (controller.ReadyToServe&&controller.Match.Server==CourtSide.Near)
                    { Assert.That(controller.Serve(), Is.True); serves++; }
                    if (controller.CanReturn(CourtSide.Near) && controller.Ball.Simulation.BounceCount == 1 &&
                        controller.Ball.Simulation.Position.y >= .95f)
                    {
                        // Reproducible human-side virtual input fixture; no automatic player demo is enabled.
                        float facing=controller.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                        float horizontal = Mathf.Atan2(-controller.Ball.Simulation.Position.x*facing,17f)*Mathf.Rad2Deg/25f;
                        var input = controller.CreateVirtualInput(CourtSide.Near,
                            controller.NearHits % 2 == 0 ? StrokeKind.Forehand : StrokeKind.Backhand,
                            .35f,Mathf.Clamp(horizontal,-1f,1f));
                        Assert.That(controller.SubmitInput(input), Is.EqualTo(SwingResult.Hit));
                    }
                    maxFarMovement = Mathf.Max(maxFarMovement,controller.FarAvatar.DistanceTravelled);
                    yield return null;
                }
                Assert.That(controller.Match.Progress.NearGames+controller.Match.Progress.FarGames,Is.EqualTo(2));
                Assert.That(controller.Match.Winner,Is.Null);Assert.That(controller.Match.Progress.TotalPoints,Is.GreaterThanOrEqualTo(8));
                Assert.That(controller.Opponent.Serves,Is.GreaterThanOrEqualTo(4),"The AI must start its own service game after the ends change.");
                Assert.That(serves, Is.GreaterThanOrEqualTo(4), "Points must return to a playable next serve.");
                Assert.That(controller.Opponent.AcceptedHits, Is.GreaterThan(0), "AI must physically return a ball.");
                Assert.That(controller.Opponent.LearnedDecisions, Is.GreaterThan(0), "The shipped PPO actor must actually choose shots.");
                Assert.That(controller.NearHits, Is.GreaterThan(0), "Human virtual input must return AI shots.");
                Assert.That(controller.Opponent.Attempts, Is.EqualTo(controller.Opponent.AcceptedHits + controller.Opponent.RejectedHits));
                Assert.That(controller.FarHits, Is.EqualTo(controller.Opponent.AcceptedHits), "Character reach or fatigue may reject an AI attempt; only accepted hits affect the rally.");
                Assert.That(maxFarMovement, Is.GreaterThan(1f));
                cases?.Add(new EvaluationCase {difficulty=difficulty.ToString(),seed=seed,near=CharacterCatalog.Ids[controller.Characters.Player(CourtSide.Near).Index],far=CharacterCatalog.Ids[controller.Characters.Player(CourtSide.Far).Index],
                    points=controller.Match.Progress.TotalPoints,nearPoints=controller.Match.Progress.NearTotalPoints,farPoints=controller.Match.Progress.FarTotalPoints,
                    nearHits=controller.NearHits,aiHits=controller.Opponent.AcceptedHits,aiAttempts=controller.Opponent.Attempts,aiRejected=controller.Opponent.RejectedHits,
                    humanServes=serves,aiServes=controller.Opponent.Serves,learnedDecisions=controller.Opponent.LearnedDecisions,actions=controller.Opponent.LearnedActionCounts});
                Debug.Log($"SINGLE_PLAYER_TEST {difficulty} games={controller.Match.Progress.NearGames}-{controller.Match.Progress.FarGames} serves={serves} aiServes={controller.Opponent.Serves} human={controller.NearHits} ai={controller.Opponent.Attempts}");
            }
            finally { Time.timeScale = previousScale; }
        }

        [Serializable] private sealed class EvaluationCase
        {public string difficulty,near,far;public uint seed;public long points,nearPoints,farPoints;public int nearHits,aiHits,aiAttempts,aiRejected,humanServes,aiServes,learnedDecisions;public int[] actions;}
        [Serializable] private sealed class EvaluationReport
        {public int schema=1;public bool passed;public string scope,gameplayHash,modelSha256;public EvaluationCase[] cases;}
        [UnityTest, Timeout(1200000)] public IEnumerator SeededLearnedAiEvaluationUsesRealScenesAndServiceChanges()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-ai-evaluation-out");
            if(flag<0||flag+1>=args.Length){Assert.Ignore("Opt-in nine-case scene evaluation; use tools/unity.sh test-ai-evaluation.");yield break;}
            var cases=new List<EvaluationCase>();bool passed=false;string modelHash;
            using(var sha=SHA256.Create())modelHash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Resources.Load<TextAsset>("DigitalTennisAI/placement-ppo").text))).Replace("-","").ToLowerInvariant();
            string path=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(Path.GetDirectoryName(path));
            void Save()=>File.WriteAllText(path,JsonUtility.ToJson(new EvaluationReport{passed=passed,scope="Unity SinglePlayerCourt; KOR vs JPN; 3 difficulties x 3 seeds; two service games per case; timeScale=4; scripted human fixture through ordinary hit validation. Point share is not human win rate or end-to-end training.",gameplayHash=GameplayBalance.Defaults.Hash,modelSha256=modelHash,cases=cases.ToArray()},true));
            Save();
            try
            {
                foreach(var difficulty in new[]{Difficulty.Easy,Difficulty.Medium,Difficulty.Hard})foreach(uint seed in new uint[]{17,4016,9001})
                {yield return PlayServiceChanges(difficulty,seed,cases,4f);Save();}
                Assert.That(cases.Count,Is.EqualTo(9));passed=true;
            }
            finally {Save();}
        }

        [UnityTest] public IEnumerator EasyOpponentServesAndRalliesAcrossGames() => PlayServiceChanges(Difficulty.Easy);
        [UnityTest] public IEnumerator MediumOpponentServesAndRalliesAcrossGames() => PlayServiceChanges(Difficulty.Medium);
        [UnityTest] public IEnumerator HardOpponentServesAndRalliesAcrossGames() => PlayServiceChanges(Difficulty.Hard);

        [UnityTest]
        public IEnumerator PracticeAndOnlineModesCannotReceiveAiInputs()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");
            yield return null;
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            controller.Configure(GameMode.SinglePlayer,Difficulty.Hard,1);
            controller.DemoReturns = true; // Even a stale practice toggle must not drive the human player.
            controller.Serve();
            float deadline = Time.realtimeSinceStartup + 5f;
            while(controller.Opponent.AcceptedHits==0 && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(controller.Opponent.AcceptedHits, Is.EqualTo(1));
            Assert.That(controller.NearHits, Is.EqualTo(0));
            controller.IsPaused=true;
            int decisions=controller.Opponent.LearnedDecisions,attempts=controller.Opponent.Attempts;
            for(int i=0;i<30;i++)controller.Opponent.Tick(controller,.02f);
            Assert.That(controller.Opponent.LearnedDecisions,Is.EqualTo(decisions));
            Assert.That(controller.Opponent.Attempts,Is.EqualTo(attempts));
            controller.IsPaused=false;
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
