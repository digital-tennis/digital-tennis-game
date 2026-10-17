Shader "DigitalTennis/Athlete Surface"
{
 Properties{_Color("Tint",Color)=(1,1,1,1) _CountryFlag("Country flag",2D)="white"{}}
 SubShader
 {
  Tags{"RenderType"="Opaque"} LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
  #pragma target 3.0
  fixed4 _Color;
  float _FittedSports,_PersonaMotif;
  sampler2D _CountryFlag;float4 _FlagForward;
  fixed4 _Primary,_Secondary,_Accent;
  float4 _Garment,_Tailor,_Trim;
  struct Input{fixed4 color:COLOR;float2 rest;float2 region;float3 worldNormal;};
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
     // Persona graphics are evaluated in the same rest surface as the garment:
     // no overlaid panels can float or intersect the body during a stroke.
     if(_PersonaMotif>=1.5 && !arm)
     {
      float x=i.rest.x, h=y-_Trim.x, ink=0;
      int motif=(int)_PersonaMotif;
      if(motif==2)ink=step(abs(frac((x-h*.65)*23)-.5),.10); // brush bands
      if(motif==3)ink=step(abs(h+.04-abs(x)*.7),.018); // varsity chevron
      if(motif==4)ink=step(abs(frac(y*28)-.5),.12)*step(y,1.24); // light bands
      if(motif==5)ink=step(abs(frac(x*18)-.5)+abs(frac(y*15)-.5),.19); // textile cells
      if(motif==6)ink=step(x,-.03)*step(1.08,y)*step(y,1.30); // precise block
      if(motif==7)ink=step(abs(abs(x)-(y-1.02)*.45),.012); // tailored vest
      if(motif==8)ink=step(abs(frac(atan2(h-.1,x+.2)*6)-.5),.09); // fan seams
      if(motif==9)ink=step(abs(x+h*.75),.016); // fencing wrap
      if(motif==10)ink=step(abs(y-(.99+sin(x*25)*.024)),.012); // wave hem
      if(motif==11)ink=step(abs(frac(x*17)-.5),.10); // canal lines
      if(motif==12)ink=step(abs(frac((y+sin(x*25)*.018)*17)-.5),.10); // rhythm waves
      if(motif==13)ink=step(abs(frac(x*17)-.5)+abs(frac(y*12)-.5),.27); // open tile geometry
      if(motif==14)ink=step(abs(frac((y+abs(sin(x*20))*.065)*21)-.5),.10); // woven contours
      color=lerp(_Primary.rgb,_Accent.rgb,ink);
      if(abs(x)>.17)color=_Secondary.rgb;
     }
     if((arm && y<_Garment.z+_Trim.y) || y>_Garment.y-_Trim.y)color=_Accent.rgb;
    }
    else if(!arm && y>_Garment.w && y<_Garment.x)color=_Secondary.rgb;
    else if(!arm && y<_Tailor.w)color=fixed3(.92,.95,.91);
    if(arm && y>_Trim.z && y<_Trim.w)color=_Accent.rgb;
   }
   // Printed front-chest badge follows existing skinned surface and torso orientation.
   // No new collider, floating decal mesh or change to athlete geometry.
   float2 flagUV=(i.rest-float2(-.16,1.16))/float2(.105,.07875);
   if(_FittedSports>.5 && i.region.x>.99 && i.region.y<.5 && dot(normalize(i.worldNormal),normalize(_FlagForward.xyz))>.45 && all(flagUV>=0) && all(flagUV<=1))
   {color=fixed3(.94,.96,.94);if(all(flagUV>.045)&&all(flagUV<.955))color=tex2D(_CountryFlag,(flagUV-.045)/.91).rgb;}
   o.Albedo=color*_Color.rgb;o.Emission=color*_Color.rgb*.10;o.Metallic=0;o.Smoothness=.18;o.Alpha=1;}
  ENDCG
 }
 Fallback "Diffuse"
}
