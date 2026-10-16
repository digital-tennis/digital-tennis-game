using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DigitalTennis.AI;
using UnityEngine;
namespace DigitalTennis.Editor
{
    /// <summary>Offline measurement, using production rules, physics, hit gate, abilities and the pinned external AI.
    /// Local scripts are test input fixtures, never a replacement opponent policy.</summary>
    public static class BalanceExperiment
    {
        [Serializable]private sealed class Row
        {public string near,far,difficulty,strategy,firstServer;public uint seed;public int points,nearWins,farWins,hits,attempts,rejected,skills,timeouts;public int[] strokes=new int[7];public float nearEnergy,farEnergy,seconds;}
        [Serializable]private sealed class Report{public string scope,gameplayHash,characterHash;public bool deterministic;public int cases,points,timeouts;public Row[] results;}
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-experiment-out");string dir=flag>=0?args[flag+1]:".work/quality/balance-baseline";Directory.CreateDirectory(dir);
            var tuning=GameplayBalance.Defaults;string csv=File.ReadAllText("Assets/StreamingAssets/Balance/characters.csv");var characters=CharacterBalanceCsv.Parse(csv);var rows=new List<Row>();
            var seeds=new uint[]{17,4016,9001};string[] strategies={"placement","early-mix","patient-mix"};
            Row First()=>Measure(0,0,Difficulty.Easy,0,17,CourtSide.Near,tuning,characters);
            bool deterministic=JsonUtility.ToJson(First())==JsonUtility.ToJson(First());if(!deterministic)throw new Exception("Experiment is not reproducible");
            for(int n=0;n<15;n++){Debug.Log("BALANCE_PROGRESS near="+CharacterCatalog.Ids[n]);for(int f=0;f<15;f++)foreach(var difficulty in new[]{Difficulty.Easy,Difficulty.Medium,Difficulty.Hard})foreach(uint seed in seeds)for(int strategy=0;strategy<3;strategy++)foreach(var server in new[]{CourtSide.Near,CourtSide.Far})
                rows.Add(Measure(n,f,difficulty,strategy,seed,server,tuning,characters));}
            var report=new Report{scope="225 ordered pairs x 3 difficulties x 3 seeds x 3 local input strategies x 2 starting servers; 4 real points per case. Detached fixed-step production models; excludes rendering, transport, human skill and perception. All point awards come from ball/rule events.",gameplayHash=tuning.Hash,characterHash=Hash128.Compute(csv).ToString(),deterministic=deterministic,cases=rows.Count,points=rows.Sum(r=>r.points),timeouts=rows.Sum(r=>r.timeouts),results=rows.ToArray()};
            File.WriteAllText(Path.Combine(dir,"experiment.json"),JsonUtility.ToJson(report,true));
            using(var writer=new StreamWriter(Path.Combine(dir,"cases.csv")))
            {writer.WriteLine("near,far,difficulty,strategy,seed,first_server,points,near_wins,far_wins,hits,attempts,rejected,skills,near_energy,far_energy,sim_seconds,timeouts,forehand,backhand,smash,volley,slice,lob");foreach(var r in rows)writer.WriteLine(string.Join(",",r.near,r.far,r.difficulty,r.strategy,r.seed,r.firstServer,r.points,r.nearWins,r.farWins,r.hits,r.attempts,r.rejected,r.skills,r.nearEnergy.ToString("F3",CultureInfo.InvariantCulture),r.farEnergy.ToString("F3",CultureInfo.InvariantCulture),r.seconds.ToString("F3",CultureInfo.InvariantCulture),r.timeouts,string.Join(",",r.strokes.Skip(1))));}
            Debug.Log("BALANCE_EXPERIMENT cases="+report.cases+" points="+report.points+" timeouts="+report.timeouts+" deterministic="+deterministic);if(report.timeouts>0)throw new Exception("Unresolved experiment points");
        }
        private static Row Measure(int near,int far,Difficulty difficulty,int strategy,uint seed,CourtSide server,GameplayBalance c,Dictionary<string,CharacterBalance> balances)
        {
            var result=new Row{near=CharacterCatalog.Ids[near],far=CharacterCatalog.Ids[far],difficulty=difficulty.ToString(),strategy=new[]{"placement","early-mix","patient-mix"}[strategy],seed=seed,firstServer=server.ToString()};
            var ability=new[]{new CharacterAbility(near,balances[result.near],c),new CharacterAbility(far,balances[result.far],c)};
            var m=new TennisMatch(server);var ball=new BallSimulation(c.Physics());var gate=new RacketInputGate();var ai=new AiPolicy(c.Difficulty(difficulty),c.AiTuning(),seed);var random=new System.Random((int)seed);uint sequence=0,predicted=0;long tick=0;float clock=0,eligibleAge=0;uint eligibleShot=0;bool willTry=true;StrokeKind last=StrokeKind.Forehand;
            var position=new[]{AutomaticCourtMovement.Home(CourtSide.Near,.85f,c),AutomaticCourtMovement.Home(CourtSide.Far,.85f,c)};var target=(Vector3[])position.Clone();
            Vec3 V(Vector3 v)=>new Vec3(v.x,v.y,v.z);
            AiState State()=>new AiState(tick,BallSimulation.FixedStep,1,V(position[1]),V(position[0]),V(ball.Position),V(ball.Velocity),m.Phase==MatchPhase.Rally?AiPhase.Rally:AiPhase.ServeInFlight,ball.BounceCount,((ulong)m.RallyId<<32)|m.CurrentShot.ShotId,(int)m.LastHitter,ball.HasShot,ball.IsResting,false,ball.Gravity,ball.Radius,ball.BounceRestitution,ball.GroundSpeedRetention,ability[1].Reach,ability[1].ShotMultiplier(StrokeKind.Forehand));
            void Swing(RacketInput input)
            {
                result.attempts++;int side=(int)input.Player;var outcome=gate.Evaluate(input,clock,m,ball,position[side],true,ability[side].Reach,c);
                if(outcome!=SwingResult.Hit){result.rejected++;return;}
                float incoming=ball.Velocity.magnitude;var velocity=RacketShot.CharacterVelocity(input,ball.Position,ball.Gravity,ball.Radius,ability[side].ShotMultiplier(input.Stroke),c,m.EndOf(input.Player));
                if(!m.TryReturn(input.Player))throw new Exception("Gate and rules disagree");ball.Launch(ball.Position,velocity,m.CurrentShot);ability[side].Returned(incoming);last=input.Stroke;result.hits++;result.strokes[(int)last]++;
                bool skill=side==0?strategy!=2||result.hits%3==0:AiSkillPolicy.ShouldActivate(State(),ai.Settings,ability[1].Meter.Stock,ability[1].Remaining,true);
                if(skill&&ability[side].Activate())result.skills++;
            }
            const int maxTicks=60*240;
            for(int steps=0;steps<maxTicks&&m.Progress.TotalPoints<4;steps++)
            {
                tick++;clock+=BallSimulation.FixedStep;bool cinematic=ability.Any(a=>a.Cinematic);foreach(var a in ability)a.Tick(BallSimulation.FixedStep,m.Phase==MatchPhase.WaitServe||m.Phase==MatchPhase.PointEnd);
                if(cinematic||ability.Any(a=>a.Cinematic))continue;
                if(m.Phase==MatchPhase.PointEnd){ball.Stop();m.AdvanceToNextPoint();foreach(var a in ability)a.Tick(1.5f,true);predicted=0;}
                if(m.Phase==MatchPhase.WaitServe)
                {
                    int who=(int)m.Server;float facing=m.EndOf(m.Server)==CourtSide.Near?1:-1;
                    var origin=new Vector3((m.ServeFromPositiveX?1:-1)*c["serve.offset"],c["serve.height"],-facing*c["serve.depth"]);
                    position[who]=new Vector3(origin.x,.85f,origin.z);position[1-who]=new Vector3(-origin.x,.85f,facing*c["serve.receiver_depth"]);
                    float time=c["serve.flight"]/ability[who].ServeSpeed;var landing=new Vector3(-origin.x,ball.Radius,facing*c["serve.target_depth"]);
                    if(!m.TryServe(m.Server))throw new Exception("Serve failed");ball.Launch(origin,(landing-origin)/time+Vector3.up*(.5f*ball.Gravity*time),m.CurrentShot);last=StrokeKind.Forehand;predicted=0;
                }
                var decision=ai.Decide(State());
                if(decision.ShouldSwing)Swing(new RacketInput(CourtSide.Far,(StrokeKind)decision.Stroke,2,++sequence,decision.Power01,decision.AimX,decision.AimZ,clock,m.RallyId,m.CurrentShot.ShotId));
                if(ability.Any(a=>a.Cinematic))continue;
                if(predicted!=m.CurrentShot.ShotId)
                {
                    predicted=m.CurrentShot.ShotId;target[0]=AutomaticCourtMovement.Home(m.EndOf(CourtSide.Near),.85f,c);target[1]=AutomaticCourtMovement.Home(m.EndOf(CourtSide.Far),.85f,c);
                    int receiver=1-(int)m.LastHitter;target[receiver]=AutomaticCourtMovement.PredictTarget(ball,m.EndOf((CourtSide)receiver),.85f,c,last==StrokeKind.Lob);
                    if(receiver==0&&strategy==1&&m.Phase==MatchPhase.Rally)
                    {
                        var early=ball.CreatePrediction();for(int frame=0;frame<180&&!early.IsResting;frame++){early.Step();if(early.BounceCount>0)break;bool own=m.EndOf(CourtSide.Near)==CourtSide.Near?early.Position.z<-.5f:early.Position.z>.5f;if(own&&early.Velocity.y<0&&early.Position.y<1.7f){target[0]=AutomaticCourtMovement.ClampTarget(early.Position,m.EndOf(CourtSide.Near),.85f,c);break;}}
                    }
                }
                for(int side=0;side<2;side++)
                {var before=position[side];var move=side==1?new Vector3(decision.MoveTarget.X,.85f,decision.MoveTarget.Z):target[side];position[side]=AutomaticCourtMovement.Step(before,move,m.EndOf((CourtSide)side),BallSimulation.FixedStep,ability[side].MoveSpeed,c);ability[side].Moved(Vector3.Distance(before,position[side]));}
                bool eligible=RacketInputGate.CanHit(CourtSide.Near,m,ball,position[0],ability[0].Reach,c)==SwingResult.Hit;
                if(eligibleShot!=m.CurrentShot.ShotId){eligibleShot=m.CurrentShot.ShotId;eligibleAge=0;willTry=random.NextDouble()<(strategy==0?.92:strategy==1?.96:.84);}if(eligible)eligibleAge+=BallSimulation.FixedStep;
                if(eligible&&willTry&&eligibleAge>=(strategy==0?.05f:strategy==1?.02f:.16f))
                {
                    float facing=m.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                    StrokeKind kind=(ball.Position.x-position[0].x)*facing>=0?StrokeKind.Forehand:StrokeKind.Backhand;
                    if(strategy>0){if(ball.Position.y>=c["hit.smash_height"]&&last==StrokeKind.Lob)kind=StrokeKind.Smash;else if(ball.BounceCount==0&&strategy==1)kind=StrokeKind.Volley;else if(result.hits%5==2)kind=StrokeKind.Lob;else if(result.hits%5==4)kind=StrokeKind.Slice;}
                    float tx=strategy==2?0:position[1].x>=0?-2.8f:2.8f;tx+=(float)(random.NextDouble()-.5)*.6f;
                    float dz=Mathf.Max(1,7.5f-ball.Position.z*facing),dx=(tx-ball.Position.x)*facing;
                    float yaw=Mathf.Clamp(Mathf.Atan2(dx,dz)*Mathf.Rad2Deg/c["shot.yaw"],-1,1),angle=yaw*c["shot.yaw"]*Mathf.Deg2Rad,distance=dz/Mathf.Cos(angle);
                    float lift=kind==StrokeKind.Slice?c["slice.lift"]:kind==StrokeKind.Volley?c["volley.lift"]:1,forward=kind==StrokeKind.Slice?c["slice.forward"]:kind==StrokeKind.Volley?c["volley.forward"]:1;
                    float pitch=Mathf.Atan(Mathf.Tan(c["shot.pitch"]*Mathf.Deg2Rad)*lift/forward),denominator=2*Mathf.Pow(Mathf.Cos(pitch),2)*(ball.Position.y-ball.Radius+distance*Mathf.Tan(pitch));
                    float speed=Mathf.Sqrt(ball.Gravity*distance*distance/Mathf.Max(.001f,denominator))/Mathf.Sqrt(Mathf.Pow(Mathf.Cos(c["shot.pitch"]*Mathf.Deg2Rad)*forward,2)+Mathf.Pow(Mathf.Sin(c["shot.pitch"]*Mathf.Deg2Rad)*lift,2));
                    float power=Mathf.Clamp01((speed/ability[0].ShotMultiplier(kind)-c["shot.base_speed"])/c["shot.power_speed"]);
                    Swing(new RacketInput(CourtSide.Near,kind,1,++sequence,power,yaw,0,clock,m.RallyId,m.CurrentShot.ShotId));
                }
                if(ability.Any(a=>a.Cinematic))continue;
                var events=ball.Step();foreach(var e in events){var previous=m.Phase;if(m.HandleBallEvent(e)&&previous==MatchPhase.InFlight&&m.Phase==MatchPhase.Rally)ability[(int)m.Server].ValidServe();}
                if(ball.IsResting)m.ResolveStoppedBall(m.CurrentShot.ShotId);
                if(!BallSimulation.IsFinite(ball.Position)||!ability.All(a=>a.Capture().IsValid))throw new Exception("Invalid measurement state");
            }
            result.points=(int)m.Progress.TotalPoints;result.nearWins=(int)m.Progress.NearTotalPoints;result.farWins=(int)m.Progress.FarTotalPoints;result.nearEnergy=ability[0].Energy;result.farEnergy=ability[1].Energy;result.seconds=clock;result.timeouts=result.points<4?1:0;return result;
        }
    }
}
