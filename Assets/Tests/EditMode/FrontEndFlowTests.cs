using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class FrontEndFlowTests
    {
        private static FrontEndModel Menu() {var m=new FrontEndModel();m.Confirm();m.Confirm();return m;}
        [Test]
        public void OfflineScenarioRequiresPlayerOpponentCourtAndVersusBeforePlay()
        {
            var m=Menu();Assert.That(m.StartPlay(),Is.False);Assert.That(m.ChooseMode(GameMode.SinglePlayer),Is.True);
            m.Pick(11);m.Confirm();m.Pick(4);m.Confirm();m.Pick(13);m.Confirm();
            Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Versus));Assert.That(m.Player,Is.EqualTo(11));Assert.That(m.Opponent,Is.EqualTo(4));Assert.That(m.Court,Is.EqualTo(13));
            Assert.That(m.Confirm(),Is.False);m.StartPlay();m.Results();Assert.That(m.Rematch(),Is.True);
            Assert.That(m.Player,Is.EqualTo(11));Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Versus));
        }
        [Test]
        public void BackAndCancelledSelectionsPreservePreviouslyConfirmedChoices()
        {
            var m=Menu();m.ChooseMode(GameMode.SinglePlayer);m.Pick(3);m.Confirm();m.Pick(7);m.Back();
            Assert.That(m.Focus,Is.EqualTo(3));Assert.That(m.Player,Is.EqualTo(3));Assert.That(m.Pick(99),Is.False);
            m.Move(-4);Assert.That(m.Focus,Is.InRange(0,14));m.Back();m.Options();m.Controls();m.Back();Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Options));m.Back();Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Menu));
        }
        [Test]
        public void OnlineUsesLobbyAndAuthorityProvidedPairAndDisallowsUncoordinatedRematch()
        {
            var m=Menu();m.ChooseMode(GameMode.Online);m.Pick(8);m.Confirm();Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Lobby));
            m.EditLobbyCourt();m.Pick(13);m.Confirm();Assert.That(m.Stage,Is.EqualTo(FrontEndStage.Lobby));
            Assert.That(m.OnlineVersus(0,8,13),Is.True);m.StartPlay();m.Results();Assert.That(m.Rematch(),Is.False);
            Assert.That(m.Opponent,Is.EqualTo(8));Assert.That(m.Court,Is.EqualTo(13));
        }
        [Test]
        public void AllFifteenCityAnchorsAreUniqueAndOnTheVisiblePhysicalWorldMap()
        {
            var positions=new System.Collections.Generic.HashSet<Vector2>();
            for(int i=0;i<15;i++) {var p=WorldRoster.Position(i);Assert.That(p.x,Is.InRange(0,1));Assert.That(p.y,Is.InRange(0,1));Assert.That(positions.Add(p),Is.True);}
            Assert.That(WorldRoster.Position(0).x,Is.GreaterThan(WorldRoster.Position(3).x));
            Assert.That(WorldRoster.Position(14).y,Is.LessThan(WorldRoster.Position(0).y));
        }
    }
}
