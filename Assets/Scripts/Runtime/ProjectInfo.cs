using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Startup marker for the development scene; gameplay is added in later tasks.</summary>
    public sealed class ProjectInfo : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("Digital Tennis development scene ready. Unity " + Application.unityVersion);
        }
    }
}
