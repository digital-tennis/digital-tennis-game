using UnityEditor.SceneManagement;
using UnityEngine;
namespace DigitalTennis.Editor
{
    /// <summary>Regenerate the authored racket-hand topology without rewriting scenes.</summary>
    public static class AthleteMotionSetup
    {
        public static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SinglePlayerCourt.unity");
            AthleteSkinSetup.Prepare(Object.FindFirstObjectByType<TennisMatchController>());
            Debug.Log("ATHLETE_MOTION_GRIP_MESHES_READY");
        }
    }
}
