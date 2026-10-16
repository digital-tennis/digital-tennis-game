using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DigitalTennis.Editor
{
    // Staged art review only. No inputs, points, contacts or match state are fabricated.
    public static class AthleteProportionEvidence
    {
        public static void Before()
        {
            const string folder="Assets/Editor/ProportionOriginals";
            Directory.CreateDirectory(folder);
            foreach(string id in new[]{"KOR","JPN"})
            {
                var target=folder+"/"+id+".asset";
                File.Copy(".work/proportion-review/originals/"+id+".asset",target,true);
                AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            }
            try { Capture("before"); }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                if(Directory.Exists("Assets/Editor")&&!Directory.EnumerateFileSystemEntries("Assets/Editor").Any())
                    AssetDatabase.DeleteAsset("Assets/Editor");
            }
        }
        public static void After()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity");
            AthleteSkinSetup.Prepare(Object.FindFirstObjectByType<TennisMatchController>());
            Capture("after");
        }
        private static void Capture(string phase)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity");
            var original = Object.FindFirstObjectByType<AthleteSkin>();
            var copy = Object.Instantiate(original.gameObject);
            copy.transform.SetParent(null);
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            foreach (var path in AthleteSkin.BonePaths) if (path != "") copy.transform.Find(path).localRotation = Quaternion.identity;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (!renderer.transform.IsChildOf(copy.transform)) renderer.enabled = false;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var key = new GameObject("Review key").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.2f; key.transform.rotation = Quaternion.Euler(35, -25, 0);
            var camera = new GameObject("Matched art camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f,.095f,.13f);
            camera.orthographic = true;
            var folder = ".work/proportion-review";
            Directory.CreateDirectory(folder);
            var receipt=new System.Text.StringBuilder();
            foreach (int index in new[] {0,1})
            {
                if(index>0)
                {
                    Object.DestroyImmediate(copy);
                    copy=Object.Instantiate(original.gameObject);copy.transform.SetParent(null);
                    copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
                    foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                    foreach(var path in AthleteSkin.BonePaths)if(path!="")copy.transform.Find(path).localRotation=Quaternion.identity;
                }
                var skin=copy.GetComponent<AthleteSkin>();skin.Present(index);
                if(phase=="before")skin.Surface.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Editor/ProportionOriginals/"+CharacterCatalog.Ids[index]+".asset");
                float armAngle=AthleteStyleCatalog.Get(index).garment.reviewArmAngle;
                copy.transform.Find("Torso/RightArm").localRotation=Quaternion.Euler(0,0,armAngle);
                copy.transform.Find("Torso/LeftArm").localRotation=Quaternion.Euler(0,0,-armAngle);
                string source=phase=="before"?".work/proportion-review/originals/"+CharacterCatalog.Ids[index]+".asset":"Assets/Resources/Athletes/"+CharacterCatalog.Ids[index]+".asset";
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    receipt.AppendLine(CharacterCatalog.Ids[index]+" "+System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant()+" "+skin.Surface.sharedMesh.name+" vertices="+skin.Surface.sharedMesh.vertexCount+" uv="+skin.Surface.sharedMesh.uv.Length);
                foreach (string view in new[] {"front", "side", "face"})
                {
                    bool close = view == "face";
                    var target = new Vector3(0, close ? 1.57f : .90f, 0);
                    camera.orthographicSize = close ? .28f : 1.0f;
                    camera.transform.position = target + (view == "side" ? Vector3.right : Vector3.forward) * 4;
                    camera.transform.LookAt(target);
                    var rt = new RenderTexture(900,1100,24);
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    var png=new Texture2D(900,1100,TextureFormat.RGB24,false);
                    png.ReadPixels(new Rect(0,0,900,1100),0,0);png.Apply();
                    File.WriteAllBytes($"{folder}/{phase}-{CharacterCatalog.Ids[index]}-{view}.png",png.EncodeToPNG());
                    RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
                }
            }
            File.WriteAllText($"{folder}/{phase}.json", "{\"baselineRevision\":\"69bdf9ca797fb8396a7b25bc040f0e75bcf8e5e0\",\"stagedArtPreview\":true,\"width\":900,\"height\":1100,\"orthographicBody\":1.0,\"orthographicFace\":0.28,\"graphics\":\""+SystemInfo.graphicsDeviceName+"\"}");
            File.WriteAllText($"{folder}/{phase}-assets.txt",receipt.ToString());
            Debug.Log("PROPORTION_REVIEW " + phase + " captured 6 real Unity camera images, staged neutral pose");
        }
    }
}
