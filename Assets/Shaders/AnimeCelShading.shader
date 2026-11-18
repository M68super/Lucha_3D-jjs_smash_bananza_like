Shader "Lucha3D/AnimeCelShading"
{
    Properties
    {
        [Header(Base Color and Ramp)]
        _BaseColor("Color Base", Color) = (1, 1, 1, 1)
        _ShadowColor("Color de Sombra", Color) = (0.55, 0.58, 0.72, 1)
        _MainTex("Textura Albedo (Opcional)", 2D) = "white" {}
        _RampThreshold("Umbral de Luz (Cel Ramp)", Range(0.0, 1.0)) = 0.5
        _RampSmoothness("Suavizado de Borde Cel", Range(0.001, 0.2)) = 0.04

        [Header(Specular Highlight)]
        _SpecularColor("Color Especular", Color) = (1, 1, 1, 1)
        _Glossiness("Brillo / Glaze", Range(8.0, 256.0)) = 32.0
        _SpecularThreshold("Umbral Especular Toon", Range(0.1, 1.0)) = 0.8

        [Header(Anime Outline)]
        _OutlineColor("Color de Contorno Anime", Color) = (0.1, 0.1, 0.15, 1)
        _OutlineWidth("Grosor de Contorno", Range(0.0, 0.05)) = 0.008
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        // ==========================================
        // PASS 1: CONTORNO ANIME (INVERTED HULL PASS)
        // ==========================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Extrusión de vértices a lo largo de las normales del modelo para contorno invertido
                float3 nOS = length(input.normalOS) > 0.001 ? normalize(input.normalOS) : float3(0, 1, 0);
                float3 extrudedPos = input.positionOS.xyz + nOS * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extrudedPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ==========================================
        // PASS 2: ILUMINACIÓN CEL-SHADING ESTILIZADA
        // ==========================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _SpecularColor;
                float _RampThreshold;
                float _RampSmoothness;
                float _Glossiness;
                float _SpecularThreshold;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // Obtener luz direccional principal de URP
                Light mainLight = GetMainLight();
                float3 lightDirWS = normalize(mainLight.direction);

                // Cálculo Lambertiano difuso: N dot L
                float NdotL = dot(normalWS, lightDirWS);
                float halfLambert = NdotL * 0.5 + 0.5;

                // Rampa Cel de 2 tonos estilizada
                float ramp = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, halfLambert * mainLight.shadowAttenuation);
                float3 diffuseColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, ramp);

                // Reflejo especular anime recortado (Blinn-Phong Toon)
                float3 halfDir = normalize(lightDirWS + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _Glossiness);
                float toonSpec = step(_SpecularThreshold, spec);
                float3 specularHighlight = toonSpec * _SpecularColor.rgb * ramp;

                // Color final modulado con textura si existe
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 finalColor = (diffuseColor * texColor.rgb * mainLight.color) + specularHighlight;

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
