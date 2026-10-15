using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class TennisEffectSceneTests
    {
        private static IEnumerator Until(Func<bool> condition,float seconds=25)
        {double end=Time.realtimeSinceStartupAsDouble+seconds;while(!condition()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Assert.That(condition(),Is.True);}
        [UnityTest]
        public IEnumerator ConfirmedContactsChargeAndSkillProduceEffectsButRejectedInputDoesNot()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();var fx=c.Effects;c.AutoServe=c.DemoReturns=false;
            c.VirtualSwing(CourtSide.Near,StrokeKind.Forehand);Assert.That(fx.PresentedByKind[1],Is.Zero);
            c.Serve();Assert.That(fx.PresentedByKind[0],Is.EqualTo(1));
            yield return Until(()=>c.CanReturn(CourtSide.Far));Assert.That(fx.PresentedByKind[2],Is.GreaterThan(0));
            var command=c.CreateVirtualInput(CourtSide.Far,StrokeKind.Forehand);var contact=c.Ball.transform.position;
            Assert.That(c.SubmitInput(command),Is.EqualTo(SwingResult.Hit));Assert.That(fx.PresentedByKind[1],Is.EqualTo(1));
            var records=fx.Capture();Assert.That(Vector3.Distance(GamePayload.Unpack(records[records.Length-1].position),contact),Is.LessThan(.001f));
            Assert.That(c.SubmitInput(command),Is.EqualTo(SwingResult.Duplicate));Assert.That(fx.PresentedByKind[1],Is.EqualTo(1));
            c.AutoServe=c.DemoReturns=true;c.DemoReturnLimit=10;
            yield return Until(()=>c.Characters.Player(CourtSide.Near).Meter.Stock>0||c.Characters.Player(CourtSide.Far).Meter.Stock>0);
            Assert.That(fx.PresentedByKind[3],Is.GreaterThan(0));
            var side=c.Characters.Player(CourtSide.Near).Meter.Stock>0?CourtSide.Near:CourtSide.Far;
            c.InputAdapter.Press(PlayerAction.Skill,side==CourtSide.Far);Assert.That(fx.PresentedByKind[4],Is.EqualTo(1));
            c.InputAdapter.Press(PlayerAction.Skill,side==CourtSide.Far);Assert.That(fx.PresentedByKind[4],Is.EqualTo(1));
            float time=fx.Clock;c.IsPaused=true;ulong ticks=c.Ball.PhysicsTicks;long points=c.Match.Score.TotalPoints;
            yield return new WaitForSeconds(.15f);Assert.That(fx.Clock,Is.EqualTo(time));Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));Assert.That(c.Match.Score.TotalPoints,Is.EqualTo(points));
            c.IsPaused=false;yield return new WaitForSeconds(.15f);Assert.That(fx.Clock,Is.GreaterThan(time));Assert.That(fx.DuplicateErrors,Is.Zero);
            c.ResetMatch();Assert.That(fx.Capture(),Is.Empty);Assert.That(fx.ActiveEffects,Is.Zero);
        }
        [UnityTest]
        public IEnumerator SettingsChooseDensityAndRemoteAuthorityCannotGenerateAnEffect()
        {
            int old=PlayerPrefs.GetInt("DigitalTennis.EffectQuality",1);
            try
            {
                yield return SceneManager.LoadSceneAsync("FrontEnd");yield return Until(()=>FrontEndController.Active?.Ready==true);
                var f=FrontEndController.Active;if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.View.Invoke("continue");f.View.Invoke("menu.options");
                Assert.That(f.View.Invoke("effects.quality.0"),Is.True);Assert.That(f.Match.Effects.Quality,Is.EqualTo(EffectQuality.Low));
                Assert.That(f.View.Invoke("effects.quality.2"),Is.True);Assert.That(f.Match.Effects.Quality,Is.EqualTo(EffectQuality.Rich));
                Assert.That(f.View.Invoke("effects.reload"),Is.True);Assert.That(f.Match.Effects.Store.Status,Does.Contain("완료"));
                uint sequence=f.Match.Effects.Sequence;f.Match.Ball.IsAuthoritative=false;f.Match.Effects.Record(TennisEffectKind.Hit,CourtSide.Near,Vector3.zero,Vector3.up,StrokeKind.Forehand);
                Assert.That(f.Match.Effects.Sequence,Is.EqualTo(sequence));Assert.That(f.Match.Match.Score.TotalPoints,Is.Zero);
            }
            finally {PlayerPrefs.SetInt("DigitalTennis.EffectQuality",old);PlayerPrefs.Save();}
        }
    }
}
