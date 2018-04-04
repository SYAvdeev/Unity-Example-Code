// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/FogToScreen"
{
	Properties
	{
		_FogTex ("Fog Texture", 2D) = "white" {}
		_MapTex ("Map Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" }
		

		Pass
		{		
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

				#pragma vertex vert
				#pragma fragment frag
				
				struct appdata
				{
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
				};

				sampler2D _FogTex;
				sampler2D _MapTex;

				struct v2f
				{
					float4 pos : SV_POSITION;
					float2 texcoord : TEXCOORD0;
				};

				v2f vert (appdata IN)
				{
					v2f OUT;
					OUT.pos = UnityObjectToClipPos(IN.vertex);
					OUT.texcoord = IN.texcoord;
					return OUT;
				}

				fixed4 frag (v2f IN) : COLOR
				{
					fixed4 fog = tex2D(_FogTex, IN.texcoord);
					fixed4 OUT = tex2D(_MapTex, IN.texcoord);

					OUT.a = fog.a;

					return OUT;
				}

			ENDCG
		}
	}
}
