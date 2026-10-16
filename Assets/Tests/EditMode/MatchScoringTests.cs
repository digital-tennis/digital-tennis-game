using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class MatchScoringTests
    {
        private uint rally;
        private static TennisMatchRules Standard(int bestOf=3) => new TennisMatchRules(bestOf,6,6,7);
        private void Point(TennisMatchScore s,CourtSide p,bool advance=true)
        {Assert.That(s.TryAward(++rally,p),Is.True);if(advance&&!s.Winner.HasValue)s.BeginNextPoint();}
        private void Game(TennisMatchScore s,CourtSide p,bool advance=true)
        {for(int i=0;i<4;i++)Point(s,p,i!=3||advance);}
        private void Set(TennisMatchScore s,CourtSide p)
        {for(int i=0;i<6;i++)Game(s,p);}

        [Test]
        public void GameWinContinuesWithNewServerRightCourtAndChangedEnds()
        {
            var s=new TennisMatchScore(CourtSide.Near,Standard());Game(s,CourtSide.Near,false);
            Assert.That(s.GameComplete,Is.True);Assert.That(s.NearGames,Is.EqualTo(1));Assert.That(s.Winner,Is.Null);
            Assert.That(s.Server,Is.EqualTo(CourtSide.Near));Assert.That(s.EndChanges,Is.Zero);
            Assert.That(s.TryAward(rally,CourtSide.Far),Is.False);Assert.That(s.TryAward(rally+1,CourtSide.Far),Is.False);
            s.BeginNextPoint();Assert.That(s.Server,Is.EqualTo(CourtSide.Far));Assert.That(s.EndOf(CourtSide.Near),Is.EqualTo(CourtSide.Far));
            Assert.That(s.Points.TotalPoints,Is.Zero);Assert.That(s.TotalPoints,Is.EqualTo(4));Assert.That(s.Points.NextServeSide,Is.EqualTo(ServeSide.Right));
            Assert.That(s.TryAward(1,CourtSide.Far),Is.False,"An old rally cannot score in the next game.");
            Game(s,CourtSide.Far);Assert.That(s.EndChanges,Is.EqualTo(1));
            Game(s,CourtSide.Near);Assert.That(s.EndChanges,Is.EqualTo(2));
        }
        [TestCase(3,2)] [TestCase(5,3)]
        public void SetsAccumulateAndOnlyRequiredSetCountEndsTheMatch(int bestOf,int needed)
        {
            var s=new TennisMatchScore(CourtSide.Far,Standard(bestOf));
            for(int set=1;set<=needed;set++)
            {
                Set(s,CourtSide.Near);Assert.That(s.NearSets,Is.EqualTo(set));Assert.That(s.CompletedSets.Length,Is.EqualTo(set));
                Assert.That(s.Winner,set==needed?Is.EqualTo(CourtSide.Near):Is.Null);
                if(set<needed){Assert.That(s.NearGames,Is.Zero);Assert.That(s.Points.TotalPoints,Is.Zero);Assert.That(s.Server,Is.EqualTo(CourtSide.Far));}
            }
            Assert.That(s.TotalPoints,Is.EqualTo(24*needed));Assert.That(s.TryAward(++rally,CourtSide.Far),Is.False);
            Assert.That(s.Summary,Does.Contain("6–0"));
        }
        [Test]
        public void FiveAllRequiresTwoGameMarginAndSplitSetsContinueToDecider()
        {
            var s=new TennisMatchScore(CourtSide.Near,Standard());
            for(int i=0;i<5;i++){Game(s,CourtSide.Near);Game(s,CourtSide.Far);}
            Game(s,CourtSide.Near);Assert.That(s.NearGames,Is.EqualTo(6));Assert.That(s.NearSets,Is.Zero);Assert.That(s.TieBreak,Is.False);
            Game(s,CourtSide.Near);Assert.That(s.NearSets,Is.EqualTo(1));Assert.That(s.CompletedSets[0].Display(),Is.EqualTo("7–5"));
            Set(s,CourtSide.Far);Assert.That(s.NearSets,Is.EqualTo(1));Assert.That(s.FarSets,Is.EqualTo(1));Assert.That(s.Winner,Is.Null);
            Set(s,CourtSide.Far);Assert.That(s.Winner,Is.EqualTo(CourtSide.Far));Assert.That(s.CompletedSets.Length,Is.EqualTo(3));
        }
        [Test]
        public void TieBreakUsesOneThenTwoServeOrderSixPointEndChangesAndExtendedMargin()
        {
            var s=new TennisMatchScore(CourtSide.Near,Standard());
            for(int i=0;i<6;i++){Game(s,CourtSide.Near);Game(s,CourtSide.Far);}
            Assert.That(s.TieBreak,Is.True);Assert.That(s.Server,Is.EqualTo(CourtSide.Near));int changes=s.EndChanges;
            var order=new[]{CourtSide.Near,CourtSide.Far,CourtSide.Far,CourtSide.Near,CourtSide.Near,CourtSide.Far,CourtSide.Far};
            for(int i=0;i<6;i++){Assert.That(s.Server,Is.EqualTo(order[i]));Point(s,i%2==0?CourtSide.Near:CourtSide.Far);}
            Assert.That(s.Server,Is.EqualTo(order[6]));Assert.That(s.EndChanges,Is.EqualTo(changes+1));
            for(int i=0;i<6;i++)Point(s,i%2==0?CourtSide.Near:CourtSide.Far);
            Assert.That(s.Display(CourtSide.Near),Is.EqualTo("6"));Assert.That(s.GameComplete,Is.False);
            Point(s,CourtSide.Near);Assert.That(s.Winner,Is.Null);Assert.That(s.NearSets,Is.Zero);
            Point(s,CourtSide.Near,false);Assert.That(s.SetComplete,Is.True);Assert.That(s.NearSets,Is.EqualTo(1));
            Assert.That(s.CompletedSets[0].Display(),Is.EqualTo("7–6 (TB 8–6)"));
            s.BeginNextPoint();Assert.That(s.Server,Is.EqualTo(CourtSide.Far));Assert.That(s.TieBreak,Is.False);
            Assert.That(s.EndChanges,Is.EqualTo(changes+3));Assert.That(s.Points.TotalPoints,Is.Zero);
        }
        [Test]
        public void TieBreakEndingAtSixPointBoundaryChangesEndsOnlyOnce()
        {
            var s=new TennisMatchScore(CourtSide.Near,Standard());
            for(int i=0;i<6;i++){Game(s,CourtSide.Near);Game(s,CourtSide.Far);}
            for(int i=0;i<16;i++)Point(s,i%2==0?CourtSide.Near:CourtSide.Far);
            int changes=s.EndChanges;Point(s,CourtSide.Near);Point(s,CourtSide.Near);
            Assert.That(s.CompletedSets[0].nearTieBreak,Is.EqualTo(10));Assert.That(s.CompletedSets[0].farTieBreak,Is.EqualTo(8));
            Assert.That(s.EndChanges,Is.EqualTo(changes+1));
        }
        [TestCase(6,6,7)] [TestCase(4,4,7)] [TestCase(4,3,10)]
        public void ConfiguredTieBreakTargetDoesNotUseTheOrdinaryFourPointGameRule(int games,int at,int target)
        {
            var s=new TennisMatchScore(CourtSide.Far,new TennisMatchRules(3,games,at,target));
            for(int i=0;i<at;i++){Game(s,CourtSide.Near);Game(s,CourtSide.Far);}
            Assert.That(s.TieBreak,Is.True);
            for(int i=1;i<target;i++){Point(s,CourtSide.Near);Assert.That(s.NearSets,Is.Zero);Assert.That(s.Display(CourtSide.Near),Is.EqualTo(i.ToString()));}
            Point(s,CourtSide.Near);Assert.That(s.NearSets,Is.EqualTo(1));Assert.That(s.CompletedSets[0].nearGames,Is.EqualTo(at+1));
            Assert.That(s.CompletedSets[0].nearTieBreak,Is.EqualTo(target));Assert.That(s.Server,Is.EqualTo(CourtSide.Near));
        }
        [Test]
        public void SwitchedEndsUsePhysicalEndForDiagonalLandingAndShotDirection()
        {
            var m=new TennisMatch();ulong id=0;
            for(int i=0;i<4;i++)
            {
                m.TryServe(m.Server);m.HandleBallEvent(new BallEvent(m.CurrentShot,++id,BallEventKind.Bounce,Vector3.zero,1,true));
                m.HandleBallEvent(new BallEvent(m.CurrentShot,++id,BallEventKind.Bounce,Vector3.zero,2,true));m.AdvanceToNextPoint();
            }
            Assert.That(m.Server,Is.EqualTo(CourtSide.Far));Assert.That(m.EndOf(m.Server),Is.EqualTo(CourtSide.Near));
            Assert.That(m.ServeFromPositiveX,Is.True);Assert.That(m.TryServe(CourtSide.Far),Is.True);
            Assert.That(m.CurrentShot.LastHitter,Is.EqualTo(CourtSide.Far));Assert.That(m.CurrentShot.HitterEnd,Is.EqualTo(CourtSide.Near));
            Assert.That(CourtBounds.Contains(new Vector3(-1,0,4),m.CurrentShot),Is.True);
            Assert.That(CourtBounds.Contains(new Vector3(-1,0,-4),m.CurrentShot),Is.False);
            var input=new RacketInput(CourtSide.Near,StrokeKind.Forehand,1,1,.35f,.4f,0,1,m.RallyId,m.CurrentShot.ShotId);
            var velocity=RacketShot.Velocity(input,end:m.EndOf(CourtSide.Near));Assert.That(velocity.z,Is.LessThan(0));Assert.That(velocity.x,Is.LessThan(0));
        }
        [Test]
        public void RulesCsvRejectsInvalidFormatsAndReloadAffectsOnlyNextMatch()
        {
            string csv=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Balance/rules.csv"));
            foreach(string bad in new[]{csv.Replace("best_of_sets,3,","best_of_sets,4,"),csv.Replace("tie_break_at,6,","tie_break_at,5,"),csv.Replace("tie_break_points,7,","tie_break_points,7.5,"),csv+"best_of_sets,5,duplicate\n"})
                Assert.Throws<FormatException>(()=>TennisMatchRules.Parse(bad));
            string path=Path.Combine(Path.GetTempPath(),"dt-rules-"+Guid.NewGuid()+".csv");
            try
            {
                var store=new TennisRulesStore(path,csv);var running=new TennisMatch(rules:store.NextMatch);
                File.WriteAllText(path,csv.Replace("best_of_sets,3,","best_of_sets,5,"));Assert.That(store.Reload(),Is.True);
                Assert.That(running.Progress.Rules.BestOfSets,Is.EqualTo(3));Assert.That(new TennisMatch(rules:store.NextMatch).Progress.Rules.BestOfSets,Is.EqualTo(5));
                File.WriteAllText(path,"bad");Assert.That(store.Reload(),Is.False);Assert.That(store.NextMatch.BestOfSets,Is.EqualTo(5));
            }finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
