using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DigitalTennis.Editor
{
    /// <summary>Reproducible original skinned meshes, stitched limb rings and authored individual silhouettes.</summary>
    public static class AthleteSkinSetup
    {
        public static void Diagnose()
        {
            var mesh=Resources.Load<Mesh>("Athletes/KOR");var normals=mesh.normals;var vertices=mesh.vertices;var weights=mesh.boneWeights;int count=0;
            for(int i=0;i<normals.Length;i++)if(normals[i].sqrMagnitude<.1f){if(count<12)Debug.Log("SKIN_NORMAL index="+i+" pos="+vertices[i].ToString("F6")+" bone="+weights[i].boneIndex0);count++;}
            Debug.Log("SKIN_BAD_NORMAL_COUNT="+count);
        }
        public static void Prepare(TennisMatchController controller)
        {
            const string folder="Assets/Resources/Athletes";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Resources","Athletes");
            const string materialPath="Assets/Materials/AthleteSkin.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("DigitalTennis/Athlete Surface"));AssetDatabase.CreateAsset(material,materialPath);}
            foreach(var motion in UnityEngine.Object.FindObjectsByType<AthleteMotion>(FindObjectsSortMode.None))
            {
                var root=motion.transform;Sockets(root);var bones=AthleteSkin.BonePaths.Select(p=>p==""?root:root.Find(p)).ToArray();
                var child=root.Find("Skinned Athlete");if(child==null){child=new GameObject("Skinned Athlete").transform;child.SetParent(root,false);}
                var renderer=child.GetComponent<SkinnedMeshRenderer>();if(renderer==null)renderer=child.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMaterial=material;renderer.bones=bones;renderer.rootBone=root;renderer.updateWhenOffscreen=true;renderer.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(6,6,6));
                var skin=root.GetComponent<AthleteSkin>();if(skin==null)skin=root.gameObject.AddComponent<AthleteSkin>();
                var serialized=new SerializedObject(skin);serialized.FindProperty("surface").objectReferenceValue=renderer;serialized.ApplyModifiedPropertiesWithoutUndo();
                for(int i=0;i<15;i++)
                {
                    var style=AthleteStyleCatalog.Get(i);string path=folder+"/"+style.id+".asset";
                    string version=style.id+"-cc0-v4-"+Hash128.Compute(JsonUtility.ToJson(style)).ToString().Substring(0,8);
                    var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(asset!=null&&asset.name==version)continue;
                    AthleteSkin.ApplyRestShape(root,style);
                    var mesh=new Sculptor(root,bones,style,CharacterCatalog.Get(i)).Build();mesh.name=version;
                    if(asset==null)AssetDatabase.CreateAsset(mesh,path);else{CopyMesh(mesh,asset);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);}
                }
                AthleteSkin.ApplyRestShape(root,AthleteStyleCatalog.Get(0));renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/KOR.asset");
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!(r is SkinnedMeshRenderer))r.enabled=false;
                foreach(var label in root.GetComponentsInChildren<TextMesh>(true))
                {var p=label.transform.localPosition;p.z=Mathf.Sign(p.z)*.179f;label.transform.localPosition=p;}
            }
            AssetDatabase.SaveAssets();
        }
        private static void CopyMesh(Mesh source,Mesh target)
        {
            target.Clear();target.ClearBlendShapes();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.colors=source.colors;target.normals=source.normals;
            target.boneWeights=source.boneWeights;target.bindposes=source.bindposes;target.SetTriangles(source.triangles,0);target.bounds=source.bounds;target.name=source.name;
            for(int shape=0;shape<source.blendShapeCount;shape++)
            {var delta=new Vector3[source.vertexCount];source.GetBlendShapeFrameVertices(shape,0,delta,null,null);target.AddBlendShapeFrame(source.GetBlendShapeName(shape),100,delta,null,null);}
        }
        private static void Sockets(Transform root)
        {
            foreach(string path in new[]{"Torso/RightArm/Elbow","Torso/LeftArm/Elbow","RightLeg/Knee","LeftLeg/Knee"})
            {
                var parent=root.Find(path);bool hand=path.Contains("Arm");string name=hand?"Hand Socket":"Foot Socket";
                if(parent.Find(name)!=null)continue;var socket=new GameObject(name).transform;socket.SetParent(parent,false);socket.localPosition=parent.Find(hand?"Skin_Hand":"Accent_Sole").localPosition;
            }
        }
        [Serializable]private sealed class AcquiredVertex {public float x,y,z,r,g,b;public int kind;}
        [Serializable]private sealed class AcquiredGeometry {public AcquiredVertex[] vertices;public int[] triangles;}
        private sealed class Sculptor
        {
            private readonly Transform root;private readonly Transform[] bones;private readonly AthleteStyle s;private readonly CharacterProfile p;
            private readonly List<Vector3> v=new List<Vector3>();private readonly List<Color> colors=new List<Color>();private readonly List<BoneWeight> weights=new List<BoneWeight>();private readonly List<int> triangles=new List<int>();
            private readonly List<int> eyes=new List<int>(),brows=new List<int>(),mouth=new List<int>();
            private readonly Color primary,secondary,accent,skin,hair,dark=new Color(.035f,.045f,.055f),white=new Color(.92f,.95f,.91f);
            public Sculptor(Transform at,Transform[] joints,AthleteStyle style,CharacterProfile profile)
            {root=at;bones=joints;s=style;p=profile;primary=CharacterProfile.ColorOf(p.primary);secondary=CharacterProfile.ColorOf(p.secondary);accent=CharacterProfile.ColorOf(p.accent);skin=CharacterProfile.ColorOf(p.skin);hair=Color.Lerp(secondary,new Color(.025f,.018f,.016f),.88f);}
            private static BoneWeight W(int a,int b=-1,float blend=0)
            {return new BoneWeight{boneIndex0=a,weight0=b<0?1:1-blend,boneIndex1=b<0?0:b,weight1=b<0?0:blend};}
            private int Vertex(Vector3 at,Color color,BoneWeight weight){int index=v.Count;v.Add(at);colors.Add(color);weights.Add(weight);return index;}
            private void Tri(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
            private Vector3 Bone(int i)=>root.InverseTransformPoint(bones[i].position);
            private void Rings(Vector3 center,float[] y,float[] x,float[] z,Func<int,Color> color,Func<int,BoneWeight> weight,int segments=20)
            {
                int start=v.Count;
                for(int row=0;row<y.Length;row++)for(int j=0;j<segments;j++)
                {float a=j*Mathf.PI*2/segments;Vertex(center+new Vector3(x[row]*Mathf.Cos(a),y[row],z[row]*Mathf.Sin(a)),color(row),weight(row));}
                for(int row=0;row<y.Length-1;row++)for(int j=0;j<segments;j++)
                {int a=start+row*segments+j,b=start+row*segments+(j+1)%segments;Tri(a,a+segments,b);Tri(b,a+segments,b+segments);}
                int bottom=Vertex(center+Vector3.up*y[0],color(0),weight(0)),top=Vertex(center+Vector3.up*y[y.Length-1],color(y.Length-1),weight(y.Length-1));
                for(int j=0;j<segments;j++){Tri(bottom,start+j,start+(j+1)%segments);int a=start+(y.Length-1)*segments;Tri(top,a+(j+1)%segments,a+j);}
            }
            private void Ellipsoid(Vector3 center,Vector3 size,Color color,int bone,int latitude=8,int segments=12)
            {
                var y=new float[latitude+1];var x=new float[y.Length];var z=new float[y.Length];
                for(int i=0;i<=latitude;i++){float a=-Mathf.PI*.5f+Mathf.PI*i/latitude;y[i]=Mathf.Sin(a)*size.y*.5f;x[i]=Mathf.Max(.001f,Mathf.Cos(a)*size.x*.5f);z[i]=Mathf.Max(.001f,Mathf.Cos(a)*size.z*.5f);}
                Rings(center,y,x,z,_=>color,_=>W(bone),segments);
            }
            private Vector3 UniformPoint(float x,float y)
            {
                float[] ys={.82f,.88f,.96f,1.10f,1.23f,1.31f,1.36f,1.40f},xs={s.hips*.47f,s.hips*.48f,s.waist*.5f,s.shoulders*.43f,s.shoulders*.48f,s.shoulders*.5f,s.shoulders*.35f,.067f},zs={.13f,.137f,.118f,.148f,.155f,.13f,.10f,.067f};
                int row=0;while(row<ys.Length-2&&y>ys[row+1])row++;float t=Mathf.InverseLerp(ys[row],ys[row+1],y),rx=Mathf.Lerp(xs[row],xs[row+1],t),rz=Mathf.Lerp(zs[row],zs[row+1],t);
                x=Mathf.Clamp(x,-rx*.98f,rx*.98f);return new Vector3(x,y,rz*Mathf.Sqrt(Mathf.Max(.001f,1-x*x/(rx*rx)))+.003f);
            }
            private void Patch(Vector3[] points,Color color,int bone)
            {
                Vector3 center=Vector3.zero;foreach(var at in points)center+=at;center/=points.Length;if(bone==1)center=UniformPoint(center.x,center.y);
                if(bone==1)
                {
                    // Draped panels need tessellation: a broad flat fan sinks through the curved shirt.
                    void Panel(Vector3 a,Vector3 b,Vector3 c,int depth)
                    {
                        if(depth>0)
                        {
                            var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;
                            Panel(a,ab,ca,depth-1);Panel(ab,b,bc,depth-1);Panel(ca,bc,c,depth-1);Panel(ab,bc,ca,depth-1);return;
                        }
                        foreach(bool reverse in new[]{false,true})
                        {
                            int at=v.Count;
                            foreach(var q in new[]{a,b,c})Vertex(UniformPoint(q.x,q.y)+Vector3.forward*.002f,color,W(bone));
                            if(reverse)Tri(at,at+2,at+1);else Tri(at,at+1,at+2);
                        }
                    }
                    for(int i=0;i<points.Length;i++)Panel(center,points[i],points[(i+1)%points.Length],2);
                    return;
                }
                // Separate front/back vertices keep normals valid instead of cancelling them to zero.
                foreach(bool reverse in new[]{false,true})
                {
                    int middle=Vertex(center,color,W(bone)),start=v.Count;
                    foreach(var at in points)Vertex(bone==1?UniformPoint(at.x,at.y):at,color,W(bone));
                    for(int i=0;i<points.Length;i++)if(reverse)Tri(middle,start+(i+1)%points.Length,start+i);else Tri(middle,start+i,start+(i+1)%points.Length);
                }
            }
            private void Line(Vector3 a,Vector3 b,float width,Color color,int bone)
            {
                int segments=bone==1?Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.025f)):1;
                var d=(b-a).normalized;var side=Vector3.Cross(d,Vector3.forward).normalized*width*.5f;
                for(int i=0;i<segments;i++){Vector3 from=Vector3.Lerp(a,b,i/(float)segments),to=Vector3.Lerp(a,b,(i+1)/(float)segments);Patch(new[]{from-side,from+side,to+side,to-side},color,bone);}
            }
            private void Box(Vector3 at,Vector3 scale,Color color,int bone)
            {
                Vector3[] q={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
                int start=v.Count;foreach(var x in q)Vertex(at+Vector3.Scale(x,scale*.5f),color,W(bone));
                int[] indices={0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4};for(int i=0;i<indices.Length;i+=3)Tri(start+indices[i],start+indices[i+1],start+indices[i+2]);
            }
            public Mesh Build()
            {
                Body();Arms();Legs();Face();Hair();Costume();
                var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(v);mesh.SetColors(colors);mesh.boneWeights=weights.ToArray();mesh.SetTriangles(triangles,0);
                mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                Morph(mesh,"Focus",brows,i=>new Vector3(0,-.012f,0));
                Morph(mesh,"Smile",mouth,i=>new Vector3(0,Mathf.Abs(v[i].x)>.015f?.015f:0,0));
                Morph(mesh,"Shout",mouth,i=>new Vector3(v[i].x*.12f,Mathf.Clamp((v[i].y-1.493f)*2,-.025f,.025f),0));
                Morph(mesh,"Blink",eyes,i=>new Vector3(0,(1.603f-v[i].y)*.70f,0));
                return mesh;
            }
            private void Morph(Mesh mesh,string name,List<int> indices,Func<int,Vector3> delta)
            {var d=new Vector3[v.Count];foreach(int i in indices)d[i]=delta(i);mesh.AddBlendShapeFrame(name,100,d,null,null);}
            private void Body()
            {
                Rings(Vector3.zero,new[]{.82f,.88f,.96f,1.10f,1.23f,1.31f,1.36f,1.40f},new[]{s.hips*.47f,s.hips*.48f,s.waist*.5f,s.shoulders*.43f,s.shoulders*.48f,s.shoulders*.5f,s.shoulders*.35f,.067f},new[]{.13f,.137f,.118f,.148f,.155f,.13f,.10f,.067f},r=>r==0?secondary:primary,r=>r<3?W(0,1,r/3f):W(1));
                Rings(Vector3.zero,new[]{1.37f,1.43f,1.48f},new[]{.065f,.061f,.06f},new[]{.063f,.060f,.057f},_=>skin,_=>W(1,2,.6f),16);
                if(s.lower=="skort")Rings(Vector3.zero,new[]{.65f,.71f,.85f,.90f},new[]{s.hips*.60f,s.hips*.57f,s.hips*.48f,s.hips*.46f},new[]{.165f,.157f,.134f,.132f},r=>r==0?accent:secondary,_=>W(0));
                else foreach(int side in new[]{-1,1})Rings(new Vector3(side*s.hips*.28f,0,0),new[]{.62f,.69f,.85f,.89f},new[]{.10f,.11f,.115f,.10f},new[]{.11f,.12f,.13f,.12f},r=>r==0?accent:secondary,_=>W(side>0?9:12));
            }
            private void Arms()
            {
                foreach(int side in new[]{-1,1})
                {
                    int upper=side>0?3:6,lower=side>0?4:7,handBone=side>0?5:8;Vector3 shoulder=Bone(upper);float radius=s.armRadius;
                    Rings(new Vector3(shoulder.x,0,0),new[]{.95f,1.00f,1.12f,1.23f,1.29f},new[]{radius*.76f,radius*.83f,radius*1.02f,radius*1.15f,radius*.9f},new[]{radius*.78f,radius*.85f,radius*1.05f,radius*1.16f,radius*.92f},r=>r>=3&&s.motif!=7?primary:skin,r=>r==0?W(upper,lower,.5f):W(upper),16);
                    Rings(new Vector3(shoulder.x,0,.006f),new[]{.665f,.70f,.80f,.91f,.95f},new[]{radius*.60f,radius*.61f,radius*.76f,radius*.78f,radius*.76f},new[]{radius*.61f,radius*.62f,radius*.78f,radius*.80f,radius*.78f},r=>s.sleeve>.4f?primary:skin,r=>r==4?W(upper,lower,.5f):r==0?W(lower,handBone,.4f):W(lower),16);
                    Rings(new Vector3(shoulder.x,0,.006f),new[]{.70f,.74f,.76f},new[]{radius*.64f,radius*.66f,radius*.67f},new[]{radius*.65f,radius*.67f,radius*.68f},_=>accent,_=>W(lower),16);
                    Vector3 h=Bone(handBone);Ellipsoid(h,new Vector3(.085f,.12f,.09f),skin,handBone);Ellipsoid(h+new Vector3(side*.04f,.005f,.025f),new Vector3(.035f,.075f,.04f),skin,handBone,6,8);
                    for(int i=0;i<3;i++)Line(h+new Vector3(-.027f+i*.022f,-.01f,.043f),h+new Vector3(-.027f+i*.022f,-.035f,.043f),.002f,Color.Lerp(skin,dark,.25f),handBone);
                }
            }
            private void Legs()
            {
                foreach(int side in new[]{-1,1})
                {
                    int hip=side>0?9:12,knee=side>0?10:13,foot=side>0?11:14;float x=Bone(hip).x,r=s.legRadius;
                    Rings(new Vector3(x,0,.005f),new[]{.49f,.55f,.70f,.82f},new[]{r*.72f,r*.82f,r*1.04f,r*1.05f},new[]{r*.76f,r*.88f,r*1.10f,r*1.08f},row=>s.lower=="leggings"||row>=2?secondary:skin,row=>row==0?W(hip,knee,.5f):W(hip),16);
                    Rings(new Vector3(x,0,.01f),new[]{.15f,.22f,.36f,.45f,.49f},new[]{r*.51f,r*.56f,r*.77f,r*.74f,r*.72f},new[]{r*.58f,r*.62f,r*.82f,r*.77f,r*.76f},row=>row<2?white:s.lower=="leggings"?secondary:skin,row=>row==4?W(hip,knee,.5f):W(knee),16);
                    Vector3 at=Bone(foot);Box(at,new Vector3(.18f,.035f,.32f),accent,foot);Ellipsoid(at+Vector3.up*.055f,new Vector3(.17f,.13f,.31f),white,foot);
                    for(int lace=0;lace<3;lace++)Box(at+new Vector3(0,.113f,-.02f+lace*.034f),new Vector3(.085f,.006f,.008f),secondary,foot);
                }
            }
            private void Face()
            {
                var source=JsonUtility.FromJson<AcquiredGeometry>(Resources.Load<TextAsset>("Characters/Acquired/"+s.acquiredHead).text);
                int start=v.Count;
                foreach(var point in source.vertices)
                {
                    var at=new Vector3(point.x*s.faceWidth,point.y,point.z);
                    if(at.y<1.50f)
                    {
                        float t=Mathf.InverseLerp(1.46f,1.50f,at.y);
                        at.x=Mathf.Lerp(Mathf.Clamp(at.x,-.060f,.060f),at.x,t);
                        at.z=Mathf.Lerp(Mathf.Clamp(at.z,-.055f,.060f),at.z,t);
                    }
                    // Individually authored jaw taper and nose projection applied to acquired topology.
                    if(at.y<1.53f)at.x*=Mathf.Lerp(.88f,1.08f,Mathf.InverseLerp(.105f,.145f,s.jawWidth));
                    if(at.z>.095f&&Mathf.Abs(at.x)<.025f&&at.y>1.53f&&at.y<1.61f)at.z+=(s.noseLength-.025f)*.6f;
                    Color color=skin;
                    if(point.kind==1)
                    {
                        // UV-sampled source eyes retain iris/pupil detail, independent of skin palette.
                        color=new Color(point.r,point.g,point.b);
                    }
                    else if(point.kind==2)color=hair;
                    else
                    {
                        float shade=Mathf.Clamp((point.r+point.g+point.b)/1.45f,.74f,1.16f);
                        color=skin*shade;color.a=1;
                        if(at.z>.07f&&at.y>1.47f&&at.y<1.52f&&Mathf.Abs(at.x)<.045f)
                            color=Color.Lerp(color,new Color(.44f,.18f,.16f),.30f);
                    }
                    int index=Vertex(at,color,W(2));
                    if(point.kind==2)brows.Add(index);
                    if(point.kind==1)eyes.Add(index);
                    if(point.kind==0&&at.z>.07f&&at.y>1.47f&&at.y<1.52f&&Mathf.Abs(at.x)<.045f)mouth.Add(index);
                }
                // glTF and this game's authored rig both use +Z facing; keep source triangle winding.
                for(int i=0;i<source.triangles.Length;i+=3)Tri(start+source.triangles[i],start+source.triangles[i+1],start+source.triangles[i+2]);
            }
            private void AcquiredHair()
            {
                string name=s.acquiredHair;bool female=name=="Hair_Long"||name=="Hair_Buns"||name=="Hair_BuzzedFemale";
                float cut=female?1.48f:1.53f,top=female?1.7666439f:1.8100767f;
                var source=JsonUtility.FromJson<AcquiredGeometry>(Resources.Load<TextAsset>("Characters/Acquired/"+name).text);int start=v.Count;
                foreach(var point in source.vertices)
                {
                    var at=new Vector3(point.x*(s.faceWidth/.19f),1.40f+(point.y-cut)/(top-cut)*.335f,point.z*1.15f);
                    if(s.motif==0&&at.y<1.61f){at.x*=.65f;at.z-=.035f;}
                    if(s.motif==9&&at.x>0&&at.y<1.60f)at.y+=.07f;
                    if(s.motif==7)at.x*=.93f;
                    Vertex(at,hair,W(2));
                }
                for(int i=0;i<source.triangles.Length;i+=3)Tri(start+source.triangles[i],start+source.triangles[i+1],start+source.triangles[i+2]);
            }
            private void Hair()
            {
                AcquiredHair();
                Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
                void Lock(float x,float y,float z,float sx,float sy,float sz)=>Ellipsoid(V(x,y,z),V(sx,sy,sz),hair,2,6,10);
                switch(s.motif)
                {
                    case 0:Lock(0,1.59f,-.19f,.075f,.19f,.07f);Line(V(-.045f,1.68f,-.145f),V(.05f,1.66f,-.17f),.020f,accent,2);break;
                    case 2:for(int i=0;i<4;i++)Lock(0,1.64f-i*.04f,-.16f,.045f,.05f,.05f);break;
                    case 3:for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Lock(Mathf.Cos(a)*.06f,1.75f+Mathf.Sin(a)*.03f,-.13f,.055f,.06f,.06f);}break;
                    case 5:Lock(-.035f,1.76f,.01f,.10f,.065f,.09f);break;
                    case 8:for(int i=0;i<6;i++)Lock(Mathf.Sin(i*2)*.012f,1.65f-i*.035f,-.17f-i*.009f,.045f,.05f,.05f);break;
                    case 10:Lock(.035f,1.76f,0,.12f,.05f,.11f);break;
                    case 11:for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)Lock(side*.12f,1.62f-i*.035f,-.10f,.04f,.05f,.04f);break;
                    case 12:for(int i=0;i<16;i++){float a=i*2.399f,r=.022f*Mathf.Sqrt(i);Lock(Mathf.Cos(a)*r,1.74f,-.02f+Mathf.Sin(a)*r,.045f,.055f,.045f);}break;
                    case 13:Lock(0,1.73f,-.16f,.12f,.10f,.09f);break;
                    case 14:for(int i=0;i<12;i++){float a=i*2.399f,r=.025f*Mathf.Sqrt(i);Lock(Mathf.Cos(a)*r,1.75f,-.02f+Mathf.Sin(a)*r,.032f,.08f,.032f);}break;
                }
            }
            private void Costume()
            {
                // Constructed collar/lapel layers, raised seam tapes and separate cuff piping.
                foreach(int side in new[]{-1,1})
                {
                    if(s.collar==1||s.collar==3)
                        Patch(new[]{UniformPoint(side*.06f,1.385f),UniformPoint(side*.14f,1.34f),UniformPoint(side*.055f,1.275f)},secondary,1);
                    if(s.collar==2)
                        Line(UniformPoint(side*.065f,1.38f),UniformPoint(0,1.27f),.018f,secondary,1);
                    float shoulder=Bone(side>0?3:6).x;
                    Rings(new Vector3(shoulder,0,0),new[]{1.19f,1.205f,1.225f,1.24f},new[]{s.armRadius*1.12f,s.armRadius*1.18f,s.armRadius*1.18f,s.armRadius*1.12f},new[]{s.armRadius*1.13f,s.armRadius*1.19f,s.armRadius*1.19f,s.armRadius*1.13f},row=>row==1?accent:secondary,_=>W(side>0?3:6),16);
                    Line(UniformPoint(side*s.waist*.44f,.97f),UniformPoint(side*s.shoulders*.43f,1.24f),.008f,secondary,1);
                }
                if(s.motif==0||s.motif==9)
                {
                    Patch(new[]{UniformPoint(-.18f,1.28f),UniformPoint(-.06f,1.33f),UniformPoint(.16f,1.02f),UniformPoint(.10f,.96f)},secondary,1);
                    Line(UniformPoint(-.06f,1.33f),UniformPoint(.16f,1.02f),.011f,accent,1);
                }
                // Each persona has authored seam routes; all are original sportswear motifs.
                Vector3 P(float x,float y)=>UniformPoint(x,y);
                switch(s.motif)
                {
                    case 0:Line(P(-.17f,1.32f),P(.12f,1.03f),.025f,accent,1);Line(P(.17f,1.32f),P(-.10f,1.08f),.014f,secondary,1);break;
                    case 1:Line(P(-.16f,1.30f),P(-.05f,1.10f),.014f,accent,1);Line(P(-.05f,1.10f),P(.16f,1.25f),.014f,accent,1);break;
                    case 2:for(int i=0;i<4;i++)Line(P(-.17f+i*.015f,.99f),P(.10f+i*.01f,1.30f),.010f,i%2==0?accent:secondary,1);break;
                    case 3:Line(P(-.19f,1.27f),P(0,1.13f),.033f,accent,1);Line(P(0,1.13f),P(.19f,1.27f),.033f,accent,1);break;
                    case 4:for(int i=0;i<4;i++)Line(P(-.17f,1.06f+i*.035f),P(.17f,1.06f+i*.035f),.012f,Color.Lerp(accent,secondary,i/4f),1);break;
                    case 5:for(int x=-2;x<=2;x++)for(int y=0;y<3;y++){Vector3 at=P(x*.055f,.99f+y*.075f);Patch(new[]{at+Vector3.up*.013f,at+Vector3.right*.012f,at-Vector3.up*.013f,at-Vector3.right*.012f},accent,1);}break;
                    case 6:Patch(new[]{P(-.18f,1.27f),P(-.03f,1.27f),P(-.03f,1.08f),P(-.18f,1.08f)},accent,1);Line(P(.08f,.98f),P(.08f,1.32f),.012f,secondary,1);break;
                    case 7:Line(P(-.13f,1.32f),P(0,1.08f),.022f,accent,1);Line(P(.13f,1.32f),P(0,1.08f),.022f,accent,1);Line(P(0,1.08f),P(0,.97f),.012f,accent,1);break;
                    case 8:for(int i=0;i<5;i++)Line(P(-.20f,1.25f),P(-.17f+i*.075f,1.03f),.010f,accent,1);break;
                    case 9:Line(P(-.16f,1.30f),P(.13f,.98f),.018f,accent,1);Line(P(-.13f,1.30f),P(.16f,.98f),.007f,secondary,1);break;
                    case 10:for(int i=0;i<7;i++)Line(P(-.18f+i*.055f,.98f+Mathf.Sin(i)*.025f),P(-.125f+i*.055f,.98f+Mathf.Sin(i+1)*.025f),.015f,accent,1);break;
                    case 11:for(int i=-2;i<=2;i++)Line(P(i*.06f,.98f),P(i*.06f,1.30f),i==0?.018f:.008f,accent,1);break;
                    case 12:for(int row=0;row<3;row++)for(int i=0;i<7;i++)Line(P(-.18f+i*.055f,1.02f+row*.055f+Mathf.Sin(i)*.018f),P(-.125f+i*.055f,1.02f+row*.055f+Mathf.Sin(i+1)*.018f),.012f,accent,1);break;
                    case 13:for(int i=-2;i<=2;i++)for(int j=0;j<2;j++){Vector3 at=P(i*.06f,1.02f+j*.10f);Patch(new[]{at+Vector3.up*.027f,at+Vector3.right*.025f,at-Vector3.up*.027f,at-Vector3.right*.025f},accent,1);}break;
                    case 14:for(int i=0;i<5;i++){Line(P(-.17f+i*.07f,1.02f),P(-.10f+i*.07f,1.16f),.009f,accent,1);Line(P(-.10f+i*.07f,1.16f),P(-.03f+i*.07f,1.02f),.009f,accent,1);}break;
                }
                Rings(Vector3.zero,new[]{1.365f,1.385f,1.405f},new[]{.071f,.073f,.071f},new[]{.071f,.073f,.071f},_=>accent,_=>W(1),20);
            }
        }
    }
}
