using System;
using System.IO;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class ResumedActorTests
    {
        [Serializable] public sealed class Parity { public Case[] cases; }
        [Serializable] public sealed class Case { public float[] raw,observation,logits;public int action; }
        [Test] public void InvalidOverrideFallsBackToBundledAndMissingBundleToExplicitDefault()
        {
            string path=Path.Combine(Path.GetTempPath(),"tennis-actor-"+Guid.NewGuid()+".json");
            try
            {
                File.WriteAllText(path,"{\"schema\":999}");
                Assert.That(FullMatchPolicyAdapter.Load(out var status,path),Is.Not.Null);
                StringAssert.Contains("교체 오류",status);
                Assert.That(FullMatchPolicyAdapter.Load(out status,path,"missing-test-actor"),Is.Null);
                StringAssert.Contains("기본 AI",status);
                File.WriteAllText(path,new string(' ',256*1024+1));
                Assert.That(FullMatchPolicyAdapter.Load(out status,path),Is.Not.Null);
                StringAssert.Contains("교체 오류",status);
            }
            finally {File.Delete(path);}
        }
        [Test] public void UnityJsonActorMatchesPythonNormalizationLogitsAndActions()
        {
            var asset=Resources.Load<TextAsset>("FullMatchAI/resumed-ppo");Assert.That(asset,Is.Not.Null);
            var data=JsonUtility.FromJson<FullMatchModelData>(asset.text);var actor=new FullMatchActor(data);
            var parity=JsonUtility.FromJson<Parity>(Resources.Load<TextAsset>("FullMatchAI/resumed-parity").text);
            Assert.That(parity.cases.Length,Is.EqualTo(64));
            foreach(var c in parity.cases)
            {
                var normalized=FullMatchActor.Normalize(c.raw);var logits=actor.Logits(c.observation);
                for(int i=0;i<32;i++)Assert.That(normalized[i],Is.EqualTo(c.observation[i]).Within(.000001f));
                for(int i=0;i<6;i++)Assert.That(logits[i],Is.EqualTo(c.logits[i]).Within(.00001f));
                Assert.That(actor.Predict(c.observation),Is.EqualTo(c.action));
            }
            data.w1[0]=float.NaN;Assert.Throws<ArgumentException>(()=>new FullMatchActor(data));
            Assert.That(actor.Predict(parity.cases[0].observation),Is.EqualTo(parity.cases[0].action),"Weights must be defensively copied");
            foreach(var field in new[]{"schema","observationVersion","actionVersion","rewardVersion","inputSize","hiddenSize","actionCount"})
            {
                var invalid=JsonUtility.FromJson<FullMatchModelData>(asset.text);typeof(FullMatchModelData).GetField(field).SetValue(invalid,-1);
                Assert.Throws<ArgumentException>(()=>new FullMatchActor(invalid));
            }
            Assert.Throws<ArgumentException>(()=>actor.Predict(new float[31]));
            var bad=new float[32];bad[4]=float.NaN;Assert.Throws<ArgumentException>(()=>actor.Predict(bad));
            bad[4]=float.PositiveInfinity;Assert.Throws<ArgumentException>(()=>FullMatchActor.Normalize(bad));
            bad[4]=3;Assert.Throws<ArgumentException>(()=>actor.Predict(bad));
        }
    }
}
