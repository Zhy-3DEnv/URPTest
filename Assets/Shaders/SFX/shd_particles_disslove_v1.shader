Shader "Framework/SFX/shd_particles_disslove_v1"
{
    Properties {
        [HDR] _TintColor ("Tint Color", color) = (0.5, 0.5, 0.5, 0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        [MaterialToggle] _AutoOffset ("Auto Offset", int) = 0
        _UOffset ("U Offset Speed", float ) = 0
        _VOffset ("V Offset Speed", float ) = 0
        _DissloveTex ("Disslove Texture", 2D) = "white" {}
        _OpacityTex ("Opacity Texture", 2D) = "white" {}
        [MaterialToggle] _IsDisslove ("Is Disslove Work", int ) = 1
        [MaterialToggle] _IsOpacityTex ("Is Opacity Texture Work", int) = 0 
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass {
            Tags { "LightMode"="XRPForward" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex :POSITION;
                float4 uv0 :TEXCOORD0;
                float4 uv_CustomData :TEXCOORD1;
                fixed4 vertexColor :COLOR;
            };

            struct v2f
            {
                float4 uv0 :TEXCOORD0;
                float4 uv1 :TEXCOORD2;
                float4 uv_CustomData :TEXCOORD1;
                float4 pos :SV_POSITION;
                fixed4 vertexColor :TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _DissloveTex;
            float4 _DissloveTex_ST;
            sampler2D _OpacityTex;
            float4 _OpacityTex_ST;
            fixed _IsDisslove;
            fixed _AutoOffset;
            fixed _IsOpacityTex;
            half _UOffset;
            half _VOffset;
            fixed4 _TintColor;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 mainUV = lerp((v.uv0.xy + float2(v.uv_CustomData.z, v.uv_CustomData.w)), (float2((_UOffset * _Time.y), (_VOffset * _Time.y)) + v.uv0.xy), _AutoOffset);
                o.uv0.xy = TRANSFORM_TEX(mainUV, _MainTex);
                o.uv0.zw = TRANSFORM_TEX(v.uv0, _DissloveTex);
                o.uv1.xy = TRANSFORM_TEX(v.uv0, _OpacityTex);
                o.uv_CustomData= v.uv_CustomData;
                o.vertexColor = v.vertexColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 mainTexVar = tex2D(_MainTex,i.uv0);
                fixed3 emissive = (mainTexVar.rgb * _TintColor.rgb * i.vertexColor.rgb);
                fixed4 opacityTexVar = tex2D(_OpacityTex , i.uv1.xy);
                fixed4 disslovetexsVar = tex2D(_DissloveTex, i.uv0.zw);
                fixed4 alpha = lerp(mainTexVar.a, opacityTexVar.r, _IsOpacityTex);
                alpha *= i.vertexColor.a * lerp( 1.0, step(i.uv_CustomData.r, disslovetexsVar.r), _IsDisslove);

                fixed4 finalCol;
                finalCol.rgb = emissive;
                finalCol.a = alpha;
                return finalCol;
            }
            ENDCG
        }
    }
}
