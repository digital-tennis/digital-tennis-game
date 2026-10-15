using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class GameplayBalanceTests
    {
        public static string Defaults => File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Balance/gameplay.csv"));
        public static string Change(string text,string key,string value)
        {
            var lines=text.Split('\n');
            for(int i=1;i<lines.Length;i++)if(lines[i].StartsWith(key+",",StringComparison.Ordinal))
            {var cells=lines[i].Split(',');cells[1]=value;lines[i]=string.Join(",",cells);return string.Join("\n",lines);}
            throw new Exception("Unknown fixture key "+key);
        }
        [Test]
        public void BundleRejectsEitherInvalidFileWithoutPartialCommitAndBootsWithDefaults()
        {
            string dir=Path.Combine(Path.GetTempPath(),"dt-bundle-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            string game=Path.Combine(dir,"gameplay.csv"),characters=Path.Combine(dir,"characters.csv");
            try {
                File.WriteAllText(game,Change(Defaults,"move.speed","NaN"));File.WriteAllText(characters,CharacterSystemTests.Defaults.Replace("KOR,70,","KOR,88,"));
                var store=new BalanceBundleStore(game,characters,Defaults,CharacterSystemTests.Defaults);
                Assert.That(store.Current["move.speed"],Is.EqualTo(5));Assert.That(store.Characters[0].serve,Is.EqualTo(70));
                var revision=store.Revision;var previous=store.Current;
                File.WriteAllText(game,Change(Defaults,"move.speed","6"));File.WriteAllText(characters,"bad");
                Assert.That(store.Reload(),Is.False);Assert.That(store.Current,Is.SameAs(previous));Assert.That(store.Revision,Is.EqualTo(revision));
                File.WriteAllText(characters,CharacterSystemTests.Defaults.Replace("KOR,70,","KOR,88,"));
                Assert.That(store.Reload(),Is.True);Assert.That(store.Current["move.speed"],Is.EqualTo(6));Assert.That(store.Characters[0].serve,Is.EqualTo(88));
                var local=store.Current;store.Present(GameplayBalance.Defaults,99);store.RestoreLocal();Assert.That(store.Current,Is.SameAs(local));Assert.That(store.Revision,Is.EqualTo(revision+1));
            }finally{Directory.Delete(dir,true);}
        }
        [Test]
        public void SchemaRejectsDuplicatesMissingUnknownNonFiniteAndInconsistentValues()
        {
            foreach(var bad in new[]{Defaults+"move.speed,6,m/s,duplicate\n",Defaults.Replace("move.speed,5,m/s,기본 이동 속도\n",""),
                Defaults+"unknown.key,1,ratio,unknown\n",Change(Defaults,"physics.gravity","Infinity"),Change(Defaults,"ai.hard.skill_stock","1.5"),
                Change(Defaults,"hit.max_height","1.5"),Change(Defaults,"energy.move_cost","0.1"),Change(Defaults,"shot.pitch","5")})
                Assert.Throws<FormatException>(()=>GameplayBalance.Parse(bad));
            var culture=CultureInfo.CurrentCulture;
            try{CultureInfo.CurrentCulture=new CultureInfo("de-DE");Assert.That(GameplayBalance.Parse("\ufeff"+Defaults.Replace("move.speed,","\"move.speed\",").Replace("\n","\r\n")).Hash,Is.EqualTo(GameplayBalance.Defaults.Hash));}
            finally{CultureInfo.CurrentCulture=culture;}
        }
        [Test]
        public void CsvChangesActualShotsPhysicsMovementAndEnergyAndPreservesEventIdentity()
        {
            string text=Change(Change(Change(Change(Defaults,"shot.base_speed","8"),"move.speed","6"),"physics.bounce","0.5"),"energy.hit_cost","8");
            var config=GameplayBalance.Parse(text);var baseline=GameplayBalance.Defaults;
            var input=new RacketInput(CourtSide.Near,StrokeKind.Forehand,1,1,0,0,0,1,1,1);
            Assert.That(RacketShot.Velocity(input,config).magnitude,Is.EqualTo(8).Within(.001));
            Assert.That(AutomaticCourtMovement.Step(new Vector3(0,.85f,-8),new Vector3(0,.85f,-5),CourtSide.Near,.1f,settings:config).z,Is.EqualTo(-7.4f).Within(.001));
            var character=CharacterBalanceCsv.Parse(CharacterSystemTests.Defaults)["KOR"];
            var a=new CharacterAbility(0,character,config);var b=new CharacterAbility(0,character,baseline);a.Returned(10);b.Returned(10);Assert.That(a.Energy,Is.LessThan(b.Energy));
            var ball=new BallSimulation();ulong last=0;float rebound=0;
            for(uint shot=1;shot<=2;shot++) {
                ball.Stop();ball.Configure((shot==1?baseline:config).Physics());ball.Launch(new Vector3(0,1,4),Vector3.down,new ShotContext(1,CourtSide.Near,shotId:shot));
                for(int i=0;i<100;i++){var events=ball.Step();if(events.Count==0)continue;Assert.That(events[0].EventId,Is.GreaterThan(last));last=events[0].EventId;if(shot==1)rebound=ball.Velocity.y;else Assert.That(ball.Velocity.y,Is.LessThan(rebound));break;}
            }
        }
        [Test]
        public void WireUsesCanonicalOrderAndRejectsTamperedAndPartialSettings()
        {
            var b=GameplayBalance.Defaults;
            Assert.That(GameplayBalance.TryWire(b.Values,b.Hash,out var decoded),Is.True);Assert.That(decoded["move.speed"],Is.EqualTo(b["move.speed"]));
            var values=b.Values;values[0]+=.01f;Assert.That(GameplayBalance.TryWire(values,b.Hash,out _),Is.False);
            Assert.That(GameplayBalance.TryWire(new float[1],b.Hash,out _),Is.False);
            Assert.That(GameplayBalance.TryWire(null,null,out _),Is.False);
        }
    }
}
