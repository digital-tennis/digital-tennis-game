using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class TennisEffectTests
    {
        private static string Defaults=>File.ReadAllText("Assets/StreamingAssets/Effects/effects.csv");
        private static TennisEffectEvent Item(uint id,float age=0)=>new TennisEffectEvent {id=id,kind=1,player=0,character=0,stroke=1,age=age,position=new[]{1f,2f,3f},direction=new[]{0f,0f,1f}};
        [Test]
        public void CsvUsesInvariantNumbersAndLeavesEveryCharacterPaletteAvailable()
        {
            var previous=CultureInfo.CurrentCulture;
            try {CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");var presets=TennisEffectsCsv.Parse("\ufeff"+Defaults.Replace("\n","\r\n"));
                Assert.That(presets[4].Duration,Is.EqualTo(1.2f));
                foreach(var profile in CharacterCatalog.All)Assert.That(ColorUtility.TryParseHtmlString(profile.accent,out _),Is.True);}
            finally {CultureInfo.CurrentCulture=previous;}
        }
        [TestCase("hit,1,18,0.24,0.42,2.4,#FFF6D6","hit,1,999,0.24,0.42,2.4,#FFF6D6")]
        [TestCase("hit,1,18,0.24,0.42,2.4,#FFF6D6","hit,1,18,NaN,0.42,2.4,#FFF6D6")]
        [TestCase("skill,1,48,1.2,1.3,2.6,character","skill,1,48,1.3,1.3,2.6,character")]
        [TestCase("bounce,1,10,0.45,0.38,0.8,#B9DED1","hit,1,10,0.45,0.38,0.8,#B9DED1")]
        [TestCase("#B9DED1","not-a-color")]
        public void CsvRejectsUnboundedNonFiniteDuplicateAndMalformedEffects(string from,string to)
        {Assert.Throws<FormatException>(()=>TennisEffectsCsv.Parse(Defaults.Replace(from,to)));}
        [Test]
        public void LiveCsvReloadIsAtomicAndKeepsPriorPresetOnFailure()
        {
            string folder=Path.Combine(Path.GetTempPath(),"dt-effect-test-"+Guid.NewGuid().ToString("N"));string path=Path.Combine(folder,"effects.csv");
            try
            {
                var store=new TennisEffectStore(path,Defaults);uint revision=store.Revision;int count=store[TennisEffectKind.Hit].Count;
                File.WriteAllText(path,"broken");Assert.That(store.Reload(),Is.False);Assert.That(store.Revision,Is.EqualTo(revision));Assert.That(store[TennisEffectKind.Hit].Count,Is.EqualTo(count));
                File.WriteAllText(path,Defaults.Replace("hit,1,18,","hit,1,26,"));Assert.That(store.Reload(),Is.True);Assert.That(store[TennisEffectKind.Hit].Count,Is.EqualTo(26));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [Test]
        public void RemoteCheckpointDuplicateReorderAndReconnectNeverReplayOldEvents()
        {
            var gate=new TennisEffectGate();int played=0;
            gate.Consume("m",1,new[]{Item(1)},0,_=>played++);Assert.That(played,Is.Zero);
            gate.Consume("m",3,new[]{Item(1),Item(2),Item(3)},.1f,e=>{Assert.That(e.age,Is.EqualTo(.1f));played++;});
            gate.Consume("m",3,new[]{Item(2),Item(3)},0,_=>played++);gate.Consume("m",2,new[]{Item(2)},0,_=>played++);
            Assert.That(played,Is.EqualTo(2));gate.Reset();gate.Consume("m",3,new[]{Item(2),Item(3)},0,_=>played++);
            gate.Consume("m",4,new[]{Item(3),Item(4)},0,_=>played++);Assert.That(played,Is.EqualTo(3));Assert.That(gate.Baselines,Is.EqualTo(2));
            gate.Consume("new-match",1,new[]{Item(1)},0,_=>played++);Assert.That(played,Is.EqualTo(3));
        }
        [Test]
        public void HistoryIsBoundedExpiresAndUsesWorldContactPositions()
        {
            var journal=new TennisEffectJournal();
            for(int i=0;i<20;i++)journal.Add(TennisEffectKind.Bounce,CourtSide.Far,14,0,new Vector3(i,.033f,4),Vector3.up,1);
            var snapshot=journal.Capture(1.5f);Assert.That(snapshot.Length,Is.EqualTo(TennisEffectJournal.Capacity));Assert.That(snapshot[15].position[0],Is.EqualTo(19));
            Assert.That(snapshot[15].age,Is.EqualTo(.5f));Assert.That(TennisEffectJournal.Valid(journal.Sequence,snapshot),Is.True);Assert.That(journal.Capture(3.1f),Is.Empty);
            snapshot[0].position[0]=float.NaN;Assert.That(TennisEffectJournal.Valid(journal.Sequence,snapshot),Is.False);
        }
        [Test]
        public void SequenceWrapAndMalformedRecordsCannotDuplicatePresentation()
        {
            var gate=new TennisEffectGate();int n=0;gate.Consume("m",uint.MaxValue,new[]{Item(uint.MaxValue)},0,_=>n++);
            gate.Consume("m",1,new[]{Item(1)},0,_=>n++);Assert.That(n,Is.EqualTo(1));
            Assert.That(TennisEffectJournal.Valid(2,new[]{Item(2),Item(2)}),Is.False);
            Assert.That(TennisEffectJournal.Valid(2,new[]{Item(1)}),Is.False);
            var bad=Item(2);bad.direction[0]=10;Assert.That(bad.IsValid,Is.False);
        }
    }
}
