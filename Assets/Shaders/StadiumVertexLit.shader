Shader "DigitalTennis/Stadium Vertex Lit"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert addshadow
        #pragma target 3.0
        fixed4 _Color;
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
