using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DigitalTennis
{
    public static class CountryFlags
    {
        private static readonly Texture2D[] cache=new Texture2D[15];
        public static Texture2D Get(int index)
        {
            if(!CharacterCatalog.ValidIndex(index))throw new ArgumentOutOfRangeException(nameof(index));
            return cache[index]!=null?cache[index]:(cache[index]=Resources.Load<Texture2D>("FrontEnd/Flags/"+CharacterCatalog.Ids[index]));
        }
    }
    [Serializable]public sealed class CountryRing {public bool hole;public float[] xy;}
    [Serializable]public sealed class CountryRegion {public string id;public CountryRing[] rings;}
    [Serializable]public sealed class CountryRegions {public CountryRegion[] regions;}
    public static class WorldCountries
    {
        private static CountryRegions data;
        public static CountryRegion[] Regions=>(data??(data=JsonUtility.FromJson<CountryRegions>(Resources.Load<TextAsset>("FrontEnd/WorldCountries").text))).regions;
        public static int Pick(Vector2 p)
        {
            foreach(var region in Regions)
            {
                int index=Array.IndexOf(CharacterCatalog.Ids,region.id);if(index<0)continue;
                bool inside=false,hole=false;
                foreach(var ring in region.rings)if(Contains(ring.xy,p)){if(ring.hole)hole=true;else inside=true;}
                if(inside&&!hole)return index;
            }
            return -1;
        }
        private static bool Contains(float[] xy,Vector2 p)
        {
            bool inside=false;int n=xy.Length/2;
            for(int i=0,j=n-1;i<n;j=i++)
            {
                float x=xy[i*2],y=xy[i*2+1],a=xy[j*2],b=xy[j*2+1];
                if((y>p.y)!=(b>p.y)&&p.x<(a-x)*(p.y-y)/(b-y)+x)inside=!inside;
            }
            return inside;
        }
    }
    /// <summary>Illustrative Natural Earth country rings, separate from land to keep UI meshes below 65K vertices.</summary>
    public sealed class CountryMapGraphic:MaskableGraphic,IPointerClickHandler
    {
        public int Selected;
        public Action<int> Picked;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;
            Vector2 P(float x,float y)=>new Vector2(rect.xMin+x*rect.width,rect.yMin+y*rect.height);
            foreach(var region in WorldCountries.Regions)
            {
                bool selected=region.id==CharacterCatalog.Ids[Selected];
                var tint=selected?new Color(.85f,.98f,.47f,1):new Color(.46f,.68f,.70f,.78f);
                foreach(var ring in region.rings)
                    for(int i=0;i<ring.xy.Length;i+=2)
                    {
                        int j=(i+2)%ring.xy.Length;
                        if(Mathf.Abs(ring.xy[i]-ring.xy[j])>.5f)continue; // No line across the antimeridian.
                        FrontEndGraphic.Line(vh,P(ring.xy[i],ring.xy[i+1]),P(ring.xy[j],ring.xy[j+1]),selected?2.1f:.8f,tint);
                    }
            }
        }
        public void OnPointerClick(PointerEventData e)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var point))return;
            var r=rectTransform.rect;int index=WorldCountries.Pick(new Vector2((point.x-r.xMin)/r.width,(point.y-r.yMin)/r.height));
            if(index>=0)Picked?.Invoke(index);
        }
    }
}
