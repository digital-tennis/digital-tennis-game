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
            Assert.That(r.Push(GestureToken.Left,1,1),Is.False);
            Assert.That(r.Push(GestureToken.Up,1,1.1),Is.False);
            Assert.That(r.Push(GestureToken.Right,2,1.2),Is.False);
            r.Clear();r.Push(GestureToken.Left,1,2);r.Push(GestureToken.Up,2,3);
            Assert.That(r.Push(GestureToken.Right,3,3.1),Is.False);
            Assert.That(r.Push(GestureToken.Left,4,double.NaN),Is.False);
        }
        [Test] public void SourceSwitchDropsQueuedGesturesAndKeepsKeyboardActionsInOneIdNamespace()
        {
            int selected=0;var source=new GestureSkillInputSource(new BufferedPlayerInputSource("fixture"),()=>selected,Catalog);
            source.PushToken(GestureToken.Left,1);source.PushToken(GestureToken.Up,2);source.PushToken(GestureToken.Right,3);
            ((IPlayerInputSource)source).Clear();source.Poll(Time.realtimeSinceStartupAsDouble,.5f,0,0);
            Assert.That(source.TryRead(out _),Is.False);
            source.PushToken(GestureToken.Left,4);source.PushToken(GestureToken.Up,5);source.PushToken(GestureToken.Right,6);
            source.Poll(Time.realtimeSinceStartupAsDouble,.5f,0,0);
            Assert.That(source.TryRead(out var skill),Is.True);Assert.That(skill.Action,Is.EqualTo(PlayerAction.Skill));Assert.That(source.TryRead(out _),Is.False);
        }
    }
}
