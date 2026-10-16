using System;
using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator KeyboardSourceNavigatesRosterAndConfirmsWithoutClickingButtons()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");
            yield return Until(()=>FrontEndController.Active?.Ready==true);
            var f=FrontEndController.Active;
            var keys=new HashSet<KeyCode>();
            f.Match.InputAdapter.UseSource(new KeyboardPlayerInputSource(key=>keys.Remove(key)));
            if(f.Model.Stage==FrontEndStage.Logo) {keys.Add(KeyCode.Return);yield return null;yield return null;}
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Title));
            keys.Add(KeyCode.Return);yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Menu));
            keys.Add(KeyCode.DownArrow);yield return null;yield return null;
            Assert.That(f.MenuIndex,Is.EqualTo(1));
            keys.Add(KeyCode.Return);yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Player));
            var visited=new HashSet<int>{f.Model.Focus};
            for(int i=0;i<15;i++)
            {
                keys.Add(KeyCode.RightArrow);yield return null;yield return null;
                visited.Add(f.Model.Focus);
                Assert.That(f.View.Texts["name"].text,Is.EqualTo(CharacterCatalog.Get(f.Model.Focus).localName));
            }
            Assert.That(visited.Count,Is.EqualTo(15));
            keys.Add(KeyCode.LeftArrow);yield return null;yield return null;
            Assert.That(f.Model.Focus,Is.EqualTo(14));
            keys.Add(KeyCode.DownArrow);yield return null;yield return null;
            Assert.That(f.Model.Focus,Is.EqualTo(7));
            keys.Add(KeyCode.UpArrow);yield return null;yield return null;
            Assert.That(f.Model.Focus,Is.EqualTo(14));
            keys.Add(KeyCode.KeypadEnter);yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Lobby));
            Assert.That(f.Model.Player,Is.EqualTo(14));
            keys.Add(KeyCode.Escape);yield return null;yield return null;
            Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Player));
            Assert.That(f.Model.Focus,Is.EqualTo(14));
            Assert.That(f.View.ButtonActivations,Is.Zero,"This regression must use the keyboard source, not UI callbacks.");
            Assert.That(f.Match.Match.CurrentShot.ShotId,Is.Zero,"Menu keys must not start a serve.");
        }
        [UnityTest]
        public IEnumerator KeyboardGameplayShowsMissesAndHitsAndKeepsTheRallyRules()
        {
            yield return SceneManager.LoadSceneAsync("FrontEnd");yield return Until(()=>FrontEndController.Active?.Ready==true);
            var f=FrontEndController.Active;if(f.Model.Stage==FrontEndStage.Logo)f.Confirm();f.Confirm();
            f.MenuChoice(2);f.Confirm();f.Confirm();f.Confirm();
            yield return Until(()=>f.Model.Stage==FrontEndStage.Playing);
            var c=f.Match;var keys=new HashSet<KeyCode>();
            c.InputAdapter.UseSource(new KeyboardPlayerInputSource(key=>keys.Remove(key)));
            keys.Add(KeyCode.Space);yield return null;yield return null;
            Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.InFlight));
            uint serve=c.Match.CurrentShot.ShotId,swing=c.NearAvatar.SwingId;
            yield return new WaitForSeconds(.4f);
            keys.Add(KeyCode.W);yield return null;yield return null;
            Assert.That(c.InputAdapter.LastAction,Is.EqualTo(PlayerAction.Backhand));
            Assert.That(c.InputAdapter.LastSwingResult,Is.EqualTo(SwingResult.WrongPhase));
            Assert.That(c.NearAvatar.SwingId,Is.GreaterThan(swing));
            Assert.That(c.NearAvatar.LastStroke,Is.EqualTo(StrokeKind.Backhand));
            Assert.That(c.NearAvatar.IsSwinging,Is.True);
            Assert.That(c.Match.CurrentShot.ShotId,Is.EqualTo(serve));
            Assert.That(c.Hits,Is.Zero);Assert.That(c.Match.Score.TotalPoints,Is.Zero);
            f.View.UpdateLive();Assert.That(f.View.Texts["input.feedback"].text,Does.Contain("백핸드 입력됨").And.Contain("헛스윙"));
            var receivers=new[]{CourtSide.Far,CourtSide.Near,CourtSide.Far,CourtSide.Near};
            var strokes=new[]{KeyCode.E,KeyCode.Q,KeyCode.D,KeyCode.W};
            for(int i=0;i<4;i++)
            {
                int index=i;
                yield return Until(()=>c.CanReturn(receivers[index])&&c.Ball.Simulation.BounceCount==1&&c.Ball.Simulation.Position.y>=.95f);
                if(receivers[i]==CourtSide.Near)
                {f.View.UpdateLive();Assert.That(f.View.Texts["input.hint"].text,Does.Contain("지금 Q / W"));}
                keys.Add(strokes[i]);yield return null;yield return null;
                Assert.That(c.InputAdapter.LastSwingResult,Is.EqualTo(SwingResult.Hit));
                Assert.That(c.Hits,Is.EqualTo(i+1));
                f.View.UpdateLive();Assert.That(f.View.Texts["input.feedback"].text,Does.Contain("타격 성공"));
            }
            Assert.That(c.NearHits,Is.EqualTo(2));Assert.That(c.FarHits,Is.EqualTo(2));
            Assert.That(c.Match.Score.TotalPoints,Is.Zero);Assert.That(c.InputAdapter.LastActionSentOnline,Is.False);
            Assert.That(f.View.ButtonActivations,Is.Zero,"Gameplay keys must pass through the keyboard adapter.");
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
            float previous=Time.timeScale;Time.timeScale=4;
            try
            {
                for(int i=1;i<=4;i++)
                {
                    yield return Until(()=>f.Match.ReadyToServe);f.Match.InputAdapter.Press(PlayerAction.Serve);
                    int points=i;yield return Until(()=>f.Match.Match.Progress.TotalPoints==points);
                }
                Assert.That(f.Model.Stage,Is.EqualTo(FrontEndStage.Playing),"One game must not open match results.");
                f.View.UpdateLive();Assert.That(f.View.Texts["sets"].text,Does.Contain("세트 0 : 0").And.Contain("게임 1 : 0"));
                Assert.That(f.View.Texts["phase"].text,Does.Contain("게임 종료"));
                yield return Until(()=>f.Match.ReadyToServe);f.View.UpdateLive();
                Assert.That(f.Match.Match.Server,Is.EqualTo(CourtSide.Far));Assert.That(f.View.Buttons["serve"].gameObject.activeSelf,Is.False);
                Assert.That(f.View.Texts["input.hint"].text,Does.Contain("상대의 서브"));
            }finally{Time.timeScale=previous;}
        }
    }
}
