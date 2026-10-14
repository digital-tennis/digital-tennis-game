using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class BallSimulationTests
    {
        private static readonly ShotContext Rally = new ShotContext(7, CourtSide.Near);

        [TestCase(4.115f, 5f, true)]
        [TestCase(4.116f, 5f, false)]
        [TestCase(-4.115f, 11.885f, true)]
        [TestCase(0f, 11.886f, false)]
        [TestCase(0f, -5f, false)]
        [TestCase(0f, 0f, true)]
        public void SinglesBoundaryUsesLandingCentreAndIncludesLines(float x, float z, bool expected)
        {
            Assert.That(CourtBounds.Contains(new Vector3(x, 0f, z), Rally), Is.EqualTo(expected));
        }

        [TestCase(1f, 6.4f, true)]
        [TestCase(1f, 6.401f, false)]
        [TestCase(-1f, 3f, false)]
        [TestCase(0f, 3f, true)]
        public void ServiceChecksTheTargetBox(float x, float z, bool expected)
        {
            var shot = new ShotContext(8, CourtSide.Near, LandingArea.ServiceBox, ServiceBoxSide.PositiveX);
            Assert.That(CourtBounds.Contains(new Vector3(x, 0f, z), shot), Is.EqualTo(expected));
        }

        [Test]
        public void FarHitterTargetsNearCourtAndNegativeServiceBox()
        {
            var shot = new ShotContext(8, CourtSide.Far, LandingArea.ServiceBox, ServiceBoxSide.NegativeX);
            Assert.That(CourtBounds.Contains(new Vector3(-1f, 0f, -6.4f), shot), Is.True);
            Assert.That(CourtBounds.Contains(new Vector3(-1f, 0f, 3f), shot), Is.False);
            Assert.That(CourtBounds.Contains(new Vector3(1f, 0f, -3f), shot), Is.False);
        }

        [Test]
        public void FlightFollowsGravityAt60Hz()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(0f, 2f, 5f), new Vector3(3f, 4f, 0f), Rally);
            for (int i = 0; i < 30; i++) Assert.That(sim.Step(), Is.Empty);
            Assert.That(sim.Position.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(sim.Position.y, Is.EqualTo(2f + 4f * 0.5f - 0.5f * 9.81f * 0.25f).Within(0.0001f));
            Assert.That(sim.Velocity.y, Is.EqualTo(4f - 9.81f * 0.5f).Within(0.0001f));
        }

        [Test]
        public void BounceRetainsRestitutionSquaredOfVerticalEnergy()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(0f, 2f, 5f), Vector3.zero, Rally);
            for (int i = 0; i < 120 && sim.BounceCount == 0; i++) sim.Step();
            float energy = 0.5f * sim.Velocity.y * sim.Velocity.y + 9.81f * (sim.Position.y - sim.Radius);
            Assert.That(sim.BounceCount, Is.EqualTo(1));
            Assert.That(sim.Velocity.y, Is.GreaterThan(0f));
            Assert.That(energy, Is.EqualTo(0.7f * 0.7f * 9.81f * (2f - sim.Radius)).Within(0.0005f));
        }

        [Test]
        public void FastDownwardBallCannotTunnelThroughGround()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(0f, 0.5f, 5f), new Vector3(0f, -120f, 0f), Rally);
            var events = sim.Step();
            Assert.That(events.Count(e => e.Kind == BallEventKind.Bounce), Is.EqualTo(1));
            Assert.That(sim.Position.y, Is.GreaterThanOrEqualTo(sim.Radius));
            Assert.That(sim.Velocity.y, Is.GreaterThan(0f));
        }

        [TestCase(0.8f, true)]
        [TestCase(2f, false)]
        public void FastBallHitsNetOrClearsItDependingOnHeight(float height, bool hit)
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(0f, height, -1f), new Vector3(0f, 0f, 120f), Rally);
            Assert.That(sim.Step().Count(e => e.Kind == BallEventKind.Net), Is.EqualTo(hit ? 1 : 0));
            Assert.That(sim.Velocity.z < 0f, Is.EqualTo(hit));
        }

        [Test]
        public void NetTopAndSideAreSweptToo()
        {
            var top = new BallSimulation();
            top.Launch(new Vector3(0f, 1.4f, 0f), new Vector3(0f, -60f, 0f), Rally);
            Assert.That(top.Step().Count(e => e.Kind == BallEventKind.Net), Is.EqualTo(1));
            Assert.That(top.Velocity.y, Is.GreaterThan(0f));
            var side = new BallSimulation();
            side.Launch(new Vector3(-5.2f, 0.8f, 0f), new Vector3(120f, 0f, 0f), Rally);
            Assert.That(side.Step().Count(e => e.Kind == BallEventKind.Net), Is.EqualTo(1));
            Assert.That(side.Velocity.x, Is.LessThan(0f));
        }

        [Test]
        public void SameTickContactsAreEmittedInPhysicalOrder()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(4.6f, 0.1205f, -1f), new Vector3(0f, -2f, 100f), Rally);
            var kinds = sim.Step().Select(e => e.Kind).ToArray();
            Assert.That(kinds, Is.EqualTo(new[] { BallEventKind.Bounce, BallEventKind.Out, BallEventKind.Net }));
        }

        [Test]
        public void FirstAndSecondBounceHaveUniqueIdsAndOutIsEmittedOnlyOnce()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(4.116f, 2f, 5f), Vector3.zero, Rally);
            var events = new List<BallEvent>();
            for (int i = 0; i < 1200 && !sim.IsResting; i++) events.AddRange(sim.Step());
            var bounces = events.Where(e => e.Kind == BallEventKind.Bounce).ToArray();
            Assert.That(bounces[0].IsFirstBounce, Is.True);
            Assert.That(bounces[1].BounceCount, Is.EqualTo(2));
            Assert.That(bounces[1].IsFirstBounce, Is.False);
            Assert.That(events.Count(e => e.Kind == BallEventKind.Out), Is.EqualTo(1));
            Assert.That(events.All(e => e.RallyId == 7 && e.LastHitter == CourtSide.Near), Is.True);
            Assert.That(events.Select(e => e.EventId).Distinct().Count(), Is.EqualTo(events.Count));
            ulong lastId = events.Last().EventId;
            sim.Launch(new Vector3(0f, 0.5f, 5f), new Vector3(0f, -120f, 0f), Rally);
            Assert.That(sim.Step()[0].EventId, Is.GreaterThan(lastId));
        }

        [Test]
        public void RestingBallStopsWithoutGeneratingRepeatedContacts()
        {
            var sim = new BallSimulation();
            sim.Launch(new Vector3(0f, 1f, 5f), Vector3.zero, Rally);
            for (int i = 0; i < 1200 && !sim.IsResting; i++) sim.Step();
            Assert.That(sim.IsResting, Is.True);
            Vector3 position = sim.Position;
            for (int i = 0; i < 120; i++) Assert.That(sim.Step(), Is.Empty);
            Assert.That(sim.Position, Is.EqualTo(position));
            Assert.That(sim.Velocity, Is.EqualTo(Vector3.zero));
            sim.Launch(new Vector3(0f, sim.Radius, 5f), Vector3.zero, Rally);
            Assert.That(sim.Step(), Is.Empty);
        }

        [Test]
        public void BallSettlesOnNetTopWithoutRepeatedNetEvents()
        {
            var sim = new BallSimulation(new BallPhysicsSettings { NetRestitution = 0f });
            sim.Launch(new Vector3(0f, 1.2f, 0f), Vector3.zero, Rally);
            int contacts = 0;
            for (int i = 0; i < 120; i++) contacts += sim.Step().Count(e => e.Kind == BallEventKind.Net);
            Assert.That(sim.IsResting, Is.True);
            Assert.That(contacts, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPhysicsAndShotInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BallSimulation(new BallPhysicsSettings { Radius = 0f }));
            var sim = new BallSimulation();
            Assert.Throws<ArgumentException>(() => sim.Launch(new Vector3(float.NaN, 2f, 5f), Vector3.zero, Rally));
            Assert.Throws<ArgumentException>(() => sim.Launch(new Vector3(0f, 0f, 5f), Vector3.zero, Rally));
            Assert.Throws<ArgumentException>(() => sim.Launch(new Vector3(0f, 0.5f, 0f), Vector3.zero, Rally));
        }
    }
}
