using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class PlayerInputAdapterTests
    {
        [Test]
        public void KeyboardMapsControlsToNormalizedIntents()
        {
            var keys = new HashSet<KeyCode> { KeyCode.Space, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.D, KeyCode.R, KeyCode.F, KeyCode.L };
            var keyboard = new KeyboardPlayerInputSource(key => keys.Remove(key));
            keyboard.Poll(10, .7f, -.3f, .2f);
            var samples = new List<PlayerInputSample>();
            while (keyboard.TryRead(out var sample)) samples.Add(sample);
            Assert.That(samples.Count, Is.EqualTo(8));
            Assert.That(samples.ConvertAll(x => x.Action), Is.EquivalentTo(new[] { PlayerAction.Serve, PlayerAction.Forehand,
                PlayerAction.Backhand, PlayerAction.Forehand, PlayerAction.Backhand, PlayerAction.Restart, PlayerAction.FaultServe, PlayerAction.LetServe }));
            Assert.That(samples.FindAll(x => x.PracticeOpponent).Count, Is.EqualTo(2));
            foreach (var sample in samples) { Assert.That(sample.IsValid); Assert.That(sample.Power01, Is.EqualTo(.7f)); Assert.That(sample.AimX, Is.EqualTo(-.3f)); }
            keyboard.Poll(11, .35f, 0, 0); Assert.That(keyboard.TryRead(out _), Is.False, "Key-down is not a held-key stream.");
        }
        [Test]
        public void RouterRejectsDuplicatesStaleFutureAndNonFiniteInput()
        {
            var source = new BufferedPlayerInputSource("fixture"); var router = new PlayerInputRouter(); router.SetSource(source);
            source.Enqueue(new PlayerInputSample(PlayerAction.Forehand, 1, 10));
            source.Enqueue(new PlayerInputSample(PlayerAction.Backhand, 1, 10));
            source.Enqueue(new PlayerInputSample(PlayerAction.Forehand, 2, 9));
            source.Enqueue(new PlayerInputSample(PlayerAction.Forehand, 3, 11));
            source.Enqueue(new PlayerInputSample(PlayerAction.Forehand, 4, 10, float.NaN));
            source.Enqueue(new PlayerInputSample((PlayerAction)99, 5, 10));
            int delivered = 0; router.Drain(10.1, (sample, session) => delivered++);
            Assert.That(delivered, Is.EqualTo(1)); Assert.That(router.Rejected, Is.EqualTo(5));
        }
        [Test]
        public void SourceSwitchDropsBothBacklogsAndCreatesNewSession()
        {
            var first = new BufferedPlayerInputSource("keyboard fixture"); var second = new BufferedPlayerInputSource("decoded IMU fixture");
            var router = new PlayerInputRouter(); router.SetSource(first); uint before = router.SessionId;
            first.Enqueue(new PlayerInputSample(PlayerAction.Forehand, 1, 1));
            second.Enqueue(new PlayerInputSample(PlayerAction.Backhand, 1, 1));
            router.SetSource(second); Assert.That(router.SessionId, Is.Not.EqualTo(before));
            int delivered = 0; router.Drain(1, (_, __) => delivered++); Assert.That(delivered, Is.Zero);
            Assert.That(first.TryRead(out _), Is.False);
            second.Enqueue(new PlayerInputSample(PlayerAction.Backhand, 1, 1));
            router.Drain(1, (_, __) => delivered++); Assert.That(delivered, Is.EqualTo(1));
        }
        [Test]
        public void BufferedIngressIsBoundedAndKeepsRecentMotion()
        {
            var source = new BufferedPlayerInputSource("fixture");
            for(uint i=1;i<=100;i++) source.Enqueue(new PlayerInputSample(PlayerAction.Forehand,i,1));
            Assert.That(source.TryRead(out var first), Is.True); Assert.That(first.EventId, Is.EqualTo(69));
            int count=1; while(source.TryRead(out _))count++; Assert.That(count,Is.EqualTo(32));
        }
    }
}
