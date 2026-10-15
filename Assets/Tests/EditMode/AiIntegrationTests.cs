using System.Linq;
using DigitalTennis.AI;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class AiIntegrationTests
    {
        [Test]
        public void AiPackageHasNoUnityOrGameAssemblyDependency()
        {
            var assembly = typeof(AiPolicy).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("DigitalTennis.AI"));
            Assert.That(assembly.GetReferencedAssemblies().Any(item => item.Name.StartsWith("UnityEngine") ||
                item.Name == "DigitalTennis.Runtime"), Is.False);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            Assert.That(package.name, Is.EqualTo("com.digital-tennis.ai"));
            Assert.That(package.source, Is.EqualTo(UnityEditor.PackageManager.PackageSource.Git));
        }

        [Test]
        public void IndependentLandingPredictionAgreesWithActualGamePhysics()
        {
            var ball = new BallSimulation();
            Vector3 position = new Vector3(1.5f, 2.2f, -12.6f), target = new Vector3(-1.5f, ball.Radius, 4f);
            Vector3 velocity = (target - position) / 1.2f + Vector3.up * (.5f * ball.Gravity * 1.2f);
            var prediction = BallPrediction.Landing(new Vec3(position.x, position.y, position.z),
                new Vec3(velocity.x, velocity.y, velocity.z), ball.Gravity, ball.Radius);
            ball.Launch(position, velocity, new ShotContext(1, CourtSide.Near));
            BallEvent? bounce = null;
            for (int tick = 0; tick < 120 && !bounce.HasValue; tick++)
                foreach (var item in ball.Step()) if (item.Kind == BallEventKind.Bounce) bounce = item;
            Assert.That(bounce.HasValue, Is.True);
            Assert.That(prediction.Valid, Is.True);
            Assert.That(prediction.TimeToBounce, Is.EqualTo(1.2f).Within(.001f));
            Assert.That(prediction.Position.X, Is.EqualTo(bounce.Value.Position.x).Within(.01f));
            Assert.That(prediction.Position.Z, Is.EqualTo(bounce.Value.Position.z).Within(.01f));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void PlannedAiShotLandsInBoundsThroughTheRealRacketAndBallSimulation(int player)
        {
            var policy = new AiPolicy(GameplayBalance.Defaults.Difficulty(Difficulty.Hard),GameplayBalance.Defaults.AiTuning(),1);
            Vector3 origin = new Vector3(0, 1, player == 0 ? -6 : 6);
            AiDecision decision = default;
            for (int tick = 1; tick < 30 && !decision.ShouldSwing; tick++)
                decision = policy.Decide(new AiState(tick, .02f, player, new Vec3(0,.85f,origin.z),
                    new Vec3(1,.85f,-origin.z), new Vec3(0,1,origin.z), new Vec3(0,2,player==0?-12:12),
                    AiPhase.Rally,1,1,1-player,gravity:GameplayBalance.Defaults["physics.gravity"],radius:GameplayBalance.Defaults["physics.radius"],bounceRestitution:GameplayBalance.Defaults["physics.bounce"],speedRetention:GameplayBalance.Defaults["physics.ground_retention"]));
            Assert.That(decision.ShouldSwing, Is.True);
            var input = new RacketInput((CourtSide)player,(StrokeKind)decision.Stroke,100,1,
                decision.Power01,decision.AimX,decision.AimZ,1,1,1);
            var ball = new BallSimulation();
            ball.Launch(origin, RacketShot.Velocity(input),new ShotContext(1,(CourtSide)player));
            BallEvent? bounce = null;
            for (int tick=0;tick<180&&!bounce.HasValue;tick++)
                foreach(var item in ball.Step())if(item.Kind==BallEventKind.Bounce)bounce=item;
            Assert.That(bounce.HasValue,Is.True);
            Assert.That(bounce.Value.IsInBounds,Is.True);
            Assert.That(bounce.Value.Position.x,Is.EqualTo(-2.5f).Within(.01f));
            Assert.That(bounce.Value.Position.z,Is.EqualTo(player==0?8:-8).Within(.01f));
        }
    }
}
