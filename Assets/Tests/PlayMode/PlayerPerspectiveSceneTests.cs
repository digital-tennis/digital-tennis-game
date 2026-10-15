using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DigitalTennis.Tests
{
    public sealed class PlayerPerspectiveSceneTests
    {
        [UnityTest]
        public IEnumerator GuestCameraFacesOwnPlayerAndCanReturnToSinglePlayerView()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = Object.FindFirstObjectByType<TennisMatchController>();
            var originalPosition = Camera.main.transform.position;
            Vector3 playerA = new Vector3(0, .85f, -8), playerB = new Vector3(0, .85f, 8);
            float aBefore = c.View.Project(playerA).x, aRight = c.View.Project(playerA + Vector3.right).x;
            Assert.That(aRight, Is.GreaterThan(aBefore));
            c.EnterOnline(false); c.PresentOnline(Vector3.up, playerA, playerB);
            Assert.That(c.View.LocalPlayer, Is.EqualTo(CourtSide.Far));
            Assert.That(c.View.Project(c.FarAvatar.transform.position).y, Is.LessThan(c.View.Project(c.NearAvatar.transform.position).y));
            Assert.That(c.View.Project(playerA + Vector3.right).x, Is.LessThan(c.View.Project(playerA).x));
            c.EnterOnline(false); Assert.That(Camera.main.transform.position.z, Is.GreaterThan(0));
            c.GetComponent<OnlineTennisSession>().Leave();
            Assert.That(Camera.main.transform.position, Is.EqualTo(originalPosition));
            Assert.That(c.Mode, Is.EqualTo(GameMode.SinglePlayer));
        }
        [UnityTest]
        public IEnumerator ReceivedPoseAndStrokeArePresentedTogetherWithoutCountParityGuess()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = Object.FindFirstObjectByType<TennisMatchController>(); c.EnterOnline(false);
            var state = new GamePayload { matchId = "pose-test", phase = "Rally", serverId = "host", lastHitter = "host",
                serveSide = "right", rallyId = 1, shotId = 3, forehandHits = 2, backhandHits = 1, nearHits = 3,
                points = new[] { 0, 0 }, ball = new BallState { pos = new[] { 1f, 1f, -4f }, vel = new[] { 0f, 1f, 10f } },
                players = new[] {
                    new PlayerState { id = "host", pos = new[] { 1f, .85f, -8f }, swingId = 3, stroke = (int)StrokeKind.Backhand, swingAge = .08f },
                    new PlayerState { id = "guest", pos = new[] { -1f, .85f, 8f }, swingId = 2, stroke = (int)StrokeKind.Forehand, swingAge = .35f }
                }};
            var buffer = new SnapshotBuffer();
            Assert.That(buffer.Push(new GameMessage { type = "snapshot", epoch = 1, tick = 20, payload = state }, 1), Is.True);
            buffer.Present(1.1, c); yield return null;
            Assert.That(c.NearAvatar.transform.position, Is.EqualTo(new Vector3(1, .85f, -8)), "World-space actor identity must not be swapped or negated.");
            Assert.That(c.NearAvatar.LastStroke, Is.EqualTo(StrokeKind.Backhand), "Use transmitted stroke, regardless of aggregate forehand parity.");
            Assert.That(c.NearAvatar.IsSwinging, Is.True);
            Assert.That(c.NearAvatar.RemoteBackhands, Is.EqualTo(1));
            Assert.That(c.FarAvatar.IsSwinging, Is.False, "Do not restart an expired remote swing.");
            buffer.Present(1.2, c); Assert.That(c.NearAvatar.RemoteBackhands, Is.EqualTo(1));
            Assert.That(c.Ball.PhysicsTicks, Is.Zero);
        }
        [UnityTest]
        public IEnumerator ServeAnimationIsIncludedInAuthoritativeSnapshot()
        {
            yield return SceneManager.LoadSceneAsync("SinglePlayerCourt"); yield return null;
            var c = Object.FindFirstObjectByType<TennisMatchController>(); c.EnterOnline(true); c.IsPaused = false;
            Assert.That(c.Serve(), Is.True);
            var state = GamePayload.Capture(c, "serve", 0);
            Assert.That(state.players[0].swingId, Is.GreaterThan(0));
            Assert.That(state.players[0].stroke, Is.EqualTo((int)StrokeKind.Forehand));
            Assert.That(state.players[0].swingAge, Is.LessThan(RacketAvatar.SwingDuration));
            Assert.That(state.players[1].swingId, Is.Zero);
        }
    }
}
