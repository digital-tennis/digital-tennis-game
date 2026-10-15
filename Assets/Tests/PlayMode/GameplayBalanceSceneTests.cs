using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace DigitalTennis.Tests
{
    public sealed class GameplayBalanceSceneTests
    {
        private static string Change(string text,string key,string value) {var rows=text.Split('\n');for(int i=1;i<rows.Length;i++)if(rows[i].StartsWith(key+",")){var cells=rows[i].Split(',');cells[1]=value;rows[i]=string.Join(",",cells);}return string.Join("\n",rows);}
        [UnityTest]
        public IEnumerator F5AppliesBundleAtServeBoundaryRejectsRallyChangesAndContinuesNextServe()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt");yield return null;
            var c=Object.FindFirstObjectByType<TennisMatchController>();c.AutoServe=c.DemoReturns=false;
            string original=File.ReadAllText(c.Balance.Path);var revision=c.Balance.Revision;
            try {
                File.WriteAllText(c.Balance.Path,Change(Change(original,"move.speed","6"),"physics.bounce","0.6"));
                c.InputAdapter.Press(PlayerAction.ReloadBalance);
                Assert.That(c.Balance.Revision,Is.GreaterThan(revision));Assert.That(c.Tuning["move.speed"],Is.EqualTo(6));Assert.That(c.Ball.Simulation.BounceRestitution,Is.EqualTo(.6f));
                Assert.That(c.Opponent.Policy.Settings.ReactionSeconds,Is.EqualTo(c.Tuning["ai.medium.reaction"]));
                c.Serve();var applied=c.Balance.Revision;
                File.WriteAllText(c.Balance.Path,Change(original,"move.speed","7"));c.InputAdapter.Press(PlayerAction.ReloadBalance);
                Assert.That(c.Balance.Revision,Is.EqualTo(applied));Assert.That(c.Tuning["move.speed"],Is.EqualTo(6));
                double end=Time.realtimeSinceStartupAsDouble+20;
                while((c.Match.Score.TotalPoints==0 || !c.ReadyToServe)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
                Assert.That(c.Match.Score.TotalPoints,Is.GreaterThan(0));Assert.That(c.ReadyToServe,Is.True);
                c.InputAdapter.Press(PlayerAction.ReloadBalance);Assert.That(c.Tuning["move.speed"],Is.EqualTo(7));Assert.That(c.Serve(),Is.True);
            }finally{File.WriteAllText(c.Balance.Path,original);}
        }
    }
}
