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
    // Test-only, loopback-only stepped Gym transport. No bridge exists in ordinary play.
    public sealed class FullMatchTrainingTests
    {
        [Serializable] public class Command { public string op; public int action, seed, near, far; }
        [Serializable] public class State
        {
            public int observationVersion=2, actionVersion=1, rewardVersion=1;
            public float[] observation;
            public bool done;
            public int nearGames,farGames,nearSets,farSets,nearGameWins,farGameWins,attempts,rejected,hits,decisions,nearServes,farServes,endChanges;
            public long nearPoints,farPoints;
            public uint rally,shot;
            public ulong physicsTicks;
            public string winner,phase,server;
            public int[] rules;
        }
        sealed class ActionSelector : IShotSelector
        {
            public int Action, Decisions;
            public void Select(AiState state,out float width,out float depth)
            { FullMatchActor.Placement(Action,out width,out depth); Decisions++; }
        }
        [UnityTest, Timeout(14400000)]
        public IEnumerator AuthoritativeGymBridge()
        {
            var args=Environment.GetCommandLineArgs(); int flag=Array.IndexOf(args,"-dt-gym-port");
            if(flag<0){Assert.Ignore("Opt-in authoritative training bridge");yield break;}
            int port=int.Parse(args[flag+1]);
            float oldCapture=Time.captureDeltaTime, oldScale=Time.timeScale;
            var listener=new TcpListener(IPAddress.Loopback,port); listener.Start(1);
            Debug.Log("FULL_MATCH_GYM_READY "+port);
            using(var client=listener.AcceptTcpClient())
            using(var stream=client.GetStream())
            using(var reader=new StreamReader(stream,Encoding.UTF8))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false)){AutoFlush=true})
            {
                listener.Stop(); client.NoDelay=true; client.ReceiveTimeout=600000; client.SendTimeout=600000;
                TennisMatchController c=null; AiPolicy executor=null; var selector=new ActionSelector();
                int attempts=0,rejected=0,nearServes=0,nearGameWins=0,farGameWins=0; uint eventId=0; long tick=0; long observedPoints=0;
                State Capture()
                {
                    var p=c.Match.Progress;
                    return new State {observation=FullMatchObservation.Observe(c,CourtSide.Near),done=c.Match.Winner.HasValue,
                        nearPoints=p.NearTotalPoints,farPoints=p.FarTotalPoints,nearGames=p.NearGames,farGames=p.FarGames,nearSets=p.NearSets,farSets=p.FarSets,
                        nearGameWins=nearGameWins,farGameWins=farGameWins,attempts=attempts,rejected=rejected,hits=c.NearHits,decisions=selector.Decisions,
                        nearServes=nearServes,farServes=c.Opponent.Serves,endChanges=p.EndChanges,rally=c.Match.RallyId,shot=c.Match.CurrentShot.ShotId,
                        physicsTicks=c.Ball.PhysicsTicks,winner=c.Match.Winner?.ToString(),phase=c.Match.Phase.ToString(),server=c.Match.Server.ToString(),rules=p.Rules.Values};
                }
                try
                {
                    Time.timeScale=1; Time.captureDeltaTime=.1f;
                    while(true)
                    {
                        string line=reader.ReadLine(); if(line==null)throw new IOException("Trainer disconnected before close");
                        var command=JsonUtility.FromJson<Command>(line);
                        if(command.op=="close") { writer.WriteLine("{\"closed\":true}");break; }
                        if(command.op=="reset")
                        {
                            if(c!=null)Assert.That(c.Match.Winner.HasValue,Is.True,"Never truncate or silently reset a training match");
                            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
                            c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
                            c.Configure(GameMode.SinglePlayer,Difficulty.Medium,(uint)command.seed);
                            Assert.That(c.Characters.Select(CourtSide.Near,command.near),Is.True);
                            Assert.That(c.Characters.Select(CourtSide.Far,command.far),Is.True);
                            Assert.That(c.Match.Progress.Rules.Values,Is.EqualTo(new[]{3,6,6,7}));
                            Assert.That(c.Tuning.Hash,Is.EqualTo(GameplayBalance.Defaults.Hash));
                            Assert.That(c.Ball.IsAuthoritative,Is.True);
                            selector=new ActionSelector(); executor=new AiPolicy(c.Tuning.Difficulty(Difficulty.Hard),c.Tuning.AiTuning(),(uint)command.seed,selector);
                            attempts=rejected=nearServes=nearGameWins=farGameWins=0; observedPoints=0; tick=0;
                            writer.WriteLine(JsonUtility.ToJson(Capture())); continue;
                        }
                        Assert.That(command.op,Is.EqualTo("step")); Assert.That(c,Is.Not.Null);
                        Assert.That(command.action,Is.InRange(0,5)); Assert.That(c.Match.Winner.HasValue,Is.False);
                        selector.Action=command.action;
                        if(!c.IsPaused&&!c.Characters.Cinematic)
                        {
                            if(c.ReadyToServe&&c.Match.Server==CourtSide.Near)
                            {
                                c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Serve,++eventId,Time.realtimeSinceStartupAsDouble),9001,CourtSide.Near);
                                if(c.Match.Phase==MatchPhase.InFlight)nearServes++;
                            }
                            var state=FullMatchObservation.AiState(c,CourtSide.Near,++tick,Time.deltaTime);
                            var decision=executor.Decide(state);
                            if(decision.ShouldSwing)
                            {
                                attempts++;
                                var result=c.SubmitInput(new RacketInput(CourtSide.Near,(StrokeKind)decision.Stroke,9001,++eventId,decision.Power01,decision.AimX,decision.AimZ,
                                    Time.realtimeSinceStartupAsDouble,c.Match.RallyId,c.Match.CurrentShot.ShotId));
                                if(result!=SwingResult.Hit)rejected++;
                                var ability=c.Characters.Player(CourtSide.Near);
                                if(AiSkillPolicy.ShouldActivate(state,executor.Settings,ability.Meter.Stock,ability.Remaining,result==SwingResult.Hit))
                                    c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.Skill,++eventId,Time.realtimeSinceStartupAsDouble),9001,CourtSide.Near);
                            }
                        }
                        yield return null;
                        var score=c.Match.Progress;
                        if(score.TotalPoints!=observedPoints)
                        {
                            Assert.That(score.TotalPoints,Is.EqualTo(observedPoints+1)); observedPoints=score.TotalPoints;
                            if(score.GameComplete){if(score.LastGameWinner==CourtSide.Near)nearGameWins++;else farGameWins++;}
                        }
                        Assert.That(attempts,Is.EqualTo(c.NearHits+rejected));
                        writer.WriteLine(JsonUtility.ToJson(Capture()));
                    }
                }
                finally {Time.captureDeltaTime=oldCapture;Time.timeScale=oldScale;listener.Stop();}
            }
        }
    }
}
