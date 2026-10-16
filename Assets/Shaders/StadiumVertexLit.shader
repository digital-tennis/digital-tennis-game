Shader "DigitalTennis/Stadium Vertex Lit"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) _CrowdMotion ("Crowd Motion", Float) = 0 _CrowdClock ("Crowd Clock", Float) = 0 _CrowdCheer ("Crowd Cheer", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert addshadow vertex:vert
        #pragma target 3.0
        fixed4 _Color;
        float _CrowdMotion, _CrowdClock, _CrowdCheer;
        void vert(inout appdata_full v)
        {
            float phase=v.vertex.x*.71+v.vertex.z*.53;
            float amplitude=_CrowdMotion*(.025+_CrowdCheer*.055);
            v.vertex.y+=sin(_CrowdClock*2.1+phase)*amplitude;
            v.vertex.x+=sin(_CrowdClock*1.3+phase)*amplitude*.4;
        }
        struct Input { fixed4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo=IN.color.rgb*_Color.rgb;
            o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
