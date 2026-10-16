using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DigitalTennis.AI;
using UnityEngine;
namespace DigitalTennis
{
    /// <summary>CLI-only ordinary adapter inputs; no writes to ball, score, stock or AI policy.</summary>
    public sealed class BalanceSurveyDriver : MonoBehaviour
    {
        private int errors;
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-dt-balance-survey");
            if(flag<0||flag+1>=args.Length){enabled=false;yield break;}
            string dir=args[flag+1];Directory.CreateDirectory(dir);Application.runInBackground=true;
            Application.logMessageReceived+=Error;yield return null;
            var c=GetComponent<TennisMatchController>();var results=new List<Case>();uint seq=1;
            for(int index=0;index<15;index++)foreach(var difficulty in new[]{Difficulty.Easy,Difficulty.Medium,Difficulty.Hard})
            {
                c.Configure(GameMode.SinglePlayer,difficulty,42);c.IsPaused=false;c.InputAdapter.GameplayEnabled=true;
                c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.SelectCharacter,seq++,Time.realtimeSinceStartupAsDouble,.35f,choice:index),987,CourtSide.Near);
                c.ApplyPlayerInput(new PlayerInputSample(PlayerAction.SelectCharacter,seq++,Time.realtimeSinceStartupAsDouble,.35f,choice:(index+1)%15),987,CourtSide.Far);
                c.InputAdapter.UseSource(new Probe(c));double start=Time.realtimeSinceStartupAsDouble,end=start+25;ulong ticks=c.Ball.PhysicsTicks;
                while(c.Match.Progress.TotalPoints==0&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                while(!c.ReadyToServe&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                results.Add(new Case{character=CharacterCatalog.Ids[index],opponent=CharacterCatalog.Ids[(index+1)%15],difficulty=difficulty.ToString(),points=c.Match.Progress.TotalPoints,hits=c.Hits,nearHits=c.NearHits,farHits=c.FarHits,aiAttempts=c.Opponent.Attempts,aiRejected=c.Opponent.RejectedHits,nextServe=c.ReadyToServe,seconds=(float)(Time.realtimeSinceStartupAsDouble-start),ticks=(long)(c.Ball.PhysicsTicks-ticks)});
                Debug.Log("BALANCE_SURVEY "+index+" "+difficulty+" points="+c.Match.Progress.TotalPoints);
            }
            bool passed=errors==0&&results.Count==45&&results.TrueForAll(x=>x.points>0&&x.nextServe&&x.ticks>0);
            File.WriteAllText(Path.Combine(dir,"survey.json"),JsonUtility.ToJson(new Report{passed=passed,runtimeErrors=errors,cases=results.ToArray(),scope="Fixed seed42, scripted local adapter vs three AI policies, one real point per case; not human win rates or all-pairs balance"},true));
            Application.logMessageReceived-=Error;Application.Quit(passed?0:1);
        }
        private void Error(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [Serializable]private sealed class Case{public string character,opponent,difficulty;public long points,ticks;public int hits,nearHits,farHits,aiAttempts,aiRejected;public float seconds;public bool nextServe;}
        [Serializable]private sealed class Report{public bool passed;public int runtimeErrors;public string scope;public Case[] cases;}
        private sealed class Probe:BufferedPlayerInputSource
        {
            private readonly TennisMatchController c;private uint sequence;private double previous;
            public Probe(TennisMatchController value):base("Balance survey (CLI only)"){c=value;}
            public override void Poll(double now,float power,float aimX,float aimZ)
            {
                if(c.Match.Progress.TotalPoints>0||now-previous<.1)return;PlayerAction action;
                if(c.ReadyToServe&&c.Match.Server==CourtSide.Near)action=PlayerAction.Serve;
                else if(c.Hits<4&&c.CanReturn(CourtSide.Near))action=c.NearHits%2==0?PlayerAction.Forehand:PlayerAction.Backhand;
                else return;
                previous=now;float facing=c.Match.EndOf(CourtSide.Near)==CourtSide.Near?1:-1;
                float aim=Mathf.Clamp(Mathf.Atan2(-c.Ball.Simulation.Position.x*1.6f*facing,17)*Mathf.Rad2Deg/c.Tuning["shot.yaw"],-1,1);
                Enqueue(new PlayerInputSample(action,++sequence,now,.35f,aim));
            }
        }
    }
}
