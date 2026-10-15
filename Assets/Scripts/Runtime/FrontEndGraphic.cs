using System;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalTennis
{
    public enum FrontGraphicKind { World, Radar, Diagonal, Ring, Connector }
    public sealed class FrontEndGraphic : MaskableGraphic
    {
        public FrontGraphicKind Kind;
        public float[] Ratings = {70,70,70,70,70,70};
        public Vector2 Endpoint;
        [Serializable] private sealed class Land { public LandPolygon[] polygons; }
        [Serializable] private sealed class LandPolygon { public float[] xy; public int[] triangles; }
        private static Land land;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 P(float x,float y) => new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            if (Kind == FrontGraphicKind.World)
            {
                if (land == null) land = JsonUtility.FromJson<Land>(Resources.Load<TextAsset>("FrontEnd/WorldLand").text);
                var grid = new Color(.23f,.47f,.5f,.17f);
                for (int x=0;x<=12;x++) Line(vh,P(x/12f,0),P(x/12f,1),.6f,grid);
                for (int y=0;y<=6;y++) Line(vh,P(0,y/6f),P(1,y/6f),.6f,grid);
                foreach (var poly in land.polygons)
                {
                    int start=vh.currentVertCount;
                    for (int i=0;i<poly.xy.Length;i+=2) vh.AddVert(P(poly.xy[i],poly.xy[i+1]),new Color(.13f,.29f,.34f,1),Vector2.zero);
                    for (int i=0;i<poly.triangles.Length;i+=3) vh.AddTriangle(start+poly.triangles[i],start+poly.triangles[i+1],start+poly.triangles[i+2]);
                    for (int i=0;i<poly.xy.Length;i+=2)
                    { int j=(i+2)%poly.xy.Length; Line(vh,P(poly.xy[i],poly.xy[i+1]),P(poly.xy[j],poly.xy[j+1]),.6f,new Color(.27f,.52f,.52f,1)); }
                }
            }
            else if (Kind == FrontGraphicKind.Diagonal)
            {
                Quad(vh,P(.38f,0),P(.78f,0),P(1,1),P(.6f,1),color);
                for(int i=0;i<7;i++) Line(vh,P(.43f+i*.06f,0),P(.65f+i*.06f,1),1,new Color(.5f,1,.7f,.07f));
            }
            else if (Kind == FrontGraphicKind.Connector) Line(vh,r.center,Endpoint,1.2f,color);
            else
            {
                int count=Kind == FrontGraphicKind.Ring ? 48 : 6; var center=r.center;
                float radius=Mathf.Min(r.width,r.height)*.48f;
                Vector2 At(int i,float scale) { float a=(90-i*360f/count)*Mathf.Deg2Rad;return center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*scale; }
                if (Kind == FrontGraphicKind.Radar)
                {
                    for (int ring=1;ring<=4;ring++) for (int i=0;i<6;i++) Line(vh,At(i,ring*.25f),At((i+1)%6,ring*.25f),1,new Color(.4f,.65f,.6f,.3f));
                    for (int i=0;i<6;i++) Line(vh,center,At(i,1),.7f,new Color(.4f,.65f,.6f,.3f));
                    int s=vh.currentVertCount;vh.AddVert(center,new Color(color.r,color.g,color.b,.28f),Vector2.zero);
                    for (int i=0;i<6;i++) vh.AddVert(At(i,Ratings[i]/100f),new Color(color.r,color.g,color.b,.28f),Vector2.zero);
                    for (int i=0;i<6;i++) { vh.AddTriangle(s,s+1+i,s+1+(i+1)%6);Line(vh,At(i,Ratings[i]/100f),At((i+1)%6,Ratings[(i+1)%6]/100f),2,color); }
                }
                else for(int i=0;i<count;i++) Line(vh,At(i,1),At(i+1,1),2,color);
            }
        }
        public static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        { var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Quad(vh,a-n,a+n,b+n,b-n,tint); }
        public static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {
            int s=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
    }
}
