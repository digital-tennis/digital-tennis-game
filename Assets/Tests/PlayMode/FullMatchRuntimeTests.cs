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
    public sealed class FullMatchRuntimeTests
    {
        [Serializable] public sealed class Report
        {
            public bool passed, pauseHeld, onlineIsolated, reset;
            public string winner, backend, scope = "Frozen resumed actor in normal SinglePlayerCourt; scripted Near ordinary adapter; authoritative default full match, no score/ball/stock writes";
            public int nearHits, farHits, attempts, rejected, decisions, farServes, endChanges, nearSets, farSets;
            public long points;
            public List<FullMatchBaselineTests.Point> trajectory = new List<FullMatchBaselineTests.Point>();
        }
        sealed class Source : BufferedPlayerInputSource
        {
            readonly TennisMatchController c; uint sequence;
            public Source(TennisMatchController controller) : base("Full-match runtime validation only") { c=controller; }
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                if(c.IsPaused||c.Characters.Cinematic)return;
                if(c.ReadyToServe&&c.Match.Server==CourtSide.Near)
                    Enqueue(new PlayerInputSample(PlayerAction.Serve,++sequence,now));
                else if(c.CanReturn(CourtSide.Near)&&c.Ball.Simulation.BounceCount==1&&c.Ball.Simulation.Position.y>=.95f)
                {
                    float facing=c.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                    float aim=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*facing,17)*Mathf.Rad2Deg/25,-1,1);
                    Enqueue(new PlayerInputSample(c.NearHits%2==0?PlayerAction.Forehand:PlayerAction.Backhand,++sequence,now,.35f,aim));
                }
            }
        }
        [UnityTest, Timeout(900000)]
        public IEnumerator ResumedActorCompletesDefaultMatchAndResetsThroughOrdinaryAdapter()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            c.Configure(GameMode.SinglePlayer,Difficulty.Medium,91027);
            c.InputAdapter.UseSource(new Source(c));
            var report=new Report {backend=c.Opponent.BackendName};
            Assert.That(report.backend,Is.EqualTo("학습 AI"));
            Assert.That(c.Match.Progress.Rules.Values,Is.EqualTo(new[]{3,6,6,7}));
            Assert.That(c.AutoServe||c.DemoReturns,Is.False);
            float capture=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=.1f;
                c.IsPaused=true;ulong ticks=c.Ball.PhysicsTicks;int samples=c.Opponent.Policy.Samples;
                for(int i=0;i<5;i++)yield return null;
                report.pauseHeld=ticks==c.Ball.PhysicsTicks&&samples==c.Opponent.Policy.Samples;
                Assert.That(report.pauseHeld,Is.True);c.IsPaused=false;
                long last=0;float deadline=Time.realtimeSinceStartup+750;
                while(!c.Match.Winner.HasValue&&Time.realtimeSinceStartup<deadline)
                {
                    var p=c.Match.Progress;
                    if(p.TotalPoints!=last)
                    {
                        Assert.That(p.TotalPoints,Is.EqualTo(last+1));last=p.TotalPoints;
                        report.trajectory.Add(new FullMatchBaselineTests.Point{rally=c.Match.RallyId,nearPoints=p.NearTotalPoints,farPoints=p.FarTotalPoints,nearGames=p.NearGames,farGames=p.FarGames,nearSets=p.NearSets,farSets=p.FarSets,endChanges=p.EndChanges,server=p.Server.ToString(),nearEnd=p.EndOf(CourtSide.Near).ToString(),farEnd=p.EndOf(CourtSide.Far).ToString()});
                        Assert.That(p.EndOf(CourtSide.Near),Is.Not.EqualTo(p.EndOf(CourtSide.Far)));
                    }
                    yield return null;
                }
                Assert.That(c.Match.Winner.HasValue,Is.True);
                var final=c.Match.Progress;
                if(final.TotalPoints!=last)report.trajectory.Add(new FullMatchBaselineTests.Point{rally=c.Match.RallyId,nearPoints=final.NearTotalPoints,farPoints=final.FarTotalPoints,nearGames=final.NearGames,farGames=final.FarGames,nearSets=final.NearSets,farSets=final.FarSets,endChanges=final.EndChanges,server=final.Server.ToString(),nearEnd=final.EndOf(CourtSide.Near).ToString(),farEnd=final.EndOf(CourtSide.Far).ToString()});
                report.winner=c.Match.Winner.ToString();report.nearSets=final.NearSets;report.farSets=final.FarSets;
                report.points=final.TotalPoints;report.endChanges=final.EndChanges;
                report.nearHits=c.NearHits;report.farHits=c.FarHits;report.attempts=c.Opponent.Attempts;report.rejected=c.Opponent.RejectedHits;report.decisions=c.Opponent.LearnedDecisions;report.farServes=c.Opponent.Serves;
                Assert.That(report.nearHits,Is.GreaterThan(0));Assert.That(report.farHits,Is.GreaterThan(0));Assert.That(report.decisions,Is.GreaterThan(0));Assert.That(report.farServes,Is.GreaterThan(0));Assert.That(report.endChanges,Is.GreaterThan(0));Assert.That(report.attempts,Is.EqualTo(report.farHits+report.rejected));Assert.That(report.trajectory.Count,Is.EqualTo(report.points));
                c.InputAdapter.Press(PlayerAction.Restart);yield return null;
                report.reset=c.Match.Progress.TotalPoints==0&&c.Match.Winner==null&&c.Match.Progress.NearSets+c.Match.Progress.FarSets==0&&c.Opponent.LearnedDecisions==0;
                Assert.That(report.reset,Is.True);
                var old=c.Opponent;c.Configure(GameMode.Online);old.Tick(c,.1f,true);yield return null;
                report.onlineIsolated=c.Opponent==null&&old.LearnedDecisions==0;
                Assert.That(report.onlineIsolated,Is.True);report.passed=true;
            }
            finally
            {
                Time.captureDeltaTime=capture;
                Directory.CreateDirectory(".work/ai-runtime");File.WriteAllText(".work/ai-runtime/scene.json",JsonUtility.ToJson(report,true));
            }
        }
    }
}
