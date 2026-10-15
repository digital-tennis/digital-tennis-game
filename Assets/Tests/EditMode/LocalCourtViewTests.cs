using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class LocalCourtViewTests
    {
        private Camera camera;
        private LocalCourtView view;
        [SetUp] public void SetUp()
        {
            camera = new GameObject("View test").AddComponent<Camera>();
            camera.aspect = 1.6f; camera.fieldOfView = 48;
            camera.transform.position = new Vector3(0, 22, -25); camera.transform.LookAt(Vector3.zero);
            view = new LocalCourtView(camera);
        }
        [TearDown] public void TearDown() { view.Dispose(); Object.DestroyImmediate(camera.gameObject); }
        [TestCase(CourtSide.Near)] [TestCase(CourtSide.Far)]
        public void OwnPlayerIsBelowOpponentAndOpponentRightAppearsLeft(CourtSide local)
        {
            view.SetPlayer(local);
            float facing = local == CourtSide.Near ? 1 : -1;
            Vector3 own = new Vector3(0, .85f, -8 * facing), opponent = new Vector3(0, .85f, 8 * facing);
            Assert.That(view.Project(own).y, Is.LessThan(view.Project(opponent).y));
            Assert.That(view.Project(own + Vector3.right * facing).x, Is.GreaterThan(view.Project(own).x));
            Assert.That(view.Project(opponent - Vector3.right * facing).x, Is.LessThan(view.Project(opponent).x));
        }
        [TestCase(CourtSide.Near)] [TestCase(CourtSide.Far)]
        public void PositiveAimStillMeansRightOnOwnScreen(CourtSide local)
        {
            view.SetPlayer(local);
            var input = new RacketInput(local, StrokeKind.Forehand, 1, 1, .35f, 1, 0, 1, 1, 1);
            Vector3 origin = new Vector3(0, 1, local == CourtSide.Near ? -8 : 8);
            Assert.That(view.Project(origin + RacketShot.Velocity(input) * .1f).x, Is.GreaterThan(view.Project(origin).x));
        }
        [Test]
        public void RepeatedGuestViewDoesNotAccumulateAndLeavingRestoresCamera()
        {
            Vector3 original = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            view.SetPlayer(CourtSide.Far); Vector3 guest = camera.transform.position;
            view.SetPlayer(CourtSide.Far); Assert.That(camera.transform.position, Is.EqualTo(guest));
            view.SetPlayer(CourtSide.Near); Assert.That(camera.transform.position, Is.EqualTo(original));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.001f));
        }
        [Test]
        public void AvatarUsesExplicitStrokeOnceAndDoesNotReplayExpiredCheckpoint()
        {
            var obj = new GameObject("Remote racket test");
            try
            {
                var avatar = obj.AddComponent<RacketAvatar>();
                avatar.PresentRemoteSwing(7, StrokeKind.Backhand, .1f);
                Assert.That(avatar.LastStroke, Is.EqualTo(StrokeKind.Backhand));
                Assert.That(avatar.IsSwinging, Is.True); Assert.That(avatar.RemoteBackhands, Is.EqualTo(1));
                avatar.PresentRemoteSwing(7, StrokeKind.Backhand, .2f);
                Assert.That(avatar.RemoteBackhands, Is.EqualTo(1));
                avatar.PresentRemoteSwing(6, StrokeKind.Forehand, 0);
                Assert.That(avatar.LastStroke, Is.EqualTo(StrokeKind.Backhand));
                avatar.PresentRemoteSwing(8, StrokeKind.Forehand, 1f);
                Assert.That(avatar.IsSwinging, Is.False); Assert.That(avatar.RemoteForehands, Is.Zero);
            }
            finally { Object.DestroyImmediate(obj); }
        }
    }
}
