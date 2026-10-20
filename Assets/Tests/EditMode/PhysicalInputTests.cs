using NUnit.Framework;
namespace DigitalTennis.Tests
{
    public sealed class PhysicalInputTests
    {
        [TestCase(MatchPhase.WaitServe,true,PlayerAction.Serve)]
        [TestCase(MatchPhase.Rally,true,PlayerAction.Smash)]
        [TestCase(MatchPhase.Rally,false,PlayerAction.Smash)]
        public void SameOverheadPatternIsInterpretedByTheGame(MatchPhase phase,bool server,PlayerAction expected)
        {Assert.That(PhysicalTennisInputResolver.TryResolve(PlayerAction.MotionOverheadDown,phase,server,0,0,out var result),Is.True);Assert.That(result,Is.EqualTo(expected));}
        [TestCase(MatchPhase.WaitServe)] [TestCase(MatchPhase.PointEnd)] [TestCase(MatchPhase.MatchEnd)]
        public void OtherPlayersServeOrDeadPointCannotBecomeASmash(MatchPhase phase)
        {Assert.That(PhysicalTennisInputResolver.TryResolve(PlayerAction.MotionOverheadDown,phase,false,0,0,out _),Is.False);}
        [TestCase(0,0f,PlayerAction.Volley)] [TestCase(1,0f,PlayerAction.Forehand)] [TestCase(1,-.2f,PlayerAction.Backhand)]
        public void TheGameUsesBounceStateForShortRotation(int bounce,float aim,PlayerAction expected)
        {Assert.That(PhysicalTennisInputResolver.TryResolve(PlayerAction.MotionShortRotation,MatchPhase.Rally,false,bounce,aim,out var result),Is.True);Assert.That(result,Is.EqualTo(expected));}
        [Test] public void InvalidPhysicalPatternIsNotDefaultServe()
        {Assert.That(new ControllerEvent((ControllerMotion)255,1,1,1,.5f,0,0).Allowed,Is.False);Assert.That(PhysicalTennisInputResolver.TryPhysicalAction((ControllerMotion)255,out _),Is.False);}
        [Test] public void AddedPhysicalActionsPreserveLegacyStrokeAndMenuIds()
        {Assert.That((int)PlayerAction.Forehand,Is.EqualTo(1));Assert.That((int)PlayerAction.Backhand,Is.EqualTo(2));Assert.That((int)PlayerAction.MenuDown,Is.EqualTo(21));}
    }
}
