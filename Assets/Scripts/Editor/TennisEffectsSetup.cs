using System;
using UnityEditor;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class TennisEffectsSetup
    {
        public static void Prepare(TennisMatchController controller)
        {
            const string texturePath="Assets/Materials/TennisParticleSoft.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.name="Procedural soft particle";
                var pixels=new Color[32*32];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16))/16;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2));}
                texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TennisParticles.mat");
            if(material==null)
            {
                material=new Material(Shader.Find("Particles/Standard Unlit"));material.name="Tennis soft additive particles";
                material.SetTexture("_MainTex",texture);material.SetColor("_Color",Color.white);
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_ZWrite",0);material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
                AssetDatabase.CreateAsset(material,"Assets/Materials/TennisParticles.mat");
            }
            var ring=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TennisEffectRing.mat");
            if(ring==null){ring=new Material(Shader.Find("Sprites/Default"));ring.name="Tennis effect rings";AssetDatabase.CreateAsset(ring,"Assets/Materials/TennisEffectRing.mat");}
            var effects=controller.GetComponent<TennisEffects>();if(effects==null)effects=controller.gameObject.AddComponent<TennisEffects>();
            var so=new SerializedObject(effects);so.FindProperty("particles").objectReferenceValue=material;so.FindProperty("rings").objectReferenceValue=ring;so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Validate(TennisMatchController c)
        {
            if(c.GetComponent<TennisEffects>()==null)throw new InvalidOperationException("Court effects missing. Run setup.");
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TennisParticles.mat")==null)throw new InvalidOperationException("Particle material missing.");
            TennisEffectsCsv.Parse(System.IO.File.ReadAllText("Assets/StreamingAssets/Effects/effects.csv"));
        }
    }
}
