using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class AudioUpgradeTests
    {
        private string Bank=>Path.Combine(Application.streamingAssetsPath,"Audio");
        [Test] public void ExistingFactorySpeechUpgradesWithoutOverwritingVolumeFilesOrCustomCaptions()
        {
            string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
            try
            {
                string csv=File.ReadAllText(Path.Combine(Bank,"audio.csv")),manifest=File.ReadAllText(Path.Combine(Bank,"factory-legacy.json"));
                var local=AudioCatalog.Parse(csv);local["KOR.ready"].File="KOR_ready.wav";local["KOR.ready"].Volume=.17f;
                File.Copy(Path.Combine(Bank,"KOR_ready.wav"),Path.Combine(dir,"KOR_ready.wav"));
                byte[] original=File.ReadAllBytes(Path.Combine(dir,"KOR_ready.wav"));
                var changed=AudioFactoryUpgrade.Apply(local,AudioCatalog.Parse(csv),manifest,dir);
                Assert.That(changed,Does.Contain("KOR.ready"));Assert.That(local["KOR.ready"].File,Is.EqualTo("KOR_ready_v2.wav"));Assert.That(local["KOR.ready"].Volume,Is.EqualTo(.17f));
                Assert.That(File.ReadAllBytes(Path.Combine(dir,"KOR_ready.wav")),Is.EqualTo(original));
                var captions=new Dictionary<string,string>{{"KOR.ready","새 기본 대사"}};
                AudioFactoryUpgrade.MergeCaptions(captions,"cue,language,text\nKOR.ready,ko,준비됐어요!",manifest,changed);
                Assert.That(captions["KOR.ready"],Is.EqualTo("새 기본 대사"));
                AudioFactoryUpgrade.MergeCaptions(captions,"cue,language,text\nKOR.ready,ko,내가 직접 편집한 대사",manifest,changed);
                Assert.That(captions["KOR.ready"],Is.EqualTo("내가 직접 편집한 대사"));
                File.AppendAllText(Path.Combine(dir,"KOR_ready.wav"),"custom waveform edit");local["KOR.ready"].File="KOR_ready.wav";
                Assert.That(AudioFactoryUpgrade.Apply(local,AudioCatalog.Parse(csv),manifest,dir),Does.Not.Contain("KOR.ready"));
                Assert.That(local["KOR.ready"].File,Is.EqualTo("KOR_ready.wav"));
            }
            finally{Directory.Delete(dir,true);}
        }
        [Test] public void OlderCsvGetsEveryMissingFactoryCueButKeepsExplicitCustomCue()
        {
            string csv=File.ReadAllText(Path.Combine(Bank,"audio.csv"));
            var lines=new List<string>(csv.TrimEnd().Split('\n'));lines.RemoveAll(s=>s.StartsWith("step_")||s.StartsWith("hit_")||s.StartsWith("skid_"));
            string old=string.Join("\n",lines).Replace("KOR_ready_v2.wav","my-voice.wav");
            var merged=AudioCatalog.WithOptionalDefaults(old,csv);
            Assert.That(merged["step_0"].FactoryFile,Is.True);Assert.That(merged["KOR.ready"].File,Is.EqualTo("my-voice.wav"));
        }
        [Test] public void OfficialCallInterruptsPersonaAndRepeatedOrExpiredRequestsAreDropped()
        {
            var q=new TennisVoiceQueue();Assert.That(q.Enqueue("KOR.skill",0));Assert.That(q.Take(0,0),Is.EqualTo("KOR.skill"));
            Assert.That(q.Enqueue("KOR.skill",.2f),Is.False);q.Enqueue("JPN.skill",.3f);q.Enqueue("umpire.out",.4f);
            Assert.That(q.Take(.4f,1),Is.EqualTo("umpire.out"));Assert.That(q.Take(.5f,0),Is.Null);
            q.Enqueue("KOR.win",1);Assert.That(q.Take(6,0),Is.Null);q.Reset();Assert.That(q.Enqueue("KOR.skill",.6f));
        }
        [Test] public void DisplayedMovementProducesCadenceAndBrakingButNeverTeleportsOrPause()
        {
            var t=new TennisFootstepTrack();t.Observe(Vector3.zero,.1f,0,true);int steps=0,skids=0;
            for(int i=1;i<=12;i++){int r=t.Observe(new Vector3(i*.3f,0,0),.1f,i*.1f,true);if(r==1)steps++;}
            skids+=t.Observe(new Vector3(3.6f,0,0),.1f,1.3f,true)==2?1:0;
            Assert.That(steps,Is.InRange(3,5));Assert.That(skids,Is.EqualTo(1));
            Assert.That(t.Observe(new Vector3(30,0,0),.1f,1.4f,true),Is.Zero);
            Assert.That(t.Observe(Vector3.zero,.1f,1.5f,false),Is.Zero);
            Assert.That(t.Observe(new Vector3(.1f,0,0),.1f,1.6f,true),Is.Zero);
        }
    }
}
