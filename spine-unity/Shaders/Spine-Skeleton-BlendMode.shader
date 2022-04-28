Shader "Spine/Skeleton-BlendMode" {
	Properties {
		_Cutoff ("Shadow alpha cutoff", Range(0,1)) = 0.1
		[HDR]_Color("Color", Color) = (1,1,1,1)
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Op", Float) = 0.0
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend Mode", Int) = 0
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend Mode", Int) = 0
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture", Int) = 1
		[HideInInspector] _StencilRef("Stencil Reference", Float) = 1.0
		[HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 8 // Set to Always as default

		// Outline properties are drawn via custom editor.
		[HideInInspector] _OutlineWidth("Outline Width", Range(0,8)) = 3.0
		[HideInInspector] _OutlineColor("Outline Color", Color) = (1,1,0,1)
		[HideInInspector] _OutlineReferenceTexWidth("Reference Texture Width", Int) = 1024
		[HideInInspector] _ThresholdEnd("Outline Threshold", Range(0,1)) = 0.25
		[HideInInspector] _OutlineSmoothness("Outline Smoothness", Range(0,1)) = 1.0
		[HideInInspector][MaterialToggle(_USE8NEIGHBOURHOOD_ON)] _Use8Neighbourhood("Sample 8 Neighbours", Float) = 1
		[HideInInspector] _OutlineMipLevel("Outline Mip Level", Range(0,3)) = 0
		
		[HideInInspector] _OpenMask("openMask",Int) = 0
		[HideInInspector] _Area("Area",Vector) = (0,0,1,1)
	}

	SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

		Fog { Mode Off }
		BlendOp[_BlendOp]
		Blend[_SrcBlend][_DstBlend]
		Cull Off
		ZWrite Off
		Lighting Off

		Stencil {
			Ref[_StencilRef]
			Comp[_StencilComp]
			Pass Keep
		}

		CGINCLUDE

		#include "UnityCG.cginc"

		float4 _Color;
		sampler2D _MainTex;

		struct VertexInput {
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
			float4 vertexColor : COLOR;
		};

		struct DistortionOutput {
			float4 pos : SV_POSITION;
			float2 uv : TEXCOORD0;
			float4 vertexColor : COLOR;
		};

		DistortionOutput vertDistortion(VertexInput v) {
			DistortionOutput o;
			o.pos = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;
			o.vertexColor = v.vertexColor;
			return o;
		}

		ENDCG

		// Normal
		Pass {
			Name "Normal"

			CGPROGRAM
			#pragma shader_feature _ _STRAIGHT_ALPHA_INPUT
			#pragma vertex vert
			#pragma fragment frag
			
			float4 _Area;
			int _OpenMask;

			struct VertexOutput {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 vertexColor : COLOR;
				float2 worldPos:TEXCOORD1;
			};

			VertexOutput vert (VertexInput v) {
				VertexOutput o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.vertexColor = v.vertexColor;
				o.worldPos = mul(unity_ObjectToWorld,v.vertex).xy;
				return o;
			}

			sampler2D _CameraDepthTexture;
			float _InvFade;
			
			float4 frag (VertexOutput i) : SV_Target {
				float4 texColor = tex2D(_MainTex, i.uv) * _Color;

				#if defined(_STRAIGHT_ALPHA_INPUT)
				texColor.rgb *= texColor.a;
				#endif

				bool inArea = true;
				if ( _OpenMask == 1)
					inArea = i.worldPos.x >= _Area.x && i.worldPos.x <= _Area.z && i.worldPos.y >= _Area.y && i.worldPos.y <= _Area.w;
				
				return inArea? (texColor * i.vertexColor):fixed4(0,0,0,0);
			}
			ENDCG
		}

		// SkillDistortion
		Pass {
			Name "SkillDistortionObject"

			Tags {"LightMode"="SkillDistortionObject"}

			CGPROGRAM
			#pragma vertex vertDistortion
			#pragma fragment frag

			float4 frag(DistortionOutput i) : SV_Target {
				float4 texColor = tex2D(_MainTex, i.uv) * i.vertexColor;
				return texColor.aaaa;
			}
			ENDCG
		}

		Pass {
			Name "Caster"
			Tags { "LightMode"="ShadowCaster" }
			Offset 1, 1
			ZWrite On
			ZTest LEqual

			Fog { Mode Off }
			Cull Off
			Lighting Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
			fixed _Cutoff;

			struct VertexOutput {
				V2F_SHADOW_CASTER;
				float4 uvAndAlpha : TEXCOORD1;
			};

			VertexOutput vert (appdata_base v, float4 vertexColor : COLOR) {
				VertexOutput o;
				o.uvAndAlpha = v.texcoord;
				o.uvAndAlpha.a = vertexColor.a;
				TRANSFER_SHADOW_CASTER(o)
				return o;
			}

			float4 frag (VertexOutput i) : SV_Target {
				fixed4 texcol = tex2D(_MainTex, i.uvAndAlpha.xy);
				clip(texcol.a * i.uvAndAlpha.a - _Cutoff);
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
	}
	CustomEditor "SpineShaderWithOutlineGUI"
}
