using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    // Opt-in pre-training measurement. Never writes score, ball, stock or balance.
    public sealed class FullMatchBaselineTests
    {
        [Serializable] public sealed class Point
        {
            public uint rally; public long nearPoints, farPoints;
            public int nearGames, farGames, nearSets, farSets, endChanges;
            public string server, nearEnd, farEnd;
        }
        [Serializable] public sealed class Case
        {
            public string near, far, difficulty; public uint seed;
            public int nearServes, farServes, nearHits, farHits, attempts, rejected, decisions;
            public int endChanges, serviceChanges, nearGames, farGames;
            public long points; public ulong physicsTicks;
            public int[] actions, swingResults = new int[10], ballEvents = new int[4];
            public List<Point> trajectory = new List<Point>();
        }
        [Serializable] public sealed class Report
        {
            public int schema = 1; public bool passed;
            public string modelSha256;
            public string unityVersion, scope = "Pre-training bundled placement PPO; actual SinglePlayerCourt; cyclic offset 1; 15 pairs x 3 difficulties x seeds 17/4016; two service games per case. Scripted Near fixture, not human win rates or full matches.";
            public List<Case> cases = new List<Case>();
        }

        [UnityTest, Timeout(7200000)]
        public IEnumerator ExpandedPreTrainingBaseline()
        {
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-dt-ai-baseline-out");
            if (flag < 0 || flag + 1 >= args.Length)
            { Assert.Ignore("Opt-in: bash tools/unity.sh test-ai-baseline"); yield break; }
            string path = Path.GetFullPath(args[flag + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string modelHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                modelHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Resources.Load<TextAsset>("DigitalTennisAI/placement-ppo").text))).Replace("-", "").ToLowerInvariant();
            var report = new Report { unityVersion = Application.unityVersion, modelSha256 = modelHash };
            void Save() => File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Save();
            float previousScale = Time.timeScale;
            try
            {
                foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
                foreach (uint seed in new uint[] { 17, 4016 })
                for (int index = 0; index < CharacterCatalog.Ids.Length; index++)
                {
                    Time.timeScale = 1;
                    yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");
                    yield return null;
                    var c = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
                    c.Configure(GameMode.SinglePlayer, difficulty, seed);
                    Assert.That(c.Characters.Select(CourtSide.Near, index), Is.True);
                    Assert.That(c.Characters.Select(CourtSide.Far, (index + 1) % CharacterCatalog.Ids.Length), Is.True);
                    Assert.That(c.Ball.IsAuthoritative, Is.True);
                    Assert.That(c.Opponent.BackendName, Is.EqualTo("학습 AI"), "No local override/fallback in baseline");
                    Assert.That(c.Tuning.Hash, Is.EqualTo(GameplayBalance.Defaults.Hash));
                    Assert.That(c.Match.Progress.Rules.Values, Is.EqualTo(new[] { 3, 6, 6, 7 }));
                    Assert.That(c.AutoServe || c.DemoReturns, Is.False);
                    var item = new Case { near = CharacterCatalog.Ids[index], far = CharacterCatalog.Ids[(index + 1) % CharacterCatalog.Ids.Length], difficulty = difficulty.ToString(), seed = seed };
                    c.SwingEvaluated += (input, result) => item.swingResults[(int)result]++;
                    c.Ball.EventRaised += e => item.ballEvents[(int)e.Kind]++;
                    long observedPoints = 0;
                    uint servedRally = 0;
                    var lastServer = c.Match.Server;
                    Time.timeScale = 8;
                    float deadline = Time.realtimeSinceStartup + 120;
                    while (c.Match.Progress.NearGames + c.Match.Progress.FarGames < 2 && Time.realtimeSinceStartup < deadline)
                    {
                        var score = c.Match.Progress;
                        if (score.TotalPoints != observedPoints)
                        {
                            Assert.That(score.TotalPoints, Is.EqualTo(observedPoints + 1));
                            observedPoints = score.TotalPoints;
                            item.trajectory.Add(new Point { rally = c.Match.RallyId, nearPoints = score.NearTotalPoints, farPoints = score.FarTotalPoints,
                                nearGames = score.NearGames, farGames = score.FarGames, nearSets = score.NearSets, farSets = score.FarSets,
                                endChanges = score.EndChanges, server = score.Server.ToString(), nearEnd = score.EndOf(CourtSide.Near).ToString(), farEnd = score.EndOf(CourtSide.Far).ToString() });
                        }
                        if (c.Match.Server != lastServer) { item.serviceChanges++; lastServer = c.Match.Server; }
                        if (c.ReadyToServe && c.Match.Server == CourtSide.Near) Assert.That(c.Serve(), Is.True);
                        if ((c.Match.Phase == MatchPhase.InFlight || c.Match.Phase == MatchPhase.Rally) && servedRally != c.Match.RallyId)
                        {
                            servedRally = c.Match.RallyId;
                            if (c.Match.Server == CourtSide.Near) item.nearServes++; else item.farServes++;
                        }
                        if (c.CanReturn(CourtSide.Near) && c.Ball.Simulation.BounceCount == 1 && c.Ball.Simulation.Position.y >= .95f)
                        {
                            float facing = c.Match.EndOf(CourtSide.Near) == CourtSide.Near ? 1 : -1;
                            float horizontal = Mathf.Atan2(-c.Ball.Simulation.Position.x * facing, 17f) * Mathf.Rad2Deg / 25f;
                            c.SubmitInput(c.CreateVirtualInput(CourtSide.Near, c.NearHits % 2 == 0 ? StrokeKind.Forehand : StrokeKind.Backhand, .35f, Mathf.Clamp(horizontal, -1, 1)));
                        }
                        yield return null;
                    }
                    // Capture the point that completed the second game before exiting.
                    var final = c.Match.Progress;
                    if (final.TotalPoints != observedPoints)
                        item.trajectory.Add(new Point { rally = c.Match.RallyId, nearPoints = final.NearTotalPoints, farPoints = final.FarTotalPoints,
                            nearGames = final.NearGames, farGames = final.FarGames, nearSets = final.NearSets, farSets = final.FarSets,
                            endChanges = final.EndChanges, server = final.Server.ToString(), nearEnd = final.EndOf(CourtSide.Near).ToString(), farEnd = final.EndOf(CourtSide.Far).ToString() });
                    item.points = final.TotalPoints; item.nearGames = final.NearGames; item.farGames = final.FarGames;
                    item.endChanges = final.EndChanges; item.nearHits = c.NearHits; item.farHits = c.FarHits;
                    item.attempts = c.Opponent.Attempts; item.rejected = c.Opponent.RejectedHits;
                    item.decisions = c.Opponent.LearnedDecisions; item.actions = c.Opponent.LearnedActionCounts; item.physicsTicks = c.Ball.PhysicsTicks;
                    report.cases.Add(item); Save();
                    Assert.That(item.nearGames + item.farGames, Is.EqualTo(2), $"Timeout {item.near}/{item.far}/{difficulty}/{seed}");
                    Assert.That(item.points, Is.EqualTo(item.trajectory.Count));
                    Assert.That(item.nearServes, Is.GreaterThanOrEqualTo(4));
                    Assert.That(item.farServes, Is.GreaterThanOrEqualTo(4));
                    Assert.That(item.endChanges, Is.EqualTo(1));
                    Assert.That(item.serviceChanges, Is.GreaterThanOrEqualTo(1));
                    Assert.That(item.attempts, Is.EqualTo(item.farHits + item.rejected));
                    Assert.That(item.physicsTicks, Is.GreaterThan(0));
                    Debug.Log($"AI_BASELINE_CASE {report.cases.Count}/90 {item.near}/{item.far} {difficulty} seed={seed} points={item.points}");
                }
                Assert.That(report.cases.Count, Is.EqualTo(90));
                report.passed = true;
            }
            finally { Time.timeScale = previousScale; Save(); }
        }
    }
}
