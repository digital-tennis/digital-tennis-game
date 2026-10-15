using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class FrontEndProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/FrontEnd.unity";
        public static void PrepareScene()
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Digital Tennis Front End");root.AddComponent<FrontEndController>();
            var cameraObject=new GameObject("Front End UI Camera");cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Depth;camera.cullingMask=1<<5;
            camera.nearClipPlane=.1f;camera.farClipPlane=5;camera.depth=20;camera.backgroundColor=new Color(.02f,.05f,.08f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
        public static void Validate()
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)throw new InvalidOperationException("FrontEnd scene missing; run setup.");
            if(Resources.Load<Font>("FrontEnd/Fonts/NanumGothic-Regular")==null || Resources.Load<Texture2D>("FrontEnd/Logo")==null || Resources.Load<TextAsset>("FrontEnd/WorldLand")==null)
                throw new InvalidOperationException("Front end needs the licensed Korean font, organization logo and physical world land data.");
            Debug.Log("DIGITAL_TENNIS_FRONT_END_VALIDATION_OK");
        }
    }
}
