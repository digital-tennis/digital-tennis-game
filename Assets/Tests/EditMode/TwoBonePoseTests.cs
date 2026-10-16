using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class TwoBonePoseTests
    {
        [TestCase(-1f)][TestCase(1f)]
        public void ReachableTargetsBendAtThePoleWithoutMovingTheRoot(float side)
        {
            var a=new GameObject("root");var b=new GameObject("elbow");var c=new GameObject("hand");
            try{b.transform.SetParent(a.transform,false);c.transform.SetParent(b.transform,false);b.transform.localPosition=Vector3.down;c.transform.localPosition=Vector3.down;
                var target=new Vector3(side,-1,0);float error=TwoBonePose.Solve(a.transform,b.transform,c.transform,target,Vector3.forward);
                Assert.That(error,Is.LessThan(.001f));Assert.That(a.transform.position,Is.EqualTo(Vector3.zero));Assert.That(b.transform.position.z,Is.GreaterThan(0));}
            finally{Object.DestroyImmediate(a);}
        }
    }
}
