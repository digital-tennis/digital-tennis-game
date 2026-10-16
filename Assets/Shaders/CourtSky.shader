Shader "DigitalTennis/Court Sky" {
 Properties{_Zenith("Zenith",Color)=(.3,.5,.7,1) _Horizon("Horizon",Color)=(.8,.7,.6,1)}
 SubShader{Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};fixed4 _Zenith,_Horizon;
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
 fixed4 frag(v2f i):SV_Target{return lerp(_Horizon,_Zenith,pow(saturate(normalize(i.dir).y),.6));}
 ENDCG}} Fallback Off }
