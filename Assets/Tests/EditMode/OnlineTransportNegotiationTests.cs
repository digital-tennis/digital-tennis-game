using System;
using NUnit.Framework;

namespace DigitalTennis.Tests
{
    public sealed class OnlineTransportNegotiationTests
    {
        [Test]public void BothActualChannelsMustBeReadyBeforeDirectProposal()
        {
            var n=new OnlineTransportNegotiation();n.Begin(true,0,10,false);Assert.That(n.Decide(10),Is.Null);n.LocalReady=true;Assert.That(n.Decide(11),Is.Null);n.PeerReady=true;Assert.That(n.Decide(12),Is.EqualTo("p2p"));Assert.That(n.Decide(13),Is.Null);Assert.That(n.Commit("p2p",1),Is.True);Assert.That(n.Commit("p2p",1),Is.False);
        }
        [Test]public void EightSecondAttemptOrExplicitRelayCannotBecomeLateDirectSuccess()
        {
            var n=new OnlineTransportNegotiation();n.Begin(true,4,0,false);Assert.That(n.Decide(7.999),Is.Null);Assert.That(n.Decide(8),Is.EqualTo("relay"));n.LocalReady=n.PeerReady=true;Assert.That(n.Decide(8.1),Is.Null);Assert.That(n.Commit("p2p",5),Is.False);Assert.That(n.Commit("relay",5),Is.True);
            n.Begin(true,5,20,true);n.LocalReady=n.PeerReady=true;Assert.That(n.Decide(20),Is.EqualTo("relay"));Assert.That(n.Commit("p2p",6),Is.False);
        }
        [Test]public void GuestCannotChooseAuthorityOrAcknowledgeUnreadyOrOldTransport()
        {
            var n=new OnlineTransportNegotiation();n.Begin(false,7,0,false);Assert.That(n.Decide(9),Is.Null);Assert.That(n.CanAcknowledge("p2p",8),Is.False);n.LocalReady=n.PeerReady=true;Assert.That(n.CanAcknowledge("p2p",8),Is.True);Assert.That(n.Commit("p2p",7),Is.False);Assert.That(n.Commit("anything",8),Is.False);Assert.That(n.Commit("p2p",8),Is.True);
        }
        [Test]public void ReconnectClearsReadinessAndRequiresANewEpoch()
        {
            var n=new OnlineTransportNegotiation();n.Begin(true,0,0,false);n.LocalReady=n.PeerReady=true;n.Decide(1);Assert.That(n.Commit("p2p",1),Is.True);n.Begin(true,1,2,false);Assert.That(n.LocalReady||n.PeerReady||n.Committed,Is.False);Assert.That(n.Commit("p2p",1),Is.False);Assert.That(n.Decide(10),Is.EqualTo("relay"));Assert.Throws<ArgumentException>(()=>n.Begin(true,uint.MaxValue,0,false));Assert.Throws<ArgumentException>(()=>n.Begin(true,0,double.NaN,false));
        }
    }
}
