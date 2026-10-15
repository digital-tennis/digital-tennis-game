using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class FrontEndSceneTests
    {
        private static IEnumerator Until(Func<bool> predicate,float seconds=8)
        {double end=Time.realtimeSinceStartupAsDouble+seconds;while(!predicate()&&Time.realtimeSinceStartupAsDouble<end)yield return null;Assert.That(predicate(),Is.True,"Front end transition timed out.");}
        private static void Click(FrontEndController f,string id) => Assert.That(f.View.Invoke(id),Is.True,"Missing enabled UI control "+id);
        [UnityTest]
        public IEnumerator WorldMapButtonsConfigureRealMatchAndMenusGateAndPauseRacketInput()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");yield return Until(()=>FrontEndController.Active?.Ready==true);
            var f=FrontEndController.Active;var c=f.Match;
            ulong ticks=c.Ball.PhysicsTicks;c.InputAdapter.Press(PlayerAction.Serve);yield return new WaitForSeconds(.15f);
            Assert.That(c.Match.CurrentShot.ShotId,Is.Zero);Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));
            if(f.Model.Stage==FrontEndStage.Logo)c.InputAdapter.Press(PlayerAction.MenuConfirm);Click(f,"continue");Click(f,"menu.single");
            for(int i=0;i<15;i++) {Click(f,"map."+CharacterCatalog.Ids[i]);Assert.That(f.Model.Focus,Is.EqualTo(i));Assert.That(f.View.Texts["name"].text,Is.EqualTo(CharacterCatalog.Get(i).localName));}
            Click(f,"country.NLD");Click(f,"confirm");Click(f,"country.RUS");Click(f,"difficulty.2");Click(f,"confirm");Click(f,"country.KOR");Click(f,"confirm");
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Versus));c.InputAdapter.Press(PlayerAction.Serve);Assert.That(c.Match.CurrentShot.ShotId,Is.Zero);
            yield return Until(()=>f.Model.Stage==FrontEndStage.Playing);
            Assert.That(c.Characters.Player(CourtSide.Near).Index,Is.EqualTo(11));Assert.That(c.Characters.Player(CourtSide.Far).Index,Is.EqualTo(4));Assert.That(c.Characters.CourtIndex,Is.Zero);
            Assert.That(c.AiDifficulty,Is.EqualTo(DigitalTennis.AI.Difficulty.Hard));Assert.That(c.AutoServe,Is.False);Assert.That(c.DemoReturns,Is.False);
            c.InputAdapter.Press(PlayerAction.Serve);Assert.That(c.Match.CurrentShot.ShotId,Is.GreaterThan(0));
            c.InputAdapter.InputEnabled=false;c.InputAdapter.Press(PlayerAction.MenuBack);
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Pause),"Menu escape must remain available while network gameplay input is blocked.");
            c.InputAdapter.InputEnabled=true;ticks=c.Ball.PhysicsTicks;yield return new WaitForSeconds(.2f);Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));
            Click(f,"confirm");Assert.That(c.IsPaused,Is.False);Click(f,"pause");Click(f,"menu");
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Menu));Assert.That(c.InputAdapter.GameplayEnabled,Is.False);
        }
        [UnityTest]
        public IEnumerator OptionsBackAndPracticeStartHaveNoAutomaticServeOrAi()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");yield return Until(()=>FrontEndController.Active?.Ready==true);
            var f=FrontEndController.Active;if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();Click(f,"continue");
            Click(f,"menu.options");Click(f,"controls");Click(f,"back");Click(f,"back");Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Menu));
            Click(f,"menu.practice");Click(f,"confirm");Click(f,"confirm");Click(f,"confirm");
            yield return Until(()=>f.Model.Stage==FrontEndStage.Playing);yield return new WaitForSeconds(1);
            Assert.That(f.Match.Mode,Is.EqualTo(GameMode.Practice));Assert.That(f.Match.Opponent,Is.Null);Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero);
            Assert.That(f.Match.AutoServe,Is.False);Assert.That(f.Match.DemoReturns,Is.False);
        }
    }
}
