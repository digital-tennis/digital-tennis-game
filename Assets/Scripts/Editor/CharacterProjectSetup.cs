using UnityEditor;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class CharacterProjectSetup
    {
        public static void Prepare(TennisMatchController controller)
        {
            if (controller.GetComponent<CharacterMatchSystem>() != null)
            { PrepareOutlines(GameObject.Find("National Court Scenery").GetComponent<NationalCourtView>()); return; }
            var system = controller.gameObject.AddComponent<CharacterMatchSystem>();
            var a = Avatar(GameObject.Find("Player 0 Placeholder")); var b = Avatar(GameObject.Find("Player 1 Placeholder"));
            var stageRoot = new GameObject("National Court Scenery");
            var stage = stageRoot.AddComponent<NationalCourtView>();
            var props = new Transform[9];
            for (int i = 0; i < props.Length; i++) props[i] = Part(stageRoot.transform, "Perimeter Motif " + i, PrimitiveType.Cube, Vector3.zero, Vector3.one).transform;
            var serialized = new SerializedObject(stage);
            serialized.FindProperty("court").objectReferenceValue = GameObject.Find("Playing Surface").GetComponent<Renderer>();
            serialized.FindProperty("surround").objectReferenceValue = GameObject.Find("Surround").GetComponent<Renderer>();
            var array = serialized.FindProperty("scenery"); array.arraySize = props.Length;
            for (int i = 0; i < props.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = props[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrepareOutlines(stage);
            serialized = new SerializedObject(system);
            serialized.FindProperty("nearView").objectReferenceValue = a; serialized.FindProperty("farView").objectReferenceValue = b;
            serialized.FindProperty("courtView").objectReferenceValue = stage; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void PrepareOutlines(NationalCourtView stage)
        {
            var serialized = new SerializedObject(stage); var array = serialized.FindProperty("outlines");
            if (array.arraySize > 0) return;
            array.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                var line = new GameObject("Court Signature Outline " + i).AddComponent<LineRenderer>(); line.transform.SetParent(stage.transform, false);
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CharacterEnergy.mat");
                line.useWorldSpace = false; line.widthMultiplier = .06f;
                array.GetArrayElementAtIndex(i).objectReferenceValue = line;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static CharacterPresentation Avatar(GameObject player)
        {
            player.GetComponent<Renderer>().enabled = false;
            var root = new GameObject("Original Character Rig").transform;
            root.SetParent(player.transform, false); root.localPosition = new Vector3(0, -1, 0);
            root.localScale = new Vector3(1 / .6f, 1 / .85f, 1 / .6f);
            var view = root.gameObject.AddComponent<CharacterPresentation>();
            var model = new GameObject("Model").transform; model.SetParent(root, false);
            var body = new GameObject("Torso Pivot").transform; body.SetParent(model, false);
            var shirt = Part(body, "Training Jacket", PrimitiveType.Capsule, new Vector3(0, .95f, 0), new Vector3(.44f, .36f, .28f));
            var legs = Part(model, "Trousers", PrimitiveType.Cube, new Vector3(0, .47f, 0), new Vector3(.34f, .65f, .26f));
            for (int i = -1; i <= 1; i += 2) Part(model, "Shoe", PrimitiveType.Cube, new Vector3(i * .12f, .08f, .08f), new Vector3(.18f, .15f, .38f));
            var head = new GameObject("Head Pivot").transform; head.SetParent(body, false); head.localPosition = new Vector3(0, 1.52f, 0);
            var skin = Part(head, "Head", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .32f);
            var hair = Part(head, "Hair Or Headwear", PrimitiveType.Sphere, new Vector3(0, .13f, 0), new Vector3(.37f, .12f, .37f));
            var trim = Part(body, "Costume Accent", PrimitiveType.Cube, new Vector3(0, 1.09f, -.16f), new Vector3(.49f, .09f, .03f));
            var arms = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var arm = new GameObject("Arm Pivot " + i).transform; arm.SetParent(body, false); arm.localPosition = new Vector3(i == 0 ? -.27f : .27f, 1.18f, 0);
                Part(arm, "Sleeve", PrimitiveType.Capsule, new Vector3(0, -.24f, 0), new Vector3(.13f, .25f, .13f)); arms[i] = arm;
            }
            var lines = new LineRenderer[8];
            const string energyPath = "Assets/Materials/CharacterEnergy.mat";
            var energy = AssetDatabase.LoadAssetAtPath<Material>(energyPath);
            if (energy == null) { energy = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(energy, energyPath); }
            for (int i = 0; i < lines.Length; i++)
            {
                var line = new GameObject("Signature Ribbon " + i).AddComponent<LineRenderer>(); line.transform.SetParent(root, false);
                line.sharedMaterial = energy;
                line.useWorldSpace = false; line.positionCount = 40; line.widthMultiplier = .018f + i * .002f; line.enabled = false; lines[i] = line;
            }
            var s = new SerializedObject(view);
            void Set(string n, Object value) => s.FindProperty(n).objectReferenceValue = value;
            Set("torso", body); Set("leftArm", arms[0]); Set("rightArm", arms[1]); Set("head", head); Set("model", model);
            Set("racket", player.transform.Find("Racket Pivot")); Set("shirt", shirt.GetComponent<Renderer>()); Set("trousers", legs.GetComponent<Renderer>());
            Set("skin", skin.GetComponent<Renderer>()); Set("trim", trim.GetComponent<Renderer>()); Set("hair", hair.GetComponent<Renderer>());
            var array = s.FindProperty("ribbons"); array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
            s.ApplyModifiedPropertiesWithoutUndo(); return view;
        }
        private static GameObject Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(primitive); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lines.mat");
            Object.DestroyImmediate(part.GetComponent<Collider>()); return part;
        }
    }
}
