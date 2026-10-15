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
            CourtOfficialsSetup.Prepare(controller);
            CharacterProjectSetup.Prepare(controller);
            if (controller.GetComponent<PlayerInputAdapter>() == null) controller.gameObject.AddComponent<PlayerInputAdapter>();
            if (controller.GetComponent<OnlineTennisSession>() == null) controller.gameObject.AddComponent<OnlineTennisSession>();
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
                new EditorBuildSettingsScene(FrontEndProjectSetup.ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(MatchProjectSetup.ScenePath, true),
                new EditorBuildSettingsScene(ProjectSetup.ScenePath, true)
            };
            FrontEndProjectSetup.PrepareScene();
            AssetDatabase.SaveAssets();
            Validate();
            EditorSceneManager.OpenScene(FrontEndProjectSetup.ScenePath, OpenSceneMode.Single);
            Debug.Log("DIGITAL_TENNIS_SINGLE_PLAYER_SETUP_OK");
        }

        public static void Validate()
        {
            MatchProjectSetup.Validate();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            if (controller == null || controller.Mode != GameMode.SinglePlayer)
                throw new InvalidOperationException("SinglePlayerCourt must contain a single-player match controller.");
            if (controller.GetComponent<CourtOfficials>() == null)
                throw new InvalidOperationException("SinglePlayerCourt needs its umpire and ball boy. Run setup.");
            if (EditorBuildSettings.scenes.Length < 4 || EditorBuildSettings.scenes[0].path != FrontEndProjectSetup.ScenePath)
                throw new InvalidOperationException("FrontEnd must be the startup build scene, with all three gameplay labs preserved.");
            FrontEndProjectSetup.Validate();
            Debug.Log("DIGITAL_TENNIS_SINGLE_PLAYER_VALIDATION_OK");
        }
    }
}
