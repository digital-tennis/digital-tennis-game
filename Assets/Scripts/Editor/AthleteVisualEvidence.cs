using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DigitalTennis.Editor
{
    // Opt-in, game-camera-only asset review. Does not fabricate gameplay events.
    public static class AthleteVisualEvidence
    {
        public static void Before() { Capture("before", false); }
        public static void After() { Capture("after", true); }
        private static void Capture(string phase, bool rebuild)
        {
            const string originalFolder="Assets/Editor/AthleteReviewOriginals";
            if(!rebuild)
            {
                Directory.CreateDirectory(originalFolder);
                foreach(string source in Directory.GetFiles(".work/athlete-visuals/original-assets","*.asset"))
                {
                    string path=originalFolder+"/"+Path.GetFileName(source);File.Copy(source,path,true);AssetDatabase.ImportAsset(path);
                }
            }
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity");
            var controller=Object.FindFirstObjectByType<TennisMatchController>();
            if(rebuild) { AthleteSkinSetup.Prepare(controller); EditorSceneManager.SaveScene(scene); }
            var skin=Object.FindObjectsByType<AthleteSkin>(FindObjectsSortMode.None).First();
            var root=skin.transform;
            // Isolate the real runtime renderer; use identical camera/light/pose on both runs.
            var copy=Object.Instantiate(root.gameObject); copy.name="Asset Review";
            copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
            foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!r.transform.IsChildOf(copy.transform))r.enabled=false;
            var review=copy.GetComponent<AthleteSkin>();
            var camera=new GameObject("Review Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.095f,.13f);camera.fieldOfView=28;
            var light=new GameObject("Review Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(35,-25,0);
            Directory.CreateDirectory(".work/athlete-visuals");
            for(int i=0;i<15;i++)
            {
                // Fresh renderer per camera review avoids multiple GPU skinning layouts in one editor frame.
                if(i>0)
                {
                    Object.DestroyImmediate(copy);
                    copy=Object.Instantiate(root.gameObject);copy.name="Asset Review";
                    copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
                    foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                    review=copy.GetComponent<AthleteSkin>();
                }
                review.Present(i);
                if(!rebuild)
                {
                    review.Surface.sharedMesh=null;
                    review.Surface.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(originalFolder+"/"+CharacterCatalog.Ids[i]+".asset");
                }
                foreach(bool close in new[]{false,true})
                {
                    var target=new Vector3(0,close?1.56f:.92f,0);camera.transform.position=target+new Vector3(close?.30f:.60f,close?.06f:.15f,close?.94f:3.7f);camera.transform.LookAt(target);
                    var rt=new RenderTexture(800,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    var png=new Texture2D(800,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,800,1000),0,0);png.Apply();
                    File.WriteAllBytes(".work/athlete-visuals/"+phase+"-"+CharacterCatalog.Ids[i]+(close?"-face":"-body")+".png",png.EncodeToPNG());
                    RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
                }
            }
            if(!rebuild)AssetDatabase.DeleteAsset(originalFolder);
            Debug.Log("ATHLETE_VISUAL_CAPTURE "+phase+" 15 athletes, matched body and face cameras; graphics="+SystemInfo.graphicsDeviceName);
        }
    }
}
