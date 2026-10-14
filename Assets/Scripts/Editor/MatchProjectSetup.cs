using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class MatchProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/MatchCourt.unity";

        [MenuItem("Digital Tennis/Prepare Serve And Rally Court")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BallProjectSetup.Prepare();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(ProjectSetup.ScenePath, ScenePath))
                    throw new InvalidOperationException("Could not create the match scene.");
                AssetDatabase.Refresh();
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var lab = UnityEngine.Object.FindFirstObjectByType<BallBounceDemo>();
            if (lab != null) lab.gameObject.SetActive(false);
            var ball = UnityEngine.Object.FindFirstObjectByType<BallFlight>();
            var marker = FindRoot(scene, "Landing Marker");
            var controllerObject = FindRoot(scene, "Tennis Match");
            if (controllerObject == null) controllerObject = new GameObject("Tennis Match");
            var controller = controllerObject.GetComponent<TennisMatchController>();
            if (controller == null) controller = controllerObject.AddComponent<TennisMatchController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("ball").objectReferenceValue = ball;
            serialized.FindProperty("landingMarker").objectReferenceValue = marker.transform;
            serialized.FindProperty("nearPlayer").objectReferenceValue = GameObject.Find("Player 0 Placeholder").transform;
            serialized.FindProperty("farPlayer").objectReferenceValue = GameObject.Find("Player 1 Placeholder").transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ProjectSetup.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DIGITAL_TENNIS_MATCH_SETUP_OK");
        }

        private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }

        public static void Validate()
        {
            BallProjectSetup.Validate();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (UnityEngine.Object.FindFirstObjectByType<TennisMatchController>() == null)
                throw new InvalidOperationException("Match controller is missing.");
            var lab = FindRoot(scene, "Ball Bounce Demo");
            if (lab != null && lab.activeSelf) throw new InvalidOperationException("Disable the standalone ball lab in the match scene.");
            if (EditorBuildSettings.scenes.Length < 2 || EditorBuildSettings.scenes[0].path != ScenePath)
                throw new InvalidOperationException("MatchCourt must be the startup build scene.");
            Debug.Log("DIGITAL_TENNIS_MATCH_VALIDATION_OK");
        }
    }
}
