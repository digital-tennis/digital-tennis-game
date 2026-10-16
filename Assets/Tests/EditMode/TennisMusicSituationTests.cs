using NUnit.Framework;
namespace DigitalTennis.Tests
{
 public sealed class TennisMusicSituationTests
 {
  private static MatchScoreState State(int a,int b,int sets=0,bool tie=false)=>new MatchScoreState{rules=new[]{3,6,6,7},games=new[]{a,b},sets=new[]{sets,0},gameServer="host",tieBreak=tie};
  [Test]public void DeuceBreakPointAndMatchPointUseTennisThresholds()
  {
   Assert.That(TennisMusicSituation.Classify(State(0,0),new[]{3,3}),Is.EqualTo(TennisMusicMood.Deuce));
   Assert.That(TennisMusicSituation.Classify(State(0,0),new[]{2,3}),Is.EqualTo(TennisMusicMood.Pressure));
   Assert.That(TennisMusicSituation.MatchPoint(State(5,4,1),new[]{3,2}),Is.True);
   Assert.That(TennisMusicSituation.MatchPoint(State(5,5,1),new[]{3,2}),Is.False);
   Assert.That(TennisMusicSituation.MatchPoint(State(6,6,1,true),new[]{6,5}),Is.True);
   Assert.That(TennisMusicSituation.MatchPoint(State(6,6,1,true),new[]{6,6}),Is.False);
   var done=State(6,4,1);done.gameComplete=true;Assert.That(TennisMusicSituation.MatchPoint(done,new[]{4,2}),Is.False);
  }
 }
}
