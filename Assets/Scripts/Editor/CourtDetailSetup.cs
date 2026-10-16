using UnityEditor;
using UnityEngine;
namespace DigitalTennis.Editor
{
    public static class CourtDetailSetup
    {
        public static void Prepare()
        {
            const string folder="Assets/Resources/CourtArt";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources","CourtArt");
            Material("Surface","DigitalTennis/Court Surface");Material("Sky","DigitalTennis/Court Sky");Material("Scenery","DigitalTennis/Athlete Surface");
            foreach(var view in Object.FindObjectsByType<NationalCourtView>(FindObjectsSortMode.None))if(view.GetComponent<CourtAtmosphere>()==null)view.gameObject.AddComponent<CourtAtmosphere>();
        }
        private static void Material(string name,string shader)
        {string path="Assets/Resources/CourtArt/"+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)AssetDatabase.CreateAsset(new Material(Shader.Find(shader)),path);}
    }
}
