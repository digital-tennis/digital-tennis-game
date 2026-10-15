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
            CourtOfficialsSetup.Prepare(controller);
            PrepareAvatar(GameObject.Find("Player 0 Placeholder"), CourtSide.Near);
            PrepareAvatar(GameObject.Find("Player 1 Placeholder"), CourtSide.Far);
            if (controller.GetComponent<PlayerInputAdapter>() == null) controller.gameObject.AddComponent<PlayerInputAdapter>();
            if (controller.GetComponent<OnlineTennisSession>() == null) controller.gameObject.AddComponent<OnlineTennisSession>();
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

        private static void PrepareAvatar(GameObject player, CourtSide side)
        {
            player.transform.rotation = Quaternion.Euler(0f, side == CourtSide.Near ? 0f : 180f, 0f);
            var avatar = player.GetComponent<RacketAvatar>();
            if (avatar == null) avatar = player.AddComponent<RacketAvatar>();
            var pivot = player.transform.Find("Racket Pivot");
            if (pivot == null)
            {
                pivot = new GameObject("Racket Pivot").transform;
                pivot.SetParent(player.transform, false);
                pivot.localPosition = new Vector3(0.8f, 0.3f, 0.3f);
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lines.mat");
                RacketPart(pivot, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f),
                    new Vector3(0.12f, 0.65f, 0.12f), material);
                RacketPart(pivot, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0f),
                    new Vector3(0.85f, 0.85f, 0.12f), material);
            }
            var serialized = new SerializedObject(avatar);
            serialized.FindProperty("racketPivot").objectReferenceValue = pivot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RacketPart(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            part.GetComponent<Collider>().enabled = false;
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
            if (UnityEngine.Object.FindFirstObjectByType<CourtOfficials>() == null)
                throw new InvalidOperationException("Court officials are missing. Run setup.");
            if (UnityEngine.Object.FindObjectsByType<RacketAvatar>(FindObjectsSortMode.None).Length != 2)
                throw new InvalidOperationException("MatchCourt needs two racket avatars. Run setup.");
            var lab = FindRoot(scene, "Ball Bounce Demo");
            if (lab != null && lab.activeSelf) throw new InvalidOperationException("Disable the standalone ball lab in the match scene.");
            if (System.Array.FindIndex(EditorBuildSettings.scenes, item => item.enabled && item.path == ScenePath) < 0)
                throw new InvalidOperationException("MatchCourt must remain enabled in build scenes.");
            Debug.Log("DIGITAL_TENNIS_MATCH_VALIDATION_OK");
        }
    }
}
