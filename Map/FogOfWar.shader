// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/FogOfWar"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		_CatCoordsX ("Cat Coordinates X", Float) = 0
		_CatCoordsY ("Cat Coordinates Y", Float) = 0
		_R1("R1", Float) = 300
		_R2("R2", Float) = 500
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" }
		

		Pass
		{		
			//Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM

				#pragma vertex vert
				#pragma fragment frag
				
				struct appdata
				{
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
				};

				sampler2D _MainTex;
				float _CatCoordsX;
				float _CatCoordsY;
				float _R1;
				float _R2;

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
					float2 catPos = {_CatCoordsX, _CatCoordsY};

					fixed4 OUT = tex2D(_MainTex, IN.texcoord);

					float dist = distance(catPos, IN.texcoord);

					if ( dist < _R1 )
					{
						OUT.a = 0.0;
					}
					else if ( dist >= _R1 && dist < _R2)
					{
						OUT.a = clamp( (dist - _R1) / (_R2 - _R1), 0, OUT.a);
					}

					return OUT;
				}

			ENDCG
		}
	}
}
