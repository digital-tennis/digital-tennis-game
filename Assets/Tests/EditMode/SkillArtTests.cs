using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
 public sealed class SkillArtTests
 {
  [Test]public void EveryCourtHasItsOwnFinitePerimeterDesign()
  {
   var shapes=new HashSet<string>();for(int style=0;style<15;style++){string signature="";for(int i=0;i<=64;i++){var p=CourtArtCatalog.Motif(style,i/64f);Assert.That(BallSimulation.IsFinite(p.x)&&BallSimulation.IsFinite(p.y));Assert.That(p.magnitude,Is.LessThan(1));signature+=p.ToString("F3");}Assert.That(shapes.Add(signature),Is.True);}
  }
  [Test]public void IndividualSkillsHaveOrderedFiniteKeysAndReturnToTheirReadyPose()
  {
   var names=new HashSet<string>();var shapes=new HashSet<int>();var accents=new HashSet<string>();
   for(int i=0;i<15;i++)
   {
    var art=SkillArtCatalog.Get(i);Assert.That(art.id,Is.EqualTo(CharacterCatalog.Ids[i]));Assert.That(names.Add(art.name));Assert.That(shapes.Add(art.shape));Assert.That(art.frames.Length,Is.GreaterThanOrEqualTo(5));
    Assert.That(art.frames[0].time,Is.Zero);Assert.That(art.frames[art.frames.Length-1].time,Is.EqualTo(1));
    float before=-1;foreach(var f in art.frames){Assert.That(f.time,Is.GreaterThan(before));before=f.time;Assert.That(BallSimulation.IsFinite(f.camera)&&BallSimulation.IsFinite(f.shift));Assert.That(f.fov,Is.InRange(25,60));Assert.That(f.jump,Is.InRange(0,.5f));}
    var a=SkillArtCatalog.Sample(i,0);var b=SkillArtCatalog.Sample(i,1);Assert.That(Quaternion.Angle(a.Rotation(f=>f.rightArm),b.Rotation(f=>f.rightArm)),Is.LessThan(.01));Assert.That(b.Jump,Is.Zero);Assert.That(b.Shift,Is.EqualTo(Vector3.zero));
    Assert.That(accents.Add(JsonUtility.ToJson(art.frames[2])));for(int r=0;r<4;r++)for(int k=0;k<=32;k++)Assert.That(BallSimulation.IsFinite(SkillArtCatalog.Ribbon(i,r,k/32f,.5f)));
    Assert.That(CourtArtCatalog.Get(i).id,Is.EqualTo(art.id));
   }
  }
 }
}
