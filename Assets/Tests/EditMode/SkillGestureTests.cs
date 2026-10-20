using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class SkillGestureTests
    {
        private static System.Collections.Generic.Dictionary<string,SkillGesture> Catalog=>SkillGestureCatalog.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Balance","gestures.csv")));
        [Test] public void EveryPersonaHasADistinctCompleteGestureAndOnlyTheLastTokenFires()
        {
            foreach(var g in Catalog.Values)
            {
                var r=new SkillGestureRecognizer();r.Select(g);
                for(int i=0;i<g.Tokens.Length;i++)Assert.That(r.Push(g.Tokens[i],(uint)i+1,1+i*.15),Is.EqualTo(i==g.Tokens.Length-1),g.Id);
                for(int i=0;i<g.Tokens.Length;i++)Assert.That(r.Push(g.Tokens[i],(uint)i+10,2+i*.15),Is.False,"Cooldown "+g.Id);
                Assert.That(r.Completed,Is.EqualTo(1));
            }
        }
        [Test] public void DuplicatePacketsStaleTimeAndLongGapsDoNotGrantASkill()
        {
            var r=new SkillGestureRecognizer();var g=Catalog["KOR"];r.Select(g);
            Assert.That(r.Push(GestureToken.HorizontalNegative,1,1),Is.False);
            Assert.That(r.Push(GestureToken.ObliqueUp,1,1.1),Is.False);
            Assert.That(r.Push(GestureToken.HorizontalPositive,2,1.2),Is.False);
            r.Clear();r.Push(GestureToken.HorizontalNegative,1,2);r.Push(GestureToken.ObliqueUp,2,3);
            Assert.That(r.Push(GestureToken.HorizontalPositive,3,3.1),Is.False);
            Assert.That(r.Push(GestureToken.HorizontalNegative,4,double.NaN),Is.False);
        }
        [Test] public void SourceSwitchDropsQueuedGesturesAndKeepsKeyboardActionsInOneIdNamespace()
        {
            int selected=0;var source=new GestureSkillInputSource(new BufferedPlayerInputSource("fixture"),()=>selected,Catalog);
            source.PushToken(GestureToken.HorizontalNegative,1);source.PushToken(GestureToken.ObliqueUp,2);source.PushToken(GestureToken.HorizontalPositive,3);
            ((IPlayerInputSource)source).Clear();source.Poll(Time.realtimeSinceStartupAsDouble,.5f,0,0);
            Assert.That(source.TryRead(out _),Is.False);
            source.PushToken(GestureToken.HorizontalNegative,4);source.PushToken(GestureToken.ObliqueUp,5);source.PushToken(GestureToken.HorizontalPositive,6);
            source.Poll(Time.realtimeSinceStartupAsDouble,.5f,0,0);
            Assert.That(source.TryRead(out var skill),Is.True);Assert.That(skill.Action,Is.EqualTo(PlayerAction.Skill));Assert.That(source.TryRead(out _),Is.False);
        }

        [Test] public void PhysicalInputsUseEveryPersonaCsvAndOnlyCompletionBecomesSkill()
        {
            foreach(var pair in Catalog)
            {
                var raw=new BufferedPlayerInputSource("physical fixture");
                var source=new GestureSkillInputSource(raw,()=>Array.IndexOf(CharacterCatalog.Ids,pair.Key),Catalog);
                for(int n=0;n<pair.Value.Tokens.Length;n++)
                {
                    var action=(PlayerAction)Enum.Parse(typeof(PlayerAction),"Motion"+pair.Value.Tokens[n]);
                    raw.Enqueue(new PlayerInputSample(action,(uint)n+1,1+n*.1,.7f,isPhysicalMotion:true));
                }
                source.Poll(1.2,.5f,0,0);
                for(int n=0;n<3;n++){Assert.That(source.TryRead(out var sample),Is.True,pair.Key);Assert.That(sample.Action==PlayerAction.Skill,Is.EqualTo(n==2),pair.Key);}
                Assert.That(source.TryRead(out _),Is.False);
            }
        }
        [Test] public void PartialPatternsCannotCrossDisabledGameplayProfileChangesOrSourceClear()
        {
            bool enabled=true;int selected=0;var raw=new BufferedPlayerInputSource("physical fixture");
            var source=new GestureSkillInputSource(raw,()=>selected,Catalog,()=>enabled);
            void Send(PlayerAction a,uint id,double t){raw.Enqueue(new PlayerInputSample(a,id,t,.5f,isPhysicalMotion:true));source.Poll(t,.5f,0,0);while(source.TryRead(out var sample))Assert.That(sample.Action,Is.Not.EqualTo(PlayerAction.Skill));}
            Send(PlayerAction.MotionHorizontalNegative,1,1);enabled=false;source.Poll(1.1,.5f,0,0);enabled=true;
            Send(PlayerAction.MotionObliqueUp,2,1.2);Send(PlayerAction.MotionHorizontalPositive,3,1.3);
            Send(PlayerAction.MotionHorizontalNegative,4,2);selected=1;source.Poll(2.05,.5f,0,0);selected=0;
            Send(PlayerAction.MotionObliqueUp,5,2.1);Send(PlayerAction.MotionHorizontalPositive,6,2.2);
            Send(PlayerAction.MotionHorizontalNegative,7,3);source.Clear();Send(PlayerAction.MotionObliqueUp,8,3.1);Send(PlayerAction.MotionHorizontalPositive,9,3.2);
        }
        [Test] public void DuplicateStaleAndFuturePhysicalEventsCannotCompleteThePattern()
        {
            var raw=new BufferedPlayerInputSource("physical fixture");var source=new GestureSkillInputSource(raw,()=>0,Catalog);
            raw.Enqueue(new PlayerInputSample(PlayerAction.MotionHorizontalNegative,1,1,.5f,isPhysicalMotion:true));
            raw.Enqueue(new PlayerInputSample(PlayerAction.MotionObliqueUp,1,1.1,.5f,isPhysicalMotion:true));
            raw.Enqueue(new PlayerInputSample(PlayerAction.MotionHorizontalPositive,2,1.2,.5f,isPhysicalMotion:true));
            raw.Enqueue(new PlayerInputSample(PlayerAction.MotionHorizontalNegative,3,0,.5f,isPhysicalMotion:true));
            raw.Enqueue(new PlayerInputSample(PlayerAction.MotionObliqueUp,4,2,.5f,isPhysicalMotion:true));
            source.Poll(1.2,.5f,0,0);int count=0;while(source.TryRead(out var sample)){Assert.That(sample.Action,Is.Not.EqualTo(PlayerAction.Skill));count++;}Assert.That(count,Is.EqualTo(2));
        }
        [Test] public void InvalidCsvReloadPreservesLastValidCatalog()
        {
            string dir=System.IO.Path.Combine(Application.temporaryCachePath,"gesture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{string path=System.IO.Path.Combine(dir,"gestures.csv");var settings=new SkillGestureSettings(path,File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","gestures.csv")));var before=settings.Current;File.WriteAllText(path,"invalid");Assert.That(settings.Reload(),Is.False);Assert.That(settings.Current,Is.SameAs(before));}
            finally{Directory.Delete(dir,true);}
        }

        [Test] public void ButtonBindingsAndInjectedNeutralActionsAreNotImuGestureTokens()
        {
            var raw=new BufferedPlayerInputSource("bound buttons");var source=new GestureSkillInputSource(raw,()=>0,Catalog);
            uint id=1;foreach(var token in Catalog["KOR"].Tokens)
                raw.Enqueue(new PlayerInputSample((PlayerAction)Enum.Parse(typeof(PlayerAction),"Motion"+token),id++,1,.5f));
            source.Poll(1,.5f,0,0);int count=0;while(source.TryRead(out var sample)){Assert.That(sample.Action,Is.Not.EqualTo(PlayerAction.Skill));count++;}Assert.That(count,Is.EqualTo(3));
        }
    }
}
