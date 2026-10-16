using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
 public sealed class VenueCameraTests
 {
  [TestCase(CourtSide.Near)][TestCase(CourtSide.Far)]public void EstablishingShotRestoresEveryCameraFieldAndHonorsReducedMotion(CourtSide end)
  {
   var go=new GameObject("Venue camera test");var camera=go.AddComponent<Camera>();var p=new Vector3(0,24,-20);var q=Quaternion.Euler(50,0,0);camera.transform.SetPositionAndRotation(p,q);camera.fieldOfView=42;var director=new VenueCameraDirector();
   try{director.Present(camera,end,true,false,.2f);Assert.That(Vector3.Distance(camera.transform.position,p),Is.GreaterThan(5));director.Present(camera,end,false,false,.1f);Assert.That(camera.transform.position,Is.EqualTo(p));Assert.That(Quaternion.Angle(camera.transform.rotation,q),Is.LessThan(.001f));Assert.That(camera.fieldOfView,Is.EqualTo(42));director.Present(camera,end,true,true,.2f);Assert.That(camera.transform.position,Is.EqualTo(p));}
   finally{director.Restore();Object.DestroyImmediate(go);}
  }
 }
}
