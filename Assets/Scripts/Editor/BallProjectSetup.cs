using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalTennis.Editor
{
    public static class BallProjectSetup
    {
        [MenuItem("Digital Tennis/Prepare Ball Bounce Lab")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before editing the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ProjectSetup.Prepare();
            // Unity 6 stores the project timestep as rational ticks. Runtime Time setters
            // alone do not persist this setting when a batch Editor exits.
            var timeAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (timeAssets.Length == 0) throw new InvalidOperationException("TimeManager settings are missing.");
            var timeSettings = new SerializedObject(timeAssets[0]);
            var timestep = timeSettings.FindProperty("Fixed Timestep");
            timestep.FindPropertyRelative("m_Count").longValue = 2352000;
            timestep.FindPropertyRelative("m_Rate").FindPropertyRelative("m_Numerator").uintValue = 141120000;
            timestep.FindPropertyRelative("m_Rate").FindPropertyRelative("m_Denominator").uintValue = 1;
            timeSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timeAssets[0]);
            Time.fixedDeltaTime = BallSimulation.FixedStep;
            var ballObject = GameObject.Find("Ball Placeholder");
            if (ballObject == null) throw new InvalidOperationException("The development ball is missing.");
            var flight = ballObject.GetComponent<BallFlight>();
            if (flight == null) flight = ballObject.AddComponent<BallFlight>();
            ballObject.GetComponent<Collider>().enabled = false;
            var trail = ballObject.GetComponent<TrailRenderer>();
            if (trail == null) trail = ballObject.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.minVertexDistance = 0.03f;
            trail.startWidth = 0.075f;
            trail.endWidth = 0.01f;
            trail.startColor = new Color(0.8f, 1f, 0.2f, 0.9f);
            trail.endColor = new Color(0.8f, 1f, 0.2f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            const string trailPath = "Assets/Materials/BallTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(trailPath);
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Trail shader is missing.");
                material = new Material(shader) { name = "BallTrail" };
                AssetDatabase.CreateAsset(material, trailPath);
            }
            trail.sharedMaterial = material;

            var marker = GameObject.Find("Landing Marker");
            // Inactive objects aren't returned by GameObject.Find.
            if (marker == null)
                foreach (var root in ballObject.scene.GetRootGameObjects())
                    if (root.name == "Landing Marker") marker = root;
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Landing Marker";
                marker.transform.localScale = new Vector3(0.6f, 0.012f, 0.6f);
                marker.GetComponent<Collider>().enabled = false;
                marker.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lines.mat");
            }
            marker.SetActive(false);
            var demoObject = GameObject.Find("Ball Bounce Demo");
            if (demoObject == null) demoObject = new GameObject("Ball Bounce Demo");
            var demo = demoObject.GetComponent<BallBounceDemo>();
            if (demo == null) demo = demoObject.AddComponent<BallBounceDemo>();
            var serialized = new SerializedObject(demo);
            serialized.FindProperty("ball").objectReferenceValue = flight;
            serialized.FindProperty("landingMarker").objectReferenceValue = marker.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(ballObject.scene);
            EditorSceneManager.SaveScene(ballObject.scene, ProjectSetup.ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DIGITAL_TENNIS_BALL_SETUP_OK");
        }

        public static void Validate()
        {
            ProjectSetup.Validate();
            if (UnityEngine.Object.FindFirstObjectByType<BallFlight>() == null ||
                UnityEngine.Object.FindFirstObjectByType<BallBounceDemo>() == null)
                throw new InvalidOperationException("Prepare the ball bounce lab first.");
            if (Mathf.Abs(Time.fixedDeltaTime - BallSimulation.FixedStep) > 0.000001f)
                throw new InvalidOperationException("The ball simulation requires a 60Hz Fixed Timestep.");
            Debug.Log("DIGITAL_TENNIS_BALL_VALIDATION_OK");
        }
    }
}
