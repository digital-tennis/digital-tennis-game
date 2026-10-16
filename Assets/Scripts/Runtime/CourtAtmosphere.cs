using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DigitalTennis
{
    [Serializable]public sealed class CourtArt{public string id,name,zenith,horizon,sun,landscape;public float elevation,intensity;public int motif;}
    [Serializable]internal sealed class CourtArts{public CourtArt[] courts;}
    public static class CourtArtCatalog
    {
        private static CourtArt[] data;
        private static float Triangle(float u)=>1-Mathf.Abs(Mathf.Repeat(u,2)-1);
        public static Vector2 Motif(int style,float u)
        {
            float a=u*Mathf.PI*2,r=.42f;
            switch(style)
            {
                case 0:r+=.08f*Mathf.Sin(a*2);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                case 1:return new Vector2((Triangle(u*4)-.5f)*.9f,u*1.2f-.6f);
                case 2:return new Vector2((Triangle(u*8)-.5f)*.85f,Mathf.Floor(u*8)/8*1.2f-.6f);
                case 3:return new Vector2((u-.5f)*1.2f,(Mathf.Floor(u*8)%2-.5f)*.7f);
                case 4:r=.38f/Mathf.Cos(Mathf.Repeat(a+Mathf.PI/6,Mathf.PI/3)-Mathf.PI/6);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                case 5:r=.35f+.10f*Mathf.Cos(a*6);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                case 6:return new Vector2((Triangle(u*6)-.5f),u-.5f);
                case 7:return new Vector2(Mathf.Sin(a)*(.18f+.14f*Mathf.Cos(a)),Mathf.Cos(a)*.55f);
                case 8:r=.34f+.10f*Mathf.Cos(a*4);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                case 9:r=.5f/(Mathf.Abs(Mathf.Cos(a))+Mathf.Abs(Mathf.Sin(a)));return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                case 10:return new Vector2(u*1.2f-.6f,Mathf.Sin(a)*.28f);
                case 11:return new Vector2((Mathf.Floor(Triangle(u*2)*6)/6-.5f),u*1.2f-.6f);
                case 12:return new Vector2(u-.5f,Mathf.Sin(a*2)*.35f);
                case 13:r=.34f+.12f*Mathf.Cos(a*8);return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
                default:return new Vector2((Triangle(u*10)-.5f)*.75f,u*1.15f-.575f);
            }
        }
        public static CourtArt Get(int i){if(data==null)data=JsonUtility.FromJson<CourtArts>(Resources.Load<TextAsset>("Characters/court-art").text).courts;return data[i];}
    }
    /// <summary>Original decorative scenery beyond the run-off. No colliders or match state writes.</summary>
    public sealed class CourtAtmosphere:MonoBehaviour
    {
        private Mesh landscape,motifs;private GameObject backdrop,apron;private Material sky;private int theme=-1;
        public int Theme=>theme;
        public void Configure(CharacterProfile p,Material court)
        {
            if(theme==p.style)return;theme=p.style;var art=CourtArtCatalog.Get(theme);
            court.shader=Resources.Load<Material>("CourtArt/Surface").shader;
            court.SetFloat("_Surface",p.surface=="clay"?1:p.surface=="grass"?2:0);
            if(sky==null)sky=new Material(Resources.Load<Material>("CourtArt/Sky"));
            sky.SetColor("_Zenith",CharacterProfile.ColorOf(art.zenith));sky.SetColor("_Horizon",CharacterProfile.ColorOf(art.horizon));RenderSettings.skybox=sky;if(Camera.main!=null)Camera.main.clearFlags=CameraClearFlags.Skybox;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Color.Lerp(CharacterProfile.ColorOf(art.zenith),Color.white,.62f);
            RenderSettings.ambientEquatorColor=new Color(.51f,.54f,.56f);RenderSettings.ambientGroundColor=new Color(.27f,.29f,.30f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=48;RenderSettings.fogEndDistance=100;RenderSettings.fogColor=CharacterProfile.ColorOf(art.horizon);
            foreach(var lamp in FindObjectsByType<Light>(FindObjectsSortMode.None))if(lamp.type==LightType.Directional)
            {lamp.transform.rotation=Quaternion.Euler(art.elevation,-32,0);lamp.color=CharacterProfile.ColorOf(art.sun);lamp.intensity=art.intensity;lamp.shadows=LightShadows.Soft;lamp.shadowStrength=.48f;}
            if(landscape!=null)Destroy(landscape);if(motifs!=null)Destroy(motifs);
            var scenery=new Geometry();var pattern=new Geometry();BuildLandscape(scenery,p,art);BuildMotifs(pattern,p);
            landscape=scenery.Mesh("Original "+art.landscape);motifs=pattern.Mesh("Original perimeter motif "+p.id);
            backdrop=Install(backdrop,"Distant city and nature",landscape);apron=Install(apron,"Run-off perimeter pattern",motifs);
        }
        private GameObject Install(GameObject item,string name,Mesh mesh)
        {
            if(item==null){item=new GameObject(name);item.transform.SetParent(transform,false);item.AddComponent<MeshFilter>();var r=item.AddComponent<MeshRenderer>();r.sharedMaterial=Resources.Load<Material>("CourtArt/Scenery");r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            item.GetComponent<MeshFilter>().sharedMesh=mesh;return item;
        }
        private static void BuildLandscape(Geometry g,CharacterProfile p,CourtArt art)
        {
            Color stone=Color.Lerp(CharacterProfile.ColorOf(p.secondary),CharacterProfile.ColorOf(art.horizon),.48f)*.72f,glass=Color.Lerp(CharacterProfile.ColorOf(art.zenith),Color.white,.26f),leaf=new Color(.25f,.41f,.33f),accent=Color.Lerp(CharacterProfile.ColorOf(p.accent),stone,.65f);
            for(int end=-1;end<=1;end+=2)
            {
                float z=end*37;
                for(int i=-5;i<=5;i++)
                {
                    float x=i*5.8f,h=6+(i*i+3*i+17)%7;
                    switch(p.style)
                    {
                        case 0: // Riverside skyline, restrained curved roof, long bridge rhythm.
                            g.Box(new Vector3(x,h*.5f,z),new Vector3(3.8f,h,3),stone);for(int floor=2;floor<h;floor+=2)g.Box(new Vector3(x,floor,z-end*1.51f),new Vector3(3.2f,.15f,.05f),glass);g.Roof(new Vector3(x,h,z),4.6f,1.1f,3.6f,accent);g.Box(new Vector3(x,7.4f,z-end*4),new Vector3(5.6f,.3f,.5f),glass);break;
                        case 1: // Layered pavilion roofs and garden canopies.
                            g.Box(new Vector3(x,3,z),new Vector3(3.5f,6,3.5f),stone);for(int n=0;n<3;n++)g.Roof(new Vector3(x,5+n*1.4f,z),5-n*.7f,1,4.3f-n*.6f,accent);Tree(g,x+2.4f,z-end*2,leaf);break;
                        case 2:
                            g.Box(new Vector3(x,4,z),new Vector3(3,8,3),stone);g.Box(new Vector3(x,9,z),new Vector3(5,1,4),accent);g.Box(new Vector3(x,10,z),new Vector3(3.7f,.4f,3.2f),glass);break;
                        case 3:
                            g.Box(new Vector3(x,h*.6f,z),new Vector3(4,h*1.2f,4),stone);for(int n=2;n<h;n+=2)g.Box(new Vector3(x,n,z-end*2.02f),new Vector3(3.4f,.16f,.05f),glass);g.Box(new Vector3(x+.8f,h*1.2f+1,z),new Vector3(.12f,2,.12f),glass);break;
                        case 4:
                            g.Roof(new Vector3(x,1,z),6,h,5,glass);g.Roof(new Vector3(x+2,1,z+end*5),7,h*.8f,6,stone);break;
                        case 5:
                            g.Box(new Vector3(x,3.6f,z),new Vector3(4.5f,7.2f,3),stone);for(int a=-1;a<=1;a++)g.Box(new Vector3(x+a,7.9f,z),new Vector3(.18f,2.5f,.18f),accent,Quaternion.Euler(0,0,a*30));g.Roof(new Vector3(x,7.5f,z),5,2,4,accent);break;
                        case 6:
                            g.Box(new Vector3(x,3,z),new Vector3(4.8f,6,4),stone);g.Roof(new Vector3(x,6,z),5.6f,2,4.6f,glass);g.Box(new Vector3(x,4,z-end*2.1f),new Vector3(.2f,6,.2f),accent,Quaternion.Euler(0,0,35));break;
                        case 7:
                            g.Box(new Vector3(x,3,z),new Vector3(4.7f,6,3),stone);g.Roof(new Vector3(x,6,z),5,2.6f,3.8f,accent);Tree(g,x+2.6f,z-end*3,leaf);break;
                        case 8:
                            Arcade(g,x,z,stone,accent);g.Roof(new Vector3(x,7,z),5.4f,1.4f,4.5f,accent);break;
                        case 9:
                            g.Box(new Vector3(x,3.5f,z),new Vector3(4.7f,7,3.8f),stone);g.Roof(new Vector3(x,7,z),5.2f,3,4.2f,accent);g.Box(new Vector3(x,9.6f,z),new Vector3(1.7f,.25f,2),glass);break;
                        case 10:
                            g.Roof(new Vector3(x,1.5f,z),5,6+(i+5)%3,4,glass);g.Box(new Vector3(x,1,z),new Vector3(6,.5f,5),stone);break;
                        case 11:
                            for(int n=0;n<4;n++)g.Box(new Vector3(x,1+n*1.7f,z),new Vector3(4.7f-n*.9f,2,3),n%2==0?stone:accent);break;
                        case 12:
                            g.Roof(new Vector3(x,0,z+end*5),10,9+Mathf.Sin(i)*3,9,leaf);g.Box(new Vector3(x,3,z-end*2),new Vector3(3.8f,6,3),stone);break;
                        case 13:
                            Arcade(g,x,z,stone,accent);g.Roof(new Vector3(x+1,0,z+end*7),12,5,10,Color.Lerp(stone,new Color(.75f,.52f,.28f),.5f));break;
                        default:
                            for(int n=0;n<4;n++)g.Box(new Vector3(x,1+n*2,z+end*n),new Vector3(8-n,2,6),Color.Lerp(leaf,stone,n*.22f));break;
                    }
                }
            }
        }
        private static void Tree(Geometry g,float x,float z,Color leaf){g.Box(new Vector3(x,2.5f,z),new Vector3(.4f,5,.4f),new Color(.29f,.23f,.18f));g.Roof(new Vector3(x,3,z),4,4.5f,4,leaf);}
        private static void Arcade(Geometry g,float x,float z,Color stone,Color trim){foreach(int s in new[]{-1,1})g.Box(new Vector3(x+s*1.8f,3,z),new Vector3(.7f,6,3),stone);g.Box(new Vector3(x,6.6f,z),new Vector3(4.4f,1.2f,3),trim);g.Roof(new Vector3(x,6.8f,z),4.5f,1.4f,3.3f,trim);}
        private static void BuildMotifs(Geometry g,CharacterProfile p)
        {
            Color c=Color.Lerp(CharacterProfile.ColorOf(p.accent),CharacterProfile.ColorOf(p.sky),.60f);
            // Decorative lines stay outside the doubles court/run-off, beneath the eye line.
            for(int side=-1;side<=1;side+=2)for(int j=-6;j<=6;j++)
            {
                Vector3 at=new Vector3(side*8.05f,-.014f,j*2.15f);int shape=p.style;
                int segments=64;
                Vector3 previous=Vector3.zero;
                for(int k=0;k<=segments;k++)
                {
                    float t=(float)k/segments;Vector2 outline=CourtArtCatalog.Motif(shape,t);Vector3 point=new Vector3(outline.x,0,outline.y);
                    if(k>0)g.Line(at+previous,at+point,.025f,c);previous=point;
                }
            }
        }
        private void OnDestroy(){if(landscape!=null)Destroy(landscape);if(motifs!=null)Destroy(motifs);if(sky!=null)Destroy(sky);}
        private sealed class Geometry
        {
            private readonly List<Vector3> v=new List<Vector3>();private readonly List<int> t=new List<int>();private readonly List<Color> c=new List<Color>();
            public void Box(Vector3 center,Vector3 size,Color color,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                Vector3[] points={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
                int offset=v.Count;foreach(var p in points){v.Add(center+rotation*Vector3.Scale(p,size*.5f));c.Add(color);}int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};foreach(int n in faces)t.Add(offset+n);
            }
            public void Roof(Vector3 at,float width,float height,float depth,Color color)
            {
                int start=v.Count;Vector3[] p={new Vector3(-width*.5f,0,-depth*.5f),new Vector3(width*.5f,0,-depth*.5f),new Vector3(0,height,0),new Vector3(-width*.5f,0,depth*.5f),new Vector3(width*.5f,0,depth*.5f)};foreach(var point in p){v.Add(at+point);c.Add(color);}int[] faces={0,2,1,1,2,4,4,2,3,3,2,0,0,1,4,0,4,3};foreach(int n in faces)t.Add(start+n);
            }
            public void Line(Vector3 a,Vector3 b,float width,Color color){var d=b-a;Box((a+b)*.5f,new Vector3(width,.01f,d.magnitude),color,Quaternion.LookRotation(d,Vector3.up));}
            public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
    }
}
