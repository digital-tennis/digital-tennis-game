using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Depth-test real 3D lettering so opposite jersey text cannot show through a body or wall.</summary>
    [RequireComponent(typeof(TextMesh))]
    public sealed class DepthText : MonoBehaviour
    {
        private TextMesh label;
        private Material material;
        private void Awake()
        {
            label=GetComponent<TextMesh>();var template=Resources.Load<Material>("FrontEnd/DepthText");
            var shader=template!=null?template.shader:Shader.Find("DigitalTennis/Depth Text");
            if(shader==null)return;
            material=template!=null?new Material(template):new Material(shader);GetComponent<Renderer>().sharedMaterial=material;
            Refresh(label.font);Font.textureRebuilt+=Refresh;
        }
        private void Refresh(Font font){if(material!=null&&label!=null&&font==label.font)material.mainTexture=font.material.mainTexture;}
        private void OnDestroy(){Font.textureRebuilt-=Refresh;if(material!=null)Destroy(material);}
    }
}
