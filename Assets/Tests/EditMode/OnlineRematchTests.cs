using NUnit.Framework;
namespace DigitalTennis.Tests
{
    public sealed class OnlineRematchTests
    {
        [Test] public void RematchNeedsBothPlayersAndCanBeConsumedOnlyOnce()
        {
            var consent=new OnlineRematchConsent();consent.Reset("finished");
            Assert.That(consent.Vote(CourtSide.Near,"finished",true,true),Is.True);Assert.That(consent.Consume(),Is.False);
            Assert.That(consent.Vote(CourtSide.Far,"finished",true,true),Is.True);Assert.That(consent.Consume(),Is.True);Assert.That(consent.Consume(),Is.False);
            consent.Reset("new-match");Assert.That(consent.Vote(CourtSide.Far,"finished",true,true),Is.False);Assert.That(consent.Consume(),Is.False);
        }
        [Test] public void CancelDisconnectAndAnUnfinishedMatchCannotGrantARematch()
        {
            var consent=new OnlineRematchConsent();consent.Reset("match");
            Assert.That(consent.Vote(CourtSide.Near,"match",true,false),Is.False);
            consent.Vote(CourtSide.Near,"match",true,true);consent.Vote(CourtSide.Far,"match",true,true);
            consent.Vote(CourtSide.Far,"match",false,true);Assert.That(consent.Consume(),Is.False);
            consent.Vote(CourtSide.Far,"match",true,true);consent.ClearVotes();Assert.That(consent.Consume(),Is.False);
        }
    }
}
