using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    // Test-only bridge. Both players use the real gates; no score/ball/stock writes.
    public sealed class FullMatchResumeTests
    {
        [Serializable] public class Command { public string op; public int nearAction,farAction,seed,near,far; }
        [Serializable] public class State
        {
            public int observationVersion=2,actionVersion=1,rewardVersion=1;
            public float[] observation,farObservation;
            public bool done;
            public int nearGames,farGames,nearSets,farSets,nearGameWins,farGameWins,attempts,rejected,hits,decisions,farAttempts,farRejected,farHits,farDecisions,nearServes,farServes,endChanges;
            public long nearPoints,farPoints;
            public uint rally,shot;
            public ulong physicsTicks;
            public string winner,phase,server;
            public int[] rules;
            public int isolationChecks;
        }
        sealed class Selector : IShotSelector
        {
            public int Action,Decisions;
            public void Select(AiState s,out float width,out float depth)
            {
                Decisions++;
                if(Action==-1) { width=s.OpponentPosition.X>=0?1:-1;depth=1; }
                else FullMatchActor.Placement(Action,out width,out depth);
            }
        }
        [UnityTest,Timeout(14400000)]
        public IEnumerator AuthoritativeResumeBridge()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-resume-port");
            if(flag<0){Assert.Ignore("Opt-in complete-state resume bridge");yield break;}
            float capture=Time.captureDeltaTime,scale=Time.timeScale;
            var listener=new TcpListener(IPAddress.Loopback,int.Parse(args[flag+1]));listener.Start(1);
            using(var client=listener.AcceptTcpClient())
            using(var stream=client.GetStream())
            using(var reader=new StreamReader(stream,Encoding.UTF8))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false)){AutoFlush=true})
            {
                client.NoDelay=true;client.ReceiveTimeout=600000;listener.Stop();
                TennisMatchController c=null;var selectors=new Selector[2];var policies=new AiPolicy[2];
                int[] attempts=new int[2],rejected=new int[2],serves=new int[2],games=new int[2];
                uint[] ids=new uint[2];long tick=0,points=0;int isolation=0;
                State Capture()
                {
                    var p=c.Match.Progress;
                    return new State {observation=FullMatchObservation.Observe(c,CourtSide.Near),farObservation=FullMatchObservation.Observe(c,CourtSide.Far),done=c.Match.Winner.HasValue,
                        nearPoints=p.NearTotalPoints,farPoints=p.FarTotalPoints,nearGames=p.NearGames,farGames=p.FarGames,nearSets=p.NearSets,farSets=p.FarSets,
                        nearGameWins=games[0],farGameWins=games[1],attempts=attempts[0],rejected=rejected[0],hits=c.NearHits,decisions=selectors[0].Decisions,
                        farAttempts=attempts[1],farRejected=rejected[1],farHits=c.FarHits,farDecisions=selectors[1].Decisions,
                        nearServes=serves[0],farServes=serves[1],endChanges=p.EndChanges,rally=c.Match.RallyId,shot=c.Match.CurrentShot.ShotId,
                        physicsTicks=c.Ball.PhysicsTicks,winner=c.Match.Winner?.ToString(),phase=c.Match.Phase.ToString(),server=c.Match.Server.ToString(),rules=p.Rules.Values,isolationChecks=isolation};
                }
                void Act(int i)
                {
                    // Online and pause never tick either side's reaction/random state.
                    if(c.Mode!=GameMode.Practice||c.IsPaused||c.Characters.Cinematic||!c.Ball.IsAuthoritative||c.Match.Winner.HasValue)return;
                    var side=(CourtSide)i;uint session=(uint)(9200+i);
                    if(c.ReadyToServe&&c.Match.Server==side)
                    {
                        c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Serve,++ids[i],Time.realtimeSinceStartupAsDouble),session,side);
                        if(c.Match.Phase==MatchPhase.InFlight)serves[i]++;
                    }
                    var state=FullMatchObservation.AiState(c,side,tick,Time.deltaTime);
                    var d=policies[i].Decide(state);
                    if(!d.ShouldSwing)return;
                    attempts[i]++;
                    var result=c.SubmitInput(new RacketInput(side,(StrokeKind)d.Stroke,session,++ids[i],d.Power01,d.AimX,d.AimZ,Time.realtimeSinceStartupAsDouble,c.Match.RallyId,c.Match.CurrentShot.ShotId));
                    if(result!=SwingResult.Hit)rejected[i]++;
                    var ability=c.Characters.Player(side);
                    if(AiSkillPolicy.ShouldActivate(state,policies[i].Settings,ability.Meter.Stock,ability.Remaining,result==SwingResult.Hit))
                        c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Skill,++ids[i],Time.realtimeSinceStartupAsDouble),session,side);
                }
                try
                {
                    Time.timeScale=1;Time.captureDeltaTime=.1f;
                    while(true)
                    {
                        var line=reader.ReadLine();if(line==null)throw new IOException("Self-play client disconnected");
                        var cmd=JsonUtility.FromJson<Command>(line);
                        if(cmd.op=="close"){writer.WriteLine("{\"closed\":true}");break;}
                        if(cmd.op=="checkpoint")
                        {
                            Assert.That(c,Is.Not.Null);
                            // No yield: rules, physics, movement and cinematics remain suspended.
                            writer.WriteLine(JsonUtility.ToJson(Capture()));continue;
                        }
                        if(cmd.op=="reset")
                        {
                            if(c!=null)Assert.That(c.Match.Winner.HasValue,Is.True,"Reset requires actual match completion");
                            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");yield return null;
                            c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
                            c.Configure(GameMode.Practice,Difficulty.Hard,(uint)cmd.seed);c.AutoServe=c.DemoReturns=false;
                            Assert.That(c.Opponent,Is.Null);Assert.That(c.Ball.IsAuthoritative,Is.True);
                            Assert.That(c.Characters.Select(CourtSide.Near,cmd.near),Is.True);Assert.That(c.Characters.Select(CourtSide.Far,cmd.far),Is.True);
                            Assert.That(c.Match.Progress.Rules.Values,Is.EqualTo(new[]{3,6,6,7}));Assert.That(c.Tuning.Hash,Is.EqualTo(GameplayBalance.Defaults.Hash));
                            attempts=new int[2];rejected=new int[2];serves=new int[2];games=new int[2];ids=new uint[2];tick=points=0;isolation=0;
                            for(int i=0;i<2;i++)
                            {
                                selectors[i]=new Selector();policies[i]=new AiPolicy(c.Tuning.Difficulty(Difficulty.Hard),c.Tuning.AiTuning(),(uint)cmd.seed+(uint)(i*7919),selectors[i]);
                                Assert.That(policies[i].Samples,Is.Zero);Assert.That(selectors[i].Decisions,Is.Zero);
                            }
                            Assert.That(policies[0],Is.Not.SameAs(policies[1]));
                            c.IsPaused=true;var ticks=c.Ball.PhysicsTicks;Act(0);Act(1);yield return null;
                            Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));Assert.That(attempts,Is.EqualTo(new[]{0,0}));isolation++;
                            c.IsPaused=false;
                            // Before play starts, prove online mode suppresses these fixture sources.
                            c.Configure(GameMode.Online);Act(0);Act(1);yield return null;
                            Assert.That(attempts,Is.EqualTo(new[]{0,0}));Assert.That(policies[0].Samples+policies[1].Samples,Is.Zero);isolation++;
                            c.Configure(GameMode.Practice,Difficulty.Hard,(uint)cmd.seed);c.AutoServe=c.DemoReturns=false;
                            Assert.That(c.Characters.Select(CourtSide.Near,cmd.near),Is.True);Assert.That(c.Characters.Select(CourtSide.Far,cmd.far),Is.True);
                            writer.WriteLine(JsonUtility.ToJson(Capture()));continue;
                        }
                        Assert.That(cmd.op,Is.EqualTo("step"));Assert.That(c,Is.Not.Null);Assert.That(c.Match.Winner.HasValue,Is.False);
                        Assert.That(cmd.nearAction,Is.InRange(-1,5));Assert.That(cmd.farAction,Is.InRange(-1,5));
                        selectors[0].Action=cmd.nearAction;selectors[1].Action=cmd.farAction;tick++;
                        Act(0);Act(1);yield return null;
                        var p=c.Match.Progress;
                        if(p.TotalPoints!=points){Assert.That(p.TotalPoints,Is.EqualTo(points+1));points=p.TotalPoints;if(p.GameComplete)games[(int)p.LastGameWinner]++;}
                        Assert.That(attempts[0],Is.EqualTo(c.NearHits+rejected[0]));Assert.That(attempts[1],Is.EqualTo(c.FarHits+rejected[1]));
                        writer.WriteLine(JsonUtility.ToJson(Capture()));
                    }
                }
                finally{Time.captureDeltaTime=capture;Time.timeScale=scale;listener.Stop();}
            }
        }
    }
}
