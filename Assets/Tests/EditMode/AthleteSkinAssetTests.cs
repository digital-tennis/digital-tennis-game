using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace DigitalTennis.Tests
{
    public sealed class AthleteSkinAssetTests
    {
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
                foreach(var w in mesh.boneWeights)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001f));
                Assert.That(signatures.Add(mesh.name),Is.True);Assert.That(style.shoulders,Is.GreaterThan(style.waist));
            }
        }
    }
}
