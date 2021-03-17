Shader "Framework/SFX/shd_fresnelScroll_ Additive_v1"{
    Properties {
        _Alpha ("Alpha", Range(0, 1)) = 1
		[HDR]_Fresnelcolor("Fresnel color", Color) = (1,1,1,1)
		_FresnelTex("Fresnel Tex", 2D) = "white" {}
		_Fre_tex_tillingoffset("Fre_tex_tilling/offset", Vector) = (1,1,0,0)
		_Fresnelmask("Fresnel mask", 2D) = "white" {}
		_Fre_mask_tillingoffset("Fre_mask_tilling/offset", Vector) = (1,1,0,0)
		_Fresnel_scalepower("Fresnel_scale/power", Vector) = (1,5,0,0)
		_fre_mask_02("fre_mask_02", 2D) = "white" {}
        _HitWidth("Hit Width", float) = 0.5
        _HitFadeOut("Hit FadeOut", float) = 0.25
        _HitDamping ("_HitDamping", float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.sofunny.xrenderpipeline/ShaderLibrary/Core.hlsl"
    ENDHLSL

    SubShader {
        Tags {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent" }

		Pass
		{
			Name "Forward"
			Tags { "LightMode"="XRPForward" }
			
			Blend One One , One OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

			struct VertexInput {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput {
				float4 clipPos : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float fogFactor : TEXCOORD2;
				float4 uv0 : TEXCOORD3;
				float4 worldNormal : TEXCOORD4;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Fre_tex_tillingoffset;
			float4 _Fre_mask_tillingoffset;
			float4 _Fresnelcolor;
			float4 _fre_mask_02_ST;
            float4 _HitPos;
			float2 _Fresnel_scalepower;
			float _TessPhongStrength;
			float _TessValue;
			float _TessMin;
			float _TessMax;
			float _TessEdgeLength;
			float _TessMaxDisp;
            float _Alpha;
            float _HitTime;
            float _HitFadeOut;
            float _HitWidth;
            float _HitDamping;
			CBUFFER_END
	
            TEXTURE2D(_FresnelTex);
            SAMPLER(sampler_FresnelTex);
            TEXTURE2D(_Fresnelmask);
            SAMPLER(sampler_Fresnelmask);
            TEXTURE2D(_fre_mask_02);
            SAMPLER(sampler_fre_mask_02);
						
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float3 worldNormal = TransformObjectToWorldNormal(v.normal);
				o.worldNormal.xyz = worldNormal;
				
				o.uv0.xy = v.uv0.xy;
				o.color = v.color;
				
				o.uv0.zw = 0;
				o.worldNormal.w = 0;
			
				float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
				float4 positionCS = TransformWorldToHClip( positionWS );
                o.positionWS = positionWS;
				o.clipPos = positionCS;
                o.fogFactor = ComputeFogFactor( positionCS.z );
				return o;
			}

			half4 frag (VertexOutput i) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(i);

                float3 worldPosition = i.positionWS;

				float2 fre_offsetUV = i.uv0.xy * float2(_Fre_tex_tillingoffset.x , _Fre_tex_tillingoffset.y);
				float2 fre_pannerUV = _Time.y * float2(_Fre_tex_tillingoffset.z , _Fre_tex_tillingoffset.w) + fre_offsetUV;

				float2 _fre_mask_offsetUV = i.uv0.xy * float2(_Fre_mask_tillingoffset.x , _Fre_mask_tillingoffset.y);
				float2 _fre_mask_pannerUV = ( 1.0 * _Time.y * float2(_Fre_mask_tillingoffset.z , _Fre_mask_tillingoffset.w) + _fre_mask_offsetUV);
                
				float3 worldViewDir = (_WorldSpaceCameraPos.xyz - worldPosition);
				worldViewDir = normalize(worldViewDir);

				float3 worldNormal = i.worldNormal.xyz;
				float fresnelNdotV5 = dot(worldNormal, worldViewDir);

				float fresnelNode5 = (_Fresnel_scalepower.x * pow( 1.0 - fresnelNdotV5, _Fresnel_scalepower.y));
				float2 uv_fre_mask_02 = i.uv0.xy * _fre_mask_02_ST.xy + _fre_mask_02_ST.zw;
				
                half3 fresnelCol =  SAMPLE_TEXTURE2D(_FresnelTex, sampler_FresnelTex, fre_pannerUV).rgb;
                half3 fresnelmask_01 = SAMPLE_TEXTURE2D(_Fresnelmask, sampler_Fresnelmask, _fre_mask_pannerUV).rgb;
                half3 fresnelmask_02 =  SAMPLE_TEXTURE2D(_fre_mask_02, sampler_fre_mask_02, uv_fre_mask_02).rgb;
               
                float distancePosWSAndHitPos = distance(worldPosition, _HitPos);
                float hitMask = saturate((distancePosWSAndHitPos - _HitTime) / _HitFadeOut);
                hitMask = abs(2 * hitMask - 1);
                hitMask = 1 - pow(hitMask, _HitWidth);

                float hitDamping =saturate(_HitTime + _HitDamping);
               
                half4 finalCol;
                finalCol.rgb = fresnelCol * fresnelmask_01.r * fresnelNode5 * _Fresnelcolor * i.color * fresnelmask_02.r * _Alpha;
                finalCol.rgb += hitMask * fresnelCol * _Fresnelcolor * (1 -  hitDamping);
                finalCol.a = 1;

                finalCol.rgb = MixFog(finalCol.rgb, i.fogFactor);
				finalCol = saturate(finalCol);
				return finalCol;
			}
			ENDHLSL
		}
    }
}