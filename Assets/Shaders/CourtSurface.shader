Shader "DigitalTennis/Court Surface" {
 Properties{_Color("Court",Color)=(.2,.5,.5,1) _Surface("Hard Clay Grass",Float)=0}
 SubShader {Tags{"RenderType"="Opaque"} LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 fixed4 _Color;float _Surface;struct Input{float3 worldPos;};
 float noise(float2 p){return frac(sin(dot(floor(p),float2(127.1,311.7)))*43758.5453);}
 void surf(Input i,inout SurfaceOutputStandard o){float grain=noise(i.worldPos.xz*75)-.5;float stripe=_Surface>1.5?(step(.5,frac(i.worldPos.z/3))-.5)*.10:0;o.Albedo=_Color.rgb*(1+grain*(_Surface>.5?.11:.055)+stripe);o.Smoothness=_Surface<.5?.19:.04;o.Metallic=0;o.Alpha=1;}
 ENDCG } Fallback "Diffuse" }
