using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class AthleteSkinAssetTests
    {
        [System.Serializable]private sealed class ImportedGeometry {public Vector3[] vertices;public int[] triangles;}
        [Test]public void AllRuntimeSkinsContainTheAcquiredFacialTopologyOnTheHeadBone()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var skin=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AthleteSkin>(true)).First();
                foreach(int i in Enumerable.Range(0,15))
                {
                    skin.Present(i);var mesh=skin.Surface.sharedMesh;
                    Assert.That(mesh,Is.SameAs(Resources.Load<Mesh>("Athletes/"+CharacterCatalog.Ids[i])));
                    Assert.That(mesh.name,Does.Contain("-cc0-"));
                    var materialState=new MaterialPropertyBlock();skin.Surface.GetPropertyBlock(materialState);
                    Assert.That(materialState.GetFloat("_FittedSports"),Is.EqualTo(i<2?1:0),"Profile switches must clear fitted-garment state");
                    // Authority invariant applies to both full-body and legacy skins.
                    Assert.That(skin.GetComponentsInChildren<Animator>().All(a=>!a.applyRootMotion),Is.True);
                    var style=AthleteStyleCatalog.Get(i);
                    if(!string.IsNullOrEmpty(style.acquiredBody))
                    {
                        var body=JsonUtility.FromJson<ImportedGeometry>(Resources.Load<TextAsset>("Characters/Acquired/"+style.acquiredBody).text);
                        CollectionAssert.AreEqual(body.triangles,mesh.triangles.Take(body.triangles.Length).ToArray(),style.id+" full acquired body must be used at runtime");
                        Assert.That(body.vertices.Length,Is.GreaterThan(8000));
                        Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount),"Fitted garment rest coordinates survive asset update");
                        Assert.That(mesh.uv2.Length,Is.EqualTo(mesh.vertexCount),"Fitted garment region mask survives asset update");
                        Assert.That(mesh.uv2.Count(uv=>uv.x==1),Is.GreaterThan(5000));
                        var actual=mesh.vertices;
                        for(int j=0;j<body.vertices.Length;j++)
                            Assert.That(Vector3.Distance(actual[j],body.vertices[j]),Is.LessThan(.025f),style.id+" source topology and authored brow refinement");
                        foreach(int bone in new[]{1,2,3,4,5,6,7,8,9,10,11,12,13,14})
                            Assert.That(mesh.boneWeights.Any(w=>w.boneIndex0==bone),Is.True,style.id+" must retain full-body weights: "+bone);
                        continue;
                    }
                    var imported=JsonUtility.FromJson<ImportedGeometry>(Resources.Load<TextAsset>("Characters/Acquired/"+style.acquiredHead).text);
                    int first=System.Array.FindIndex(mesh.boneWeights,w=>w.boneIndex0==2&&w.weight0==1);
                    Assert.That(first,Is.GreaterThan(0));
                    // The complete acquired head index stream must exist, not a token triangle or unused file.
                    var triangles=mesh.triangles;int begin=System.Array.FindIndex(triangles,x=>x==first+imported.triangles[0]);
                    Assert.That(begin,Is.GreaterThanOrEqualTo(0));
                    CollectionAssert.AreEqual(imported.triangles,triangles.Skip(begin).Take(imported.triangles.Length).Select(x=>x-first).ToArray(),CharacterCatalog.Ids[i]);
                    Assert.That(imported.vertices.Length,Is.GreaterThan(1000));
                    Assert.That(mesh.boneWeights.Skip(first).Take(imported.vertices.Length).All(w=>w.boneIndex0==2&&w.weight0==1),Is.True);
                }
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }
        [Test]public void AcquiredIdleIsNonStaticBoundedAndLoopsWithoutDisplacingTheRig()
        {
            float maximum=0;
            for(int i=0;i<200;i++)
            {
                var rotation=AcquiredAthleteIdle.Sample(i*.05f);
                Assert.That(float.IsNaN(rotation.x)||float.IsInfinity(rotation.x),Is.False);
                maximum=Mathf.Max(maximum,Quaternion.Angle(Quaternion.identity,rotation));
            }
            Assert.That(maximum,Is.GreaterThan(.01f).And.LessThan(15));
        }
        [Test]public void BendingTheElbowDeformsItsWeightedSurfaceWithoutMovingTheHead()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var before=new Mesh();var after=new Mesh();
            try
            {
                var skin=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AthleteSkin>(true)).First();var surface=skin.Surface;
                surface.BakeMesh(before);surface.bones[4].localRotation=Quaternion.Euler(-95,0,0);surface.BakeMesh(after);
                var weights=surface.sharedMesh.boneWeights;var first=before.vertices;var second=after.vertices;float arm=0,head=0;
                for(int i=0;i<weights.Length;i++){float distance=Vector3.Distance(first[i],second[i]);if(weights[i].boneIndex0==4)arm=Mathf.Max(arm,distance);if(weights[i].boneIndex0==2)head=Mathf.Max(head,distance);}
                Assert.That(arm,Is.GreaterThan(.08f));Assert.That(head,Is.LessThan(.00001f));
            }
            finally{Object.DestroyImmediate(before);Object.DestroyImmediate(after);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }
        [Test]public void EveryPersonaHasADistinctBoundSkinWithExpressionsAndNormalizedWeights()
        {
            var signatures=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<15;i++)
            {
                var style=AthleteStyleCatalog.Get(i);Assert.That(style.id,Is.EqualTo(CharacterCatalog.Ids[i]));
                var mesh=Resources.Load<Mesh>("Athletes/"+style.id);Assert.That(mesh,Is.Not.Null);Assert.That(mesh.bindposes.Length,Is.EqualTo(15));Assert.That(mesh.blendShapeCount,Is.EqualTo(4));
                var expression=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(2,0,expression,null,null);Assert.That(expression.All(d=>d.magnitude<.05f),Is.True,"Shout must open the mouth locally, rather than move it towards the eyes");
                Assert.That(mesh.vertexCount,Is.GreaterThan(1000));Assert.That(mesh.vertices.All(BallSimulation.IsFinite),Is.True);
                Assert.That(mesh.normals.All(n=>BallSimulation.IsFinite(n)&&n.sqrMagnitude>.1f),Is.True,"Valid surfaces must not have cancelled/zero normals: "+style.id);
                foreach(var w in mesh.boneWeights)
                {
                    Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001f));
                    Assert.That(w.weight0,Is.GreaterThanOrEqualTo(w.weight1));Assert.That(w.weight1,Is.GreaterThanOrEqualTo(w.weight2));
                }
                Assert.That(mesh.bounds.size.x,Is.LessThan(1),"Clothing clearance must never use a subdivision count as metres");
                Assert.That(mesh.bounds.size.z,Is.LessThan(1),"No floating uniform panels outside the athlete");
                Assert.That(signatures.Add(mesh.name),Is.True);Assert.That(style.shoulders,Is.GreaterThan(style.waist));
            }
        }
    }
}
