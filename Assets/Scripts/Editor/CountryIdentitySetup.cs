using System;
using UnityEditor;
using UnityEngine;
namespace DigitalTennis.Editor
{
    public static class CountryIdentitySetup
    {
        public static void Prepare()
        {
            foreach(string id in CharacterCatalog.Ids)
            {
                string path="Assets/Resources/FrontEnd/Flags/"+id+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Missing flag "+id);
                importer.npotScale=TextureImporterNPOTScale.None;importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();Debug.Log("COUNTRY_IDENTITY_FLAGS_CONFIGURED_15");
        }
    }
}
