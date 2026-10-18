using System;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class LearnedAiTests
    {
        [Test]
        public void BundledPpoActorLoadsAndRejectsCorruptOrOversizedReplacement()
        {
            var asset=Resources.Load<TextAsset>("DigitalTennisAI/placement-ppo");
            Assert.That(asset,Is.Not.Null,"Pinned AI package must ship its trained actor.");
            var actor=TennisLearnedPolicy.Parse(asset.text);
            Assert.That(actor.Predict(new float[LearnedShotSelector.ObservationCount]),Is.InRange(0,5));
            Assert.Throws<ArgumentException>(()=>TennisLearnedPolicy.Parse("{}"));
            Assert.Throws<ArgumentException>(()=>TennisLearnedPolicy.Parse(new string(' ',256*1024+1)));
        }
    }
}
