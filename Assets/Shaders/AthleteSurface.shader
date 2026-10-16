Shader "DigitalTennis/Athlete Surface"
{
 Properties{_Color("Tint",Color)=(1,1,1,1)}
 SubShader
 {
  Tags{"RenderType"="Opaque"} LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  fixed4 _Color;
  struct Input{fixed4 color:COLOR;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;}
  void surf(Input i,inout SurfaceOutputStandard o){o.Albedo=i.color.rgb*_Color.rgb;o.Emission=i.color.rgb*_Color.rgb*.10;o.Metallic=0;o.Smoothness=.18;o.Alpha=1;}
  ENDCG
 }
 Fallback "Diffuse"
}
