using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class BallSceneTests
    {
        [UnityTest]
        public IEnumerator DevelopmentSceneRunsEveryShotPreset()
        {
            yield return SceneManager.LoadSceneAsync("DevelopmentCourt");
            yield return null;
            var ball = Object.FindFirstObjectByType<BallFlight>();
            var demo = Object.FindFirstObjectByType<BallBounceDemo>();
            Assert.That(ball, Is.Not.Null);
            Assert.That(demo, Is.Not.Null);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(BallSimulation.FixedStep).Within(0.000001f));
            demo.AutoReplay = false;
            var records = new List<BallEvent>();
            ball.EventRaised += records.Add;
            var allIds = new HashSet<ulong>();
            for (int preset = 0; preset < 6; preset++)
            {
                records.Clear();
                demo.LaunchPreset(preset);
                yield return new WaitForSeconds(3f);
                var bounces = records.Where(e => e.Kind == BallEventKind.Bounce).ToArray();
                Assert.That(bounces.Length, Is.GreaterThanOrEqualTo(2), "preset " + preset);
                Assert.That(bounces[0].IsInBounds, Is.EqualTo(preset == 0 || preset == 1 || preset == 4), "preset " + preset);
                Assert.That(bounces[0].BounceCount, Is.EqualTo(1));
                Assert.That(bounces[1].BounceCount, Is.EqualTo(2));
                Assert.That(records.Count(e => e.Kind == BallEventKind.Out), Is.EqualTo(bounces[0].IsInBounds ? 0 : 1));
                Assert.That(records.Count(e => e.Kind == BallEventKind.Net), Is.EqualTo(preset == 3 ? 1 : 0));
                Assert.That(Vector3.Distance(ball.transform.position, ball.Simulation.Position), Is.LessThan(0.00001f));
                foreach (var item in records) Assert.That(allIds.Add(item.EventId), Is.True);
            }
            ball.EventRaised -= records.Add;
        }

        [UnityTest]
        public IEnumerator NonAuthoritativeBallDoesNotSimulateOrPublishEvents()
        {
            var item = new GameObject("Guest ball");
            item.transform.position = new Vector3(2f, 2f, -3f);
            var ball = item.AddComponent<BallFlight>();
            ball.IsAuthoritative = false;
            int events = 0;
            ball.EventRaised += _ => events++;
            ball.Launch(new Vector3(0f, 1f, 5f), Vector3.down, new ShotContext(99, CourtSide.Near));
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.Simulation.Tick, Is.EqualTo(0));
            Assert.That(events, Is.EqualTo(0));
            Assert.That(item.transform.position, Is.EqualTo(new Vector3(2f, 2f, -3f)));
            Object.Destroy(item);
            yield return null;
        }
    }
}
