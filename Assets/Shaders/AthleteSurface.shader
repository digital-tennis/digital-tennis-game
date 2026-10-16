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
  float _FittedSports;
  fixed4 _Primary,_Secondary,_Accent;
  float4 _Garment,_Tailor,_Trim;
  struct Input{fixed4 color:COLOR;float2 rest;float2 region;};
  void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.color=v.color;o.rest=v.texcoord.xy;o.region=v.texcoord1.xy;}
  void surf(Input i,inout SurfaceOutputStandard o){fixed3 color=i.color.rgb;
   if(_FittedSports>.5 && i.region.x>.99)
   {
    float y=i.rest.y;bool arm=i.region.y>.5;
    if(y>=_Garment.x && y<_Garment.y && (!arm || y>_Garment.z))
    {
     color=_Primary.rgb;
     float panel=abs(i.rest.x-(y-_Trim.x)*_Tailor.y);
     if(panel<_Tailor.x)color=_Secondary.rgb;
     if(abs(panel-_Tailor.x)<_Tailor.z)color=_Accent.rgb;
     if((arm && y<_Garment.z+_Trim.y) || y>_Garment.y-_Trim.y)color=_Accent.rgb;
    }
    else if(!arm && y>_Garment.w && y<_Garment.x)color=_Secondary.rgb;
    else if(!arm && y<_Tailor.w)color=fixed3(.92,.95,.91);
    if(arm && y>_Trim.z && y<_Trim.w)color=_Accent.rgb;
   }
   o.Albedo=color*_Color.rgb;o.Emission=color*_Color.rgb*.10;o.Metallic=0;o.Smoothness=.18;o.Alpha=1;}
  ENDCG
 }
 Fallback "Diffuse"
}
