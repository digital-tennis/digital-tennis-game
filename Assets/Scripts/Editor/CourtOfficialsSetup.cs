using UnityEditor;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class CourtOfficialsSetup
    {
        // Idempotent scene additions through the Editor API; existing scene GUIDs stay intact.
        public static void Prepare(TennisMatchController controller)
        {
            if (controller.GetComponent<CourtOfficials>() != null)
            {
                controller.transform.Find("Court Officials/Umpire Chair").localPosition = new Vector3(6.8f, 0f, 0f);
                return;
            }
            var officials = controller.gameObject.AddComponent<CourtOfficials>();
            var white = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Lines.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
            var uniform = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlayerFar.mat");
            var runnerUniform = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlayerNear.mat");
            var root = new GameObject("Court Officials").transform;
            root.SetParent(controller.transform, false);
            var chair = new GameObject("Umpire Chair").transform;
            chair.SetParent(root, false);
            chair.localPosition = new Vector3(6.8f, 0f, 0f);
            foreach (float x in new[] { -.35f, .35f })
                foreach (float z in new[] { -.4f, .4f })
                    Part(chair, "Chair Leg", PrimitiveType.Cube, new Vector3(x, 1f, z), new Vector3(.07f, 2f, .07f), white);
            Part(chair, "Seat", PrimitiveType.Cube, new Vector3(0, 2, 0), new Vector3(.9f, .12f, 1f), dark);
            Part(chair, "Chair Back", PrimitiveType.Cube, new Vector3(.4f, 2.45f, 0), new Vector3(.08f, .9f, 1f), white);
            for (int i = 0; i < 5; i++)
                Part(chair, "Ladder Rung", PrimitiveType.Cube, new Vector3(0, .25f + i * .35f, -.4f), new Vector3(.7f, .06f, .06f), white);
            Part(chair, "Umpire Body", PrimitiveType.Capsule, new Vector3(0, 2.45f, 0), new Vector3(.44f, .4f, .44f), uniform);
            Part(chair, "Umpire Head", PrimitiveType.Sphere, new Vector3(0, 3.05f, 0), Vector3.one * .38f, white);
            Part(chair, "Umpire Cap", PrimitiveType.Cube, new Vector3(0, 3.23f, 0), new Vector3(.5f, .06f, .4f), dark);
            foreach (float z in new[] { -.17f, .17f })
                Part(chair, "Umpire Leg", PrimitiveType.Cube, new Vector3(-.28f, 1.85f, z), new Vector3(.16f, .5f, .16f), dark);
            var arm = new GameObject("Umpire Signal Arm").transform;
            arm.SetParent(chair, false); arm.localPosition = new Vector3(-.2f, 2.7f, -.25f);
            Part(arm, "Arm", PrimitiveType.Capsule, new Vector3(0, -.25f, 0), new Vector3(.13f, .27f, .13f), white);

            var boy = new GameObject("Ball Boy").transform;
            boy.SetParent(root, false); boy.localPosition = new Vector3(6f, 0, -.8f);
            var body = new GameObject("Runner Body").transform;
            body.SetParent(boy, false); body.localPosition = new Vector3(0, .62f, 0);
            Part(body, "Shirt", PrimitiveType.Capsule, Vector3.zero, new Vector3(.38f, .3f, .35f), runnerUniform);
            Part(body, "Head", PrimitiveType.Sphere, new Vector3(0, .48f, 0), Vector3.one * .32f, white);
            Part(body, "Cap", PrimitiveType.Cube, new Vector3(0, .64f, .06f), new Vector3(.4f, .07f, .4f), uniform);
            foreach (float x in new[] { -.12f, .12f })
            {
                Part(body, "Leg", PrimitiveType.Capsule, new Vector3(x, -.38f, 0), new Vector3(.13f, .2f, .13f), dark);
                Part(body, "Arm", PrimitiveType.Capsule, new Vector3(x * 2, -.02f, .08f), new Vector3(.11f, .22f, .11f), white);
            }
            var serialized = new SerializedObject(officials);
            serialized.FindProperty("umpireArm").objectReferenceValue = arm;
            serialized.FindProperty("ballBoy").objectReferenceValue = boy;
            serialized.FindProperty("runnerBody").objectReferenceValue = body;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = at; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
