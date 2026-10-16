using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DigitalTennis.Editor
{
    /// <summary>Bakes original static venue geometry. No downloaded assets or gameplay colliders.</summary>
    public static class StadiumProjectSetup
    {
        private const string Folder="Assets/Art/Stadium";
        private static Color Hex(string text) => CharacterProfile.ColorOf(text);
        private static Mesh cube;
        public static void Prepare(TennisMatchController controller)
        {
            if(cube==null){var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=sample.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(sample);}
            var existing=UnityEngine.Object.FindFirstObjectByType<TennisStadium>();
            if(existing!=null)
            {
                foreach(var renderer in existing.GetComponentsInChildren<Renderer>())GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
                Upgrade(existing);return;
            }
            MakeFolder(Folder);
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            var root=new GameObject("Tennis Stadium");var stadium=root.AddComponent<TennisStadium>();
            if(controller.GetComponent<StadiumValidationDriver>()==null)controller.gameObject.AddComponent<StadiumValidationDriver>();
            var baseMesh=new Builder();var seatMesh=new Builder();var crowd=new Builder();var rail=new Builder();var trim=new Builder();var apron=new Builder();var led=new Builder();
            var random=new System.Random(48126);int seats=0,spectators=0;
            var white=Color.white;var concrete=Hex("#7C8D93");var steel=Hex("#273B48");
            apron.Box(new Vector3(0,-.22f,0),new Vector3(54,.16f,68),white);
            baseMesh.Box(new Vector3(0,-.48f,0),new Vector3(58,.35f,72),Hex("#1A2931"));
            // Every stand begins beyond the clear court and player run-off, on both ends.
            foreach(var stand in new[]{(new Vector3(0,0,20),0f,28f,26),(new Vector3(0,0,-20),180f,28f,26),
                (new Vector3(10,0,0),90f,32f,30),(new Vector3(-10,0,0),-90f,32f,30)})
            {
                var transform=Matrix4x4.TRS(stand.Item1,Quaternion.Euler(0,stand.Item2,0),Vector3.one);
                foreach(var builder in new[]{baseMesh,seatMesh,crowd,rail,trim})builder.Space=transform;
                for(int row=0;row<6;row++)
                {
                    float y=.45f+row*.56f,z=row*1.05f;
                    baseMesh.Box(new Vector3(0,y/2,z),new Vector3(stand.Item3,y,1.06f),concrete);
                    baseMesh.Box(new Vector3(0,y+.025f,z),new Vector3(2.1f,.05f,.98f),Hex("#C2C8BD"));
                    trim.Box(new Vector3(0,y-.12f,z-.52f),new Vector3(stand.Item3,.08f,.035f),white);
                    for(int n=0;n<stand.Item4;n++)
                    {
                        float x=(n-(stand.Item4-1)*.5f)*.96f;
                        if(Mathf.Abs(x)<1.2f)continue;
                        seats++;
                        var at=new Vector3(x,y+.17f,z);
                        seatMesh.Box(at,new Vector3(.64f,.12f,.56f),white);
                        seatMesh.Box(at+new Vector3(0,.29f,.24f),new Vector3(.64f,.48f,.10f),white,Quaternion.Euler(-9,0,0));
                        rail.Box(at-new Vector3(0,.12f,0),new Vector3(.09f,.24f,.32f),steel);
                        if(random.NextDouble()>.84)continue;
                        spectators++;
                        Color skin=new[]{Hex("#E5BE99"),Hex("#C69168"),Hex("#9C694E"),Hex("#704A37"),Hex("#F0D3B6")}[random.Next(5)];
                        Color shirt=new[]{Hex("#47788D"),Hex("#C8CEC6"),Hex("#B06555"),Hex("#BAA65D"),Hex("#536E69"),Hex("#69709C")}[random.Next(6)];
                        crowd.Box(at+new Vector3(0,.28f,-.02f),new Vector3(.41f,.52f,.25f),shirt);
                        crowd.Head(at+new Vector3(0,.72f,-.04f),new Vector3(.17f,.20f,.17f),skin);
                        crowd.Box(at+new Vector3(0,.03f,-.27f),new Vector3(.37f,.19f,.38f),Hex("#334550"));
                        bool cheering=random.Next(6)==0;
                        for(int side=-1;side<=1;side+=2)
                            crowd.Box(at+new Vector3(side*.26f,cheering?.49f:.25f,-.09f),new Vector3(.12f,.43f,.13f),shirt,
                                Quaternion.Euler(cheering?0:24,0,side*(cheering?145:12)));
                    }
                }
                baseMesh.Box(new Vector3(0,1.75f,6),new Vector3(stand.Item3+.6f,3.5f,.3f),Hex("#344B58"));
                rail.Box(new Vector3(0,4.42f,5.82f),new Vector3(stand.Item3,.055f,.055f),Hex("#A6BABD"));
                for(float x=-stand.Item3*.5f;x<=stand.Item3*.5f;x+=2)
                    rail.Box(new Vector3(x,3.95f,5.82f),new Vector3(.055f,.96f,.055f),Hex("#A6BABD"));
                // Central tunnel and a roofed concourse split the seating into readable blocks.
                baseMesh.Box(new Vector3(0,1.05f,6.3f),new Vector3(2.4f,2.1f,.15f),Hex("#091C27"));
                trim.Box(new Vector3(0,2.23f,6.15f),new Vector3(2.65f,.18f,.35f),white);
                baseMesh.Box(new Vector3(0,4.6f,6.3f),new Vector3(stand.Item3+1.4f,.22f,2.4f),Hex("#223C49"));
            }
            foreach(var builder in new[]{baseMesh,seatMesh,crowd,rail,trim})builder.Space=Matrix4x4.identity;
            // Low courtside boards, access gates, benches and planting keep the court centre quiet.
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<4;i++)
                {
                    float z=-12+i*8;
                    baseMesh.Box(new Vector3(side*8.7f,.46f,z),new Vector3(.22f,.92f,5.4f),Hex("#142F3B"));
                    trim.Box(new Vector3(side*8.56f,.82f,z),new Vector3(.03f,.06f,5.2f),white);
                }
                foreach(float x in new[]{-5.1f,5.1f})
                {
                    baseMesh.Box(new Vector3(x,.48f,side*18),new Vector3(6.5f,.96f,.22f),Hex("#142F3B"));
                    trim.Box(new Vector3(x,.85f,side*17.86f),new Vector3(6.3f,.055f,.035f),white);
                }
                for(int i=-1;i<=1;i+=2)
                {
                    var at=new Vector3(side*8.5f,0,i*4.2f);
                    baseMesh.Box(at+Vector3.up*.48f,new Vector3(.8f,.12f,2.4f),Hex("#B9C1B5"));
                    baseMesh.Box(at+new Vector3(side*.35f,.82f,0),new Vector3(.12f,.66f,2.4f),Hex("#B9C1B5"));
                    foreach(float leg in new[]{-.85f,.85f})rail.Box(at+new Vector3(0,.22f,leg),new Vector3(.55f,.44f,.12f),steel);
                }
                foreach(float x in new[]{-19f,19f})
                {
                    var at=new Vector3(x,0,side*23.5f);
                    rail.Box(at+Vector3.up*5.5f,new Vector3(.24f,11,.24f),Hex("#5C7480"));
                    baseMesh.Box(at+Vector3.up*11,new Vector3(3.1f,1.4f,.3f),Hex("#20333E"));
                    for(int i=0;i<4;i++)led.Box(at+new Vector3(-1.05f+i*.7f,11,-side*.19f),new Vector3(.45f,.92f,.07f),white);
                }
            }
            for(int side=-1;side<=1;side+=2)
                for(int end=-1;end<=1;end+=2)
                {
                    var at=new Vector3(side*18.8f,0,end*14.6f);
                    baseMesh.Box(at+Vector3.up*.35f,new Vector3(1.9f,.7f,2.1f),Hex("#465F63"));
                    for(int i=-1;i<=1;i++)crowd.Head(at+new Vector3(i*.45f,1.05f+.12f*(i+1),0),new Vector3(.6f,.65f,.7f),Hex("#37685A"));
                }
            Material surface=Material("Venue",false);Material lamps=Material("Lamps",true);
            Create(root,"Outer Plaza",apron,surface);
            Create(root,"Grandstands and Facilities",baseMesh,surface);
            Create(root,"Individual Stadium Seats",seatMesh,surface);
            var audience=Create(root,"Seated Audience",crowd,surface);audience.shadowCastingMode=ShadowCastingMode.Off;
            Create(root,"Rails and Floodlight Masts",rail,surface);
            Create(root,"National Accent Bands",trim,surface);
            Create(root,"Floodlight Panels",led,lamps);
            for(int end=-1;end<=1;end+=2)
            {
                Quaternion rotation=Quaternion.Euler(0,end>0?0:180,0);
                var board=new Builder();board.Box(new Vector3(0,6.55f,end*24.7f),new Vector3(12.4f,3.25f,.48f),Hex("#0B2330"));
                board.Box(new Vector3(0,8.25f,end*24.7f),new Vector3(12.7f,.14f,.62f),Hex("#D0E7BB"));
                Create(root,"End Scoreboard "+end,board,surface);
                Label(root,"Live Score "+end,"DIGITAL TENNIS",new Vector3(0,6.9f,end*24.42f),rotation,.14f);
                Label(root,"Venue Name "+end,"WORLD TOUR",new Vector3(0,5.42f,end*24.40f),rotation,.083f);
                foreach(float x in new[]{-5.1f,5.1f})Label(root,"Baseline Sponsor","DIGITAL TENNIS",new Vector3(x,.48f,end*17.85f),rotation,.062f);
            }
            for(int side=-1;side<=1;side+=2)
                foreach(float z in new[]{-12f,4f,12f})
                    Label(root,"Sideline Sponsor","PLAY BEYOND BORDERS",new Vector3(side*8.55f,.46f,z),Quaternion.Euler(0,side>0?-90:90,0),.038f);
            var serialized=new SerializedObject(stadium);
            serialized.FindProperty("spectatorCount").intValue=spectators;serialized.FindProperty("seatCount").intValue=seats;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Upgrade(stadium);Validate(controller);AssetDatabase.SaveAssets();
            Debug.Log($"STADIUM_PREPARED seats={seats} spectators={spectators}");
        }
        private static void Upgrade(TennisStadium stadium)
        {
            const string textPath="Assets/Resources/FrontEnd/DepthText.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(textPath)==null)AssetDatabase.CreateAsset(new Material(Shader.Find("DigitalTennis/Depth Text")),textPath);
            foreach(var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if(label.GetComponent<DepthText>()==null)label.gameObject.AddComponent<DepthText>();
            // Move both complete signs into the normal court camera frustum, without changing that camera.
            for(int end=-1;end<=1;end+=2)
            {
                stadium.transform.Find("End Scoreboard "+end).localPosition=new Vector3(0,-1.7f,-end*5.7f);
                stadium.transform.Find("Live Score "+end).localPosition=new Vector3(0,5.2f,end*18.72f);
                stadium.transform.Find("Venue Name "+end).localPosition=new Vector3(0,3.72f,end*18.70f);
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.55f,.65f,.70f);
            RenderSettings.ambientEquatorColor=new Color(.36f,.43f,.47f);
            RenderSettings.ambientGroundColor=new Color(.20f,.24f,.27f);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional){light.shadows=LightShadows.Soft;light.shadowStrength=.65f;light.color=new Color(1,.94f,.85f);}
            if(stadium.transform.Find("National Architecture")!=null)return;
            var nations=new GameObject("National Architecture");nations.transform.SetParent(stadium.transform,false);
            for(int country=0;country<15;country++)
            {
                var b=new Builder();var profile=CharacterCatalog.Get(country);
                var primary=Hex(profile.secondary);var accent=Hex(profile.accent);var stone=Color.Lerp(primary,Color.white,.50f);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Space=Matrix4x4.TRS(new Vector3(side*16.7f,4.8f,0),Quaternion.Euler(0,side*90,0),Vector3.one);
                    // Open pavilions sit above the outer concourse; sight lines and play bounds remain clear.
                    for(int i=-2;i<=2;i++)b.Box(new Vector3(i*6,1.1f,0),new Vector3(.16f,2.2f,.16f),stone);
                    if(country==0) // layered, curved roof rhythm
                        for(int i=-4;i<=4;i++)b.Box(new Vector3(i*3,2.35f+Mathf.Abs(i)*.10f,0),new Vector3(3.2f,.17f,2.1f),primary,Quaternion.Euler(0,0,i*3));
                    else if(country==1||country==10) // paper folds / sail canopy
                        for(int i=-2;i<=2;i++)for(int slope=-1;slope<=1;slope+=2)b.Box(new Vector3(i*6+slope*1.4f,2.6f,0),new Vector3(3.4f,.14f,2.5f),stone,Quaternion.Euler(0,0,slope*(country==1?24:36)));
                    else if(country==2) // gateway bays
                        for(int i=-2;i<=2;i++){b.Box(new Vector3(i*6,2.4f,0),new Vector3(5.5f,.26f,1.4f),primary);b.Box(new Vector3(i*6,2.65f,0),new Vector3(6,.12f,1.6f),accent);}
                    else if(country==3||country==6) // urban beams / exposed truss
                    {
                        b.Box(new Vector3(0,2.4f,0),new Vector3(28,.18f,1.7f),primary);
                        for(int i=-6;i<=6;i++)b.Box(new Vector3(i*2,1.9f,0),new Vector3(.12f,1.25f,.12f),accent,Quaternion.Euler(0,0,i%2==0?45:-45));
                    }
                    else if(country==4||country==11) // faceted roofs / stepped gables
                        for(int i=-2;i<=2;i++)for(int step=0;step<4;step++)b.Box(new Vector3(i*6,2.25f+step*.23f,0),new Vector3(4.8f-step*.9f,.24f,1.7f),step%2==0?primary:accent);
                    else if(country==5||country==13) // textile lattice / open geometric arches
                    {
                        for(int i=-2;i<=2;i++)
                            for(int n=0;n<7;n++){float a=n*Mathf.PI/6;b.Box(new Vector3(i*6+Mathf.Cos(a)*2.3f,1.5f+Mathf.Sin(a)*1.1f,0),new Vector3(1.2f,.12f,1.5f),stone,Quaternion.Euler(0,0,90+n*30));}
                        for(int i=-12;i<=12;i+=2)b.Box(new Vector3(i,1.3f,.7f),new Vector3(.08f,1.7f,.08f),accent,Quaternion.Euler(0,0,45));
                    }
                    else if(country==7) // brick pavilion rhythm
                        for(int i=-2;i<=2;i++){b.Box(new Vector3(i*6,2.2f,0),new Vector3(5.4f,.18f,2),stone);b.Box(new Vector3(i*6,2.4f,0),new Vector3(5,.22f,1.8f),primary);}
                    else if(country==8) // warm arcades
                        for(int i=-2;i<=2;i++)for(int n=0;n<8;n++){float a=n*Mathf.PI/7;b.Box(new Vector3(i*6+Mathf.Cos(a)*2.4f,1.2f+Mathf.Sin(a)*1.5f,0),new Vector3(1.1f,.18f,1.9f),stone,Quaternion.Euler(0,0,90+n*180f/7));}
                    else if(country==9) // asymmetric contemporary art fins
                        for(int i=-6;i<=6;i++)b.Box(new Vector3(i*2,1.8f+Mathf.Sin(i*.7f)*.3f,0),new Vector3(.18f,2.5f,2.2f),i%2==0?primary:accent,Quaternion.Euler(0,0,i*4));
                    else if(country==12) // flowing ribbon canopy
                        for(int i=-12;i<=12;i++)b.Box(new Vector3(i,2.5f+Mathf.Sin(i*.35f)*.4f,0),new Vector3(1.15f,.16f,2.2f),i%3==0?accent:primary,Quaternion.Euler(0,0,Mathf.Cos(i*.35f)*8));
                    else // terraced stone with an open shade structure
                        for(int i=-2;i<=2;i++){b.Box(new Vector3(i*6,2.3f,0),new Vector3(5.6f,.18f,2),stone);for(int j=0;j<4;j++)b.Box(new Vector3(i*6,1.4f+j*.23f,0),new Vector3(.7f-j*.1f,.2f,.7f),primary);}
                }
                var renderer=Create(nations,"Country "+country,b,Material("Venue",false));renderer.gameObject.SetActive(country==0);
            }
        }
        public static void Validate(TennisMatchController controller)
        {
            var stadium=UnityEngine.Object.FindFirstObjectByType<TennisStadium>();
            if(stadium==null||stadium.SpectatorCount<400||stadium.SeatCount<500||stadium.GetComponentsInChildren<Collider>(true).Length!=0)
                throw new InvalidOperationException("Stadium needs populated stands and must not add gameplay colliders. Run setup.");
        }
        private static Material Material(string name,bool unlit)
        {
            string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            material=new Material(Shader.Find(unlit?"Unlit/Color":"DigitalTennis/Stadium Vertex Lit"));material.color=unlit?new Color(.88f,.96f,.85f):Color.white;
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static MeshRenderer Create(GameObject root,string name,Builder b,Material material)
        {
            string path=Folder+"/"+name+".asset";var mesh=b.Bake(name);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(root.transform,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;var r=obj.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveShadows=true;
            // Geometry is already combined by material; do not duplicate it through static batching.
            return r;
        }
        private static TextMesh Label(GameObject root,string name,string text,Vector3 at,Quaternion rotation,float size)
        {
            var obj=new GameObject(name,typeof(TextMesh));obj.transform.SetParent(root.transform,false);obj.transform.SetPositionAndRotation(at,rotation);
            var t=obj.GetComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.83f,.94f,.83f);t.text=text;
            t.GetComponent<Renderer>().sharedMaterial=t.font.material;return t;
        }
        private static void MakeFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');MakeFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
        private sealed class Builder
        {
            private readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            private readonly List<Color32> colors=new List<Color32>();private readonly List<int> triangles=new List<int>();
            public Matrix4x4 Space=Matrix4x4.identity;
            public void Box(Vector3 at,Vector3 size,Color color,Quaternion? rotation=null)
            {
                var matrix=Space*Matrix4x4.TRS(at,rotation??Quaternion.identity,size);var normal=matrix.inverse.transpose;int start=vertices.Count;
                var points=cube.vertices;var directions=cube.normals;
                for(int i=0;i<points.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(points[i]));normals.Add(normal.MultiplyVector(directions[i]).normalized);colors.Add(color);}
                foreach(var index in cube.triangles)triangles.Add(start+index);
            }
            public void Head(Vector3 at,Vector3 size,Color color)
            {
                var p=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
                foreach(var t in new[]{new[]{0,4,3},new[]{0,3,5},new[]{0,5,2},new[]{0,2,4},new[]{1,3,4},new[]{1,5,3},new[]{1,2,5},new[]{1,4,2}})
                {
                    var a=Space.MultiplyPoint3x4(at+Vector3.Scale(p[t[0]],size));var b=Space.MultiplyPoint3x4(at+Vector3.Scale(p[t[1]],size));var c=Space.MultiplyPoint3x4(at+Vector3.Scale(p[t[2]],size));
                    var n=Vector3.Cross(b-a,c-a).normalized;int start=vertices.Count;
                    foreach(var v in new[]{a,b,c}){vertices.Add(v);normals.Add(n);colors.Add(color);}triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                }
            }
            public Mesh Bake(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
