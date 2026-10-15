using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class PlayerInputSceneTests
    {
        [UnityTest]
        public IEnumerator KeyboardAndReplaceableSourceUseTheSameRacketGate()
        {
            yield return SceneManager.LoadSceneAsync("MatchCourt");yield return null;
            var c=Object.FindFirstObjectByType<TennisMatchController>();
            c.Configure(GameMode.Practice);c.AutoServe=false;c.DemoReturns=false;
            c.GetComponent<OnlineTennisSession>().enabled=false; // The fixture owns typing focus instead of OnGUI.
            var keys=new HashSet<KeyCode>();c.InputAdapter.UseSource(new KeyboardPlayerInputSource(key=>keys.Remove(key)));
            c.InputAdapter.Typing=true; keys.Add(KeyCode.Space);yield return null;yield return null;
            Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.WaitServe), "Typing a room address must not serve.");
            keys.Clear(); c.InputAdapter.Press(PlayerAction.Serve); // An explicit screen button still works.
            c.InputAdapter.Typing=false;yield return null;yield return null;
            Assert.That(c.Match.Phase,Is.EqualTo(MatchPhase.InFlight));
            float deadline=Time.realtimeSinceStartup+6;
            while((!c.CanReturn(CourtSide.Far)||c.Ball.Simulation.Position.y<.95f)&&Time.realtimeSinceStartup<deadline)yield return null;
            keys.Add(KeyCode.E);yield return null;yield return null;
            Assert.That(c.FarHits,Is.EqualTo(1));
            var decoded=new BufferedPlayerInputSource("Decoded board test fixture");
            c.InputAdapter.UseSource(decoded);
            deadline=Time.realtimeSinceStartup+6;
            while((!c.CanReturn(CourtSide.Near)||c.Ball.Simulation.Position.y<.95f)&&Time.realtimeSinceStartup<deadline)yield return null;
            var sample=new PlayerInputSample(PlayerAction.Backhand,1,Time.realtimeSinceStartupAsDouble);
            decoded.Enqueue(sample);yield return null;yield return null;
            Assert.That(c.NearHits,Is.EqualTo(1));Assert.That(c.BackhandHits,Is.EqualTo(1));
            decoded.Enqueue(sample);yield return null;yield return null;
            Assert.That(c.NearHits,Is.EqualTo(1));Assert.That(c.InputAdapter.Rejected,Is.GreaterThan(0));
            c.InputAdapter.UseKeyboard();Assert.That(c.InputAdapter.SourceName,Is.EqualTo("Keyboard"));
        }
        [UnityTest]
        public IEnumerator OnlineGuestAndPausedHostCannotAdvancePhysicsOrLocalRules()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");yield return null;
            var c=Object.FindFirstObjectByType<TennisMatchController>();c.EnterOnline(false);
            Assert.That(c.Opponent,Is.Null);Assert.That(c.Serve(),Is.False);
            ulong ticks=c.Ball.PhysicsTicks;var phase=c.Match.Phase;
            c.InputAdapter.Press(PlayerAction.Serve);yield return new WaitForSeconds(.2f);
            Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));Assert.That(c.Match.Phase,Is.EqualTo(phase));
            c.PresentOnline(new Vector3(1,2,3),new Vector3(0,.85f,-8),new Vector3(0,.85f,8));
            Assert.That(c.Ball.transform.position,Is.EqualTo(new Vector3(1,2,3)));
            c.EnterOnline(true);c.IsPaused=false;Assert.That(c.Serve(),Is.True);
            yield return new WaitForSeconds(.1f);c.IsPaused=true;
            var position=c.Ball.Simulation.Position;ticks=c.Ball.PhysicsTicks;
            yield return new WaitForSeconds(.2f);
            Assert.That(c.Ball.Simulation.Position,Is.EqualTo(position));Assert.That(c.Ball.PhysicsTicks,Is.EqualTo(ticks));
        }
    }
}
