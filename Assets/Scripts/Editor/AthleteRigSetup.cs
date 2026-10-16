using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DigitalTennis.Editor
{
    public static class AthleteRigSetup
    {
        private const string MaterialPath="Assets/Materials/Athlete.mat";
        public static void Prepare(TennisMatchController controller)
        {
            foreach(var view in UnityEngine.Object.FindObjectsByType<CharacterPresentation>(FindObjectsSortMode.None))
            {
                PrepareRacket(view.GetComponentInParent<RacketAvatar>());
                var model=view.transform.Find("Model");
                if(model.Find("Athlete")!=null)continue;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
                var root=Joint(model,"Athlete",Vector3.zero);root.gameObject.AddComponent<AthleteMotion>();
                var body=Joint(root,"Torso",new Vector3(0,1.02f,0));
                Part(body,"Primary_Jersey",PrimitiveType.Capsule,new Vector3(0,.13f,0),new Vector3(.47f,.265f,.29f));
                Part(body,"Secondary_Shorts",PrimitiveType.Cube,new Vector3(0,-.15f,0),new Vector3(.37f,.24f,.29f));
                Part(body,"Accent_Collar",PrimitiveType.Sphere,new Vector3(0,.36f,0),new Vector3(.17f,.07f,.17f));
                Part(body,"Skin_Neck",PrimitiveType.Cylinder,new Vector3(0,.40f,0),new Vector3(.12f,.09f,.12f));
                for(int i=-1;i<=1;i+=2)
                {
                    Part(body,"Accent_Shoulder",PrimitiveType.Capsule,new Vector3(i*.19f,.24f,0),new Vector3(.10f,.16f,.29f),Quaternion.Euler(0,0,i*20));
                    Part(body,"Accent_SidePanel",PrimitiveType.Cube,new Vector3(i*.19f,.01f,.12f),new Vector3(.055f,.30f,.025f),Quaternion.Euler(0,0,-i*10));
                }
                var head=Joint(body,"Head",new Vector3(0,.54f,0));
                Part(head,"Skin_Face",PrimitiveType.Sphere,Vector3.zero,new Vector3(.30f,.36f,.285f));
                Part(head,"Skin_Jaw",PrimitiveType.Sphere,new Vector3(0,-.075f,.028f),new Vector3(.24f,.20f,.25f));
                Part(head,"Skin_Nose",PrimitiveType.Sphere,new Vector3(0,-.012f,.149f),new Vector3(.045f,.065f,.055f));
                for(int s=-1;s<=1;s+=2)
                {
                    Part(head,"Skin_Ear",PrimitiveType.Sphere,new Vector3(s*.145f,0,0),new Vector3(.055f,.09f,.06f));
                    Part(head,"White_Eye",PrimitiveType.Sphere,new Vector3(s*.062f,.035f,.132f),new Vector3(.065f,.032f,.025f));
                    Part(head,"Dark_Iris",PrimitiveType.Sphere,new Vector3(s*.060f,.034f,.145f),new Vector3(.024f,.028f,.012f));
                    Part(head,"Hair_Brow",PrimitiveType.Capsule,new Vector3(s*.062f,.070f,.134f),new Vector3(.012f,.039f,.012f),Quaternion.Euler(0,0,s*80));
                }
                Part(head,"Dark_Mouth",PrimitiveType.Capsule,new Vector3(0,-.080f,.151f),new Vector3(.009f,.032f,.009f),Quaternion.Euler(0,0,90));
                var band=Joint(head,"Headband",Vector3.zero);
                Part(band,"Accent_Band",PrimitiveType.Sphere,new Vector3(0,.096f,0),new Vector3(.31f,.060f,.295f));
                for(int style=0;style<5;style++)
                {
                    var hair=Joint(head,"HairStyle"+style,Vector3.zero);
                    Part(hair,"Hair_Crown",PrimitiveType.Sphere,new Vector3(0,.125f,-.02f),new Vector3(.315f,.14f,.28f));
                    if(style==0)Part(hair,"Hair_Ponytail",PrimitiveType.Capsule,new Vector3(0,.065f,-.21f),new Vector3(.085f,.15f,.09f),Quaternion.Euler(25,0,0));
                    if(style==1)for(int i=0;i<5;i++)Part(hair,"Hair_SweptLock",PrimitiveType.Capsule,new Vector3(-.10f+i*.048f,.18f,.02f),new Vector3(.065f,.09f,.15f),Quaternion.Euler(40,0,-25));
                    if(style==2)Part(hair,"Hair_CloseCrop",PrimitiveType.Sphere,new Vector3(0,.09f,-.025f),new Vector3(.318f,.16f,.30f));
                    if(style==3)for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;Part(hair,"Hair_Curl",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.13f,.13f,Mathf.Sin(a)*.10f),Vector3.one*.105f);}
                    if(style==4)Part(hair,"Hair_Bun",PrimitiveType.Sphere,new Vector3(0,.12f,-.16f),Vector3.one*.15f);
                    hair.gameObject.SetActive(style==0);
                }
                for(int s=-1;s<=1;s+=2)
                {
                    var arm=Joint(body,s<0?"LeftArm":"RightArm",new Vector3(s*.25f,.25f,0));
                    Part(arm,"Primary_Sleeve",PrimitiveType.Capsule,new Vector3(0,-.075f,0),new Vector3(.16f,.105f,.16f));
                    Part(arm,"Skin_UpperArm",PrimitiveType.Capsule,new Vector3(0,-.20f,0),new Vector3(.108f,.13f,.108f));
                    var elbow=Joint(arm,"Elbow",new Vector3(0,-.32f,0));
                    Part(elbow,"Skin_Forearm",PrimitiveType.Capsule,new Vector3(0,-.115f,0),new Vector3(.095f,.13f,.095f));
                    Part(elbow,"Accent_Wristband",PrimitiveType.Cylinder,new Vector3(0,-.22f,0),new Vector3(.103f,.037f,.103f));
                    Part(elbow,"Skin_Hand",PrimitiveType.Sphere,new Vector3(0,-.285f,.01f),new Vector3(.09f,.13f,.10f));
                    var leg=Joint(root,s<0?"LeftLeg":"RightLeg",new Vector3(s*.105f,.83f,0));
                    Part(leg,"Skin_Thigh",PrimitiveType.Capsule,new Vector3(0,-.15f,0),new Vector3(.145f,.18f,.155f));
                    var knee=Joint(leg,"Knee",new Vector3(0,-.34f,0));
                    Part(knee,"Skin_Calf",PrimitiveType.Capsule,new Vector3(0,-.13f,0),new Vector3(.115f,.16f,.12f));
                    Part(knee,"White_Sock",PrimitiveType.Cylinder,new Vector3(0,-.28f,0),new Vector3(.12f,.07f,.12f));
                    Part(knee,"White_Shoe",PrimitiveType.Capsule,new Vector3(0,-.36f,.07f),new Vector3(.16f,.18f,.13f),Quaternion.Euler(90,0,0));
                    Part(knee,"Accent_Sole",PrimitiveType.Cube,new Vector3(0,-.415f,.07f),new Vector3(.17f,.035f,.29f));
                }
                for(int side=-1;side<=1;side+=2)
                {
                    var label=new GameObject("Jersey Country",typeof(TextMesh));label.transform.SetParent(body,false);
                    label.transform.localPosition=new Vector3(0,.13f,side*.15f);label.transform.localRotation=Quaternion.Euler(0,side>0?180:0,0);
                    var t=label.GetComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=48;t.characterSize=.018f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text="KOR";
                    t.GetComponent<Renderer>().sharedMaterial=t.font.material;
                }
            }
        }
        private static void PrepareRacket(RacketAvatar avatar)
        {
            var pivot=avatar.transform.Find("Racket Pivot");
            if(pivot.Find("Tennis Racket Frame")!=null)return;
            foreach(var renderer in pivot.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            const string path="Assets/Art/AthleteRacket.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var triangles=new List<int>();
                for(int a=0;a<40;a++)for(int b=0;b<8;b++)
                {
                    float u=a*Mathf.PI*2/40,w=b*Mathf.PI*2/8;
                    Vector3 normal=new Vector3(Mathf.Cos(u)*Mathf.Cos(w),Mathf.Sin(u)*Mathf.Cos(w),Mathf.Sin(w));
                    v.Add(new Vector3(Mathf.Cos(u)*.40f,.80f+Mathf.Sin(u)*.47f,0)+normal*.026f);n.Add(normal);c.Add(new Color(.66f,.90f,.38f));
                    int i=a*8+b,j=((a+1)%40)*8+b,k=a*8+(b+1)%8,l=((a+1)%40)*8+(b+1)%8;
                    triangles.AddRange(new[]{i,k,j,j,k,l});
                }
                void String(Vector3 from,Vector3 to)
                {
                    Vector3 width=Vector3.Cross(to-from,Vector3.forward).normalized*.0025f;
                    int i=v.Count;v.AddRange(new[]{from-width,from+width,to+width,to-width});
                    for(int x=0;x<4;x++){n.Add(Vector3.forward);c.Add(new Color(.82f,.87f,.86f));}
                    triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3,i+2,i+1,i,i+3,i+2,i});
                }
                for(int i=-4;i<=4;i++)
                {
                    float x=i*.075f,y=.47f*Mathf.Sqrt(1-x*x/(.4f*.4f));String(new Vector3(x,.8f-y,0),new Vector3(x,.8f+y,0));
                    float h=i*.09f,extent=.4f*Mathf.Sqrt(1-h*h/(.47f*.47f));String(new Vector3(-extent,.8f+h,0),new Vector3(extent,.8f+h,0));
                }
                mesh=new Mesh{name="Original open tennis racket"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetColors(c);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var frame=new GameObject("Tennis Racket Frame",typeof(MeshFilter),typeof(MeshRenderer));frame.transform.SetParent(pivot,false);
            frame.GetComponent<MeshFilter>().sharedMesh=mesh;frame.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Stadium/Venue.mat");
            Part(pivot,"Dark_RacketGrip",PrimitiveType.Cylinder,new Vector3(0,.12f,0),new Vector3(.085f,.20f,.085f));
            Part(pivot,"Secondary_RacketThroat",PrimitiveType.Cylinder,new Vector3(0,.34f,0),new Vector3(.035f,.17f,.035f));
        }
        private static Transform Joint(Transform parent,string name,Vector3 at)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=at;return t;}
        private static void Part(Transform parent,string name,PrimitiveType type,Vector3 at,Vector3 size,Quaternion? rotation=null)
        {
            var p=GameObject.CreatePrimitive(type);p.name=name;p.transform.SetParent(parent,false);p.transform.localPosition=at;p.transform.localScale=size;p.transform.localRotation=rotation??Quaternion.identity;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(Shader.Find("Standard"));material.SetFloat("_Glossiness",.22f);AssetDatabase.CreateAsset(material,MaterialPath);}
            p.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
