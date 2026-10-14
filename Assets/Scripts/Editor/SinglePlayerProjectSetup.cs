using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class SinglePlayerProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/SinglePlayerCourt.unity";

        [MenuItem("Digital Tennis/Prepare Single Player Court")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MatchProjectSetup.Prepare();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(MatchProjectSetup.ScenePath, ScenePath))
                    throw new InvalidOperationException("Could not create the single-player scene.");
                AssetDatabase.Refresh();
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("mode").enumValueIndex = (int)GameMode.SinglePlayer;
            serialized.FindProperty("aiDifficulty").enumValueIndex = (int)DigitalTennis.AI.Difficulty.Medium;
            serialized.FindProperty("autoServe").boolValue = false;
            serialized.FindProperty("demoReturns").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(MatchProjectSetup.ScenePath, true),
                new EditorBuildSettingsScene(ProjectSetup.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DIGITAL_TENNIS_SINGLE_PLAYER_SETUP_OK");
        }

        public static void Validate()
        {
            MatchProjectSetup.Validate();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            if (controller == null || controller.Mode != GameMode.SinglePlayer)
                throw new InvalidOperationException("SinglePlayerCourt must contain a single-player match controller.");
            if (EditorBuildSettings.scenes.Length < 3 || EditorBuildSettings.scenes[0].path != ScenePath)
                throw new InvalidOperationException("SinglePlayerCourt must be the startup build scene.");
            Debug.Log("DIGITAL_TENNIS_SINGLE_PLAYER_VALIDATION_OK");
        }
    }
}
