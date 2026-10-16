using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
 public sealed class OpeningSequenceTests
 {
  [Test]public void EditHasContinuousCoverageAndFiniteSafeCamerasAndBallPath()
  {
   var edit=JsonUtility.FromJson<OpeningEdit>(Resources.Load<TextAsset>("FrontEnd/opening-edit").text);float end=0;var ids=new System.Collections.Generic.HashSet<string>();
   foreach(var s in edit.shots){Assert.That(s.start,Is.EqualTo(end));Assert.That(s.end,Is.GreaterThan(s.start));Assert.That(ids.Add(s.id),Is.True);Assert.That(s.from.y,Is.GreaterThan(.1));Assert.That(s.to.y,Is.GreaterThan(.1));Assert.That(s.fovFrom,Is.InRange(25,75));Assert.That((s.lookFrom-s.from).magnitude,Is.GreaterThan(.1));end=s.end;}
   Assert.That(end,Is.EqualTo(OpeningSequence.Duration));Assert.That(ids.Count,Is.GreaterThanOrEqualTo(8));
   for(float t=0;t<OpeningSequence.Duration;t+=.01f){var p=OpeningSequence.BallAt(t);Assert.That(BallSimulation.IsFinite(p),Is.True);Assert.That(p.y,Is.GreaterThanOrEqualTo(.09));}
   foreach(float t in new[]{3.2f,4.3f,5.55f,7.35f,8.5f,9.1f})Assert.That(Vector3.Distance(OpeningSequence.BallAt(t-.0001f),OpeningSequence.BallAt(t+.0001f)),Is.LessThan(.02));
  }
  [Test]public void ReplayAndSkipStayInMenusAndCannotStartAMatch()
  {var m=new FrontEndModel();m.Confirm();Assert.That(m.ReplayOpening(),Is.True);Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Logo));Assert.That(m.StartPlay(),Is.False);m.Confirm();m.Confirm();m.ChooseMode(GameMode.Online);Assert.That(m.ReplayOpening(),Is.False);}
 }
}
