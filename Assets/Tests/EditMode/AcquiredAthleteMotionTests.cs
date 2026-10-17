using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class AcquiredAthleteMotionTests
    {
        [Test]
        public void AcquiredJogMovesBothLegsTorsoAndArmsWithFiniteNormalizedRotations()
        {
            var cfg=AcquiredAthleteMotion.Config;
            Assert.That(cfg.bones.Length,Is.EqualTo(10));
            foreach(int bone in new[]{0,2,4,6,7,8,9})
            {
                float movement=0;var first=AcquiredAthleteMotion.Sample(2,bone,0);
                for(int i=0;i<60;i++)
                {
                    var q=AcquiredAthleteMotion.Sample(2,bone,i/60f);
                    Assert.That(Quaternion.Dot(q,q),Is.EqualTo(1).Within(.001));
                    movement=Mathf.Max(movement,Quaternion.Angle(first,q));
                }
                Assert.That(movement,Is.GreaterThan(2),cfg.bones[bone].path+" must use a moving full-body track");
            }
        }
        [Test]
        public void AuthoredGripAndReachHaveBoundedAnatomicalOffsets()
        {
            var cfg=AcquiredAthleteMotion.Config;
            Assert.That(cfg.gripPalmOffset.magnitude,Is.InRange(.04f,.10f));
            Assert.That(cfg.maxArmStretch,Is.InRange(1,1.1f));
            Assert.That(cfg.recoveryEnd,Is.LessThanOrEqualTo(RacketAvatar.SwingDuration));
        }
        [Test]
        public void LoopBoundaryDoesNotSnapAndIdleIsDistinctFromRunning()
        {
            for(int bone=0;bone<10;bone++)
            {
                Assert.That(Quaternion.Angle(AcquiredAthleteMotion.Sample(2,bone,.99999f),AcquiredAthleteMotion.Sample(2,bone,0)),Is.LessThan(.2));
            }
            Assert.That(Quaternion.Angle(AcquiredAthleteMotion.Sample(0,6,.3f),AcquiredAthleteMotion.Sample(2,6,.3f)),Is.GreaterThan(10));
        }
    }
}
