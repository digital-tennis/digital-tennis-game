using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DigitalTennis.Editor
{
    /// <summary>Creates the development scene without overwriting an existing scene.</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/DevelopmentCourt.unity";

        [MenuItem("Digital Tennis/Prepare Development Scene")]
        public static void Prepare()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.companyName = "Digital Tennis";
            PlayerSettings.productName = "Digital Tennis";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            if (Application.platform == RuntimePlatform.OSXEditor && Directory.Exists("/Applications/Visual Studio Code.app"))
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor("/Applications/Visual Studio Code.app");

            CreateFolder("Assets/Scenes");
            CreateFolder("Assets/Materials");
            if (!File.Exists(ScenePath))
                CreateCourt();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Validate();
            Debug.Log("DIGITAL_TENNIS_SETUP_OK");
        }

        private static void CreateCourt()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = Material("Ground", new Color(0.025f, 0.08f, 0.18f));
            var court = Material("Court", new Color(0.09f, 0.32f, 0.25f));
            var lines = Material("Lines", new Color(0.9f, 0.98f, 0.94f));
            var net = Material("Net", new Color(0.5f, 0.65f, 0.64f));
            var lime = Material("Ball", new Color(0.78f, 1f, 0.23f));
            var cyan = Material("PlayerNear", new Color(0f, 0.8f, 0.95f));
            var coral = Material("PlayerFar", new Color(1f, 0.4f, 0.38f));

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.07f, 0.15f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            cameraObject.transform.position = new Vector3(0f, 22f, -25f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            var courtRoot = new GameObject("Court").transform;
            Box("Surround", new Vector3(0f, -0.13f, 0f), new Vector3(15f, 0.2f, 31f), ground, courtRoot);
            Box("Playing Surface", new Vector3(0f, -0.045f, 0f), new Vector3(10.97f, 0.09f, 23.77f), court, courtRoot);
            foreach (var side in new[] { -1f, 1f })
            {
                Box("Singles Sideline " + side, new Vector3(side * 4.115f, 0.014f, 0f), new Vector3(0.05f, 0.025f, 23.77f), lines, courtRoot);
                Box("Baseline " + side, new Vector3(0f, 0.014f, side * 11.885f), new Vector3(8.23f, 0.025f, 0.05f), lines, courtRoot);
                Box("Service Line " + side, new Vector3(0f, 0.014f, side * 6.4f), new Vector3(8.23f, 0.025f, 0.05f), lines, courtRoot);
                Box("Center Mark " + side, new Vector3(0f, 0.014f, side * 11.7f), new Vector3(0.05f, 0.025f, 0.3f), lines, courtRoot);
            }
            Box("Center Service Line", new Vector3(0f, 0.014f, 0f), new Vector3(0.05f, 0.025f, 12.8f), lines, courtRoot);

            var netRoot = new GameObject("Net").transform;
            Box("Top Tape", new Vector3(0f, 0.94f, 0f), new Vector3(9.2f, 0.06f, 0.045f), lines, netRoot);
            foreach (var side in new[] { -1f, 1f })
                Box("Post " + side, new Vector3(side * 4.6f, 0.54f, 0f), new Vector3(0.1f, 1.08f, 0.1f), net, netRoot);
            for (int i = 0; i <= 36; i++)
                Box("Vertical Thread " + i, new Vector3(-4.5f + i * 0.25f, 0.47f, 0f), new Vector3(0.012f, 0.88f, 0.015f), net, netRoot);
            for (int i = 1; i <= 7; i++)
                Box("Horizontal Thread " + i, new Vector3(0f, i * 0.12f, 0f), new Vector3(9f, 0.012f, 0.015f), net, netRoot);

            Marker("Player 0 Placeholder", PrimitiveType.Capsule, new Vector3(0f, 0.85f, -9f), new Vector3(0.6f, 0.85f, 0.6f), cyan);
            Marker("Player 1 Placeholder", PrimitiveType.Capsule, new Vector3(0f, 0.85f, 9f), new Vector3(0.6f, 0.85f, 0.6f), coral);
            Marker("Ball Placeholder", PrimitiveType.Sphere, new Vector3(0f, 0.17f, -5f), Vector3.one * 0.24f, lime);
            new GameObject("Project Info").AddComponent<DigitalTennis.ProjectInfo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material Material(string name, Color color)
        {
            var path = "Assets/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Built-in Standard shader is unavailable.");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var item = Marker(name, PrimitiveType.Cube, position, scale, material);
            item.transform.SetParent(parent, true);
        }

        private static GameObject Marker(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(primitive);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        [MenuItem("Digital Tennis/Validate Development Project")]
        public static void Validate()
        {
            if (Application.unityVersion != "6000.3.26f1")
                throw new InvalidOperationException("Use Unity 6000.3.26f1 for this project.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("DevelopmentCourt scene is missing. Run Prepare first.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (Camera.main == null || GameObject.Find("Court") == null || GameObject.Find("Net") == null)
                throw new InvalidOperationException("The development scene needs its camera, court and net.");
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
                throw new InvalidOperationException("Enable Force Text serialization for source control.");
            Debug.Log("DIGITAL_TENNIS_VALIDATION_OK");
        }

        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/mac/DigitalTennis.app");
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/windows/DigitalTennis.exe");
        public static void BuildReleaseMac()=>Build(BuildTarget.StandaloneOSX,"Builds/release/mac/DigitalTennis.app",true);
        public static void BuildReleaseWindows()=>Build(BuildTarget.StandaloneWindows64,"Builds/release/windows/DigitalTennis.exe",true);
        [Serializable]private sealed class BuildManifest{public string configuration,platform,unity,sourceCommit;public bool development,allowDebugging,dirtyWorkingTree;public string[] scenes;}

        [MenuItem("Digital Tennis/Export Development Preview")]
        public static void ExportPreview()
        {
            Validate();
            var camera = Camera.main;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory(".work");
                File.WriteAllBytes(".work/development-preview.png", image.EncodeToPNG());
                Debug.Log("DIGITAL_TENNIS_PREVIEW_OK: .work/development-preview.png");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static string GitValue(string arguments)
        {
            using(var process=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo("git",arguments){RedirectStandardOutput=true,UseShellExecute=false}})
            {process.Start();string value=process.StandardOutput.ReadToEnd().Trim();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Cannot determine build source revision");return value;}
        }
        private static void Build(BuildTarget target, string output,bool release=false)
        {
            Validate();
            string commit=GitValue("rev-parse HEAD");bool dirty=GitValue("status --porcelain").Length>0;
            if(release&&dirty)throw new InvalidOperationException("Commit source changes before a release build");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options=release?BuildOptions.None:BuildOptions.Development|BuildOptions.AllowDebugging;
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-dt-clean-build")>=0)options|=BuildOptions.CleanBuildCache;
            var scenePaths=EditorBuildSettings.scenes.Where(item=>item.enabled).Select(item=>item.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                target = target,
                locationPathName = output,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + report.summary.result);
            bool development=(report.summary.options&BuildOptions.Development)!=0,debugging=(report.summary.options&BuildOptions.AllowDebugging)!=0;
            if(release&&(development||debugging))throw new InvalidOperationException("Release contains development/debugging flags");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-info.json"),JsonUtility.ToJson(new BuildManifest{configuration=release?"release":"development",platform=target.ToString(),unity=Application.unityVersion,sourceCommit=commit,development=development,allowDebugging=debugging,dirtyWorkingTree=dirty,scenes=scenePaths},true));
            Debug.Log("DIGITAL_TENNIS_BUILD_OK: " + output+" configuration="+(release?"release":"development"));
        }
    }

    [InitializeOnLoad]
    internal static class StartupScene
    {
        static StartupScene()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.isDirty || !string.IsNullOrEmpty(scene.path) || !File.Exists(ProjectSetup.ScenePath)) return;
                string startup = File.Exists(FrontEndProjectSetup.ScenePath) ? FrontEndProjectSetup.ScenePath : File.Exists(SinglePlayerProjectSetup.ScenePath) ? SinglePlayerProjectSetup.ScenePath :
                    File.Exists(MatchProjectSetup.ScenePath) ? MatchProjectSetup.ScenePath : ProjectSetup.ScenePath;
                EditorSceneManager.OpenScene(startup, OpenSceneMode.Single);
            };
        }
    }
}
