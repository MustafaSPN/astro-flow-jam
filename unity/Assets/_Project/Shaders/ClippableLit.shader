// Basit aydınlatmalı URP shader'ı; bir düzlemin öbür tarafında kalan pikselleri çizmez.
// Roketin kapıdan geçen kısmı bu sayede duvarın arkasına girmiş gibi görünmez olur.
// _ClipPlane: xyz = düzlem normali (dünya), w = dot(normal, düzlem noktası). dot(normal, p) > w olan pikseller kesilir.
// Varsayılan değer hiçbir şeyi kesmez.
Shader "AstroFlowJam/ClippableLit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0
        _ClipPlane("Clip Plane", Vector) = (0, 0, 0, 100000)
        _GlowGroup("Glow Group (0 none, 1 block, 2 pixel)", Float) = 0
        _Gloss("Gloss (candy shine)", Range(0, 1)) = 0
        _GlossKeepDark("Gloss off on dark colors", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            float4 _ClipPlane;
            half _GlowGroup;
            half _Gloss;
            half _GlossKeepDark;
        CBUFFER_END
        // Joker hedef seçimi parlaması (global, LevelController ayarlar): bloklar ve resim pikselleri ayrı ayrı.
        half _AfjGlowBlocks;
        half _AfjGlowPixels;

        void ApplyClip(float3 positionWS)
        {
            clip(_ClipPlane.w - dot(_ClipPlane.xyz, positionWS));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyClip(input.positionWS);
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate(dot(normal, light.direction));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDir = normalize(light.direction + viewDir);
                half specular = pow(saturate(dot(normal, halfDir)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 0.5;
                half3 color = _BaseColor.rgb * (SampleSH(normal) + light.color * diffuse) + light.color * specular;
                // Metal (sahte ortam yansıması): yansıyan yön yukarı baktıkça parlak, aşağı baktıkça koyu bir gökyüzü
                // degradesi taban rengiyle boyanır; parlama dar ve renkli, kenarlarda (fresnel) açık parıltı. 0'da etkisiz.
                if (_Metallic > 0.001)
                {
                    float3 r = reflect(-viewDir, normal);
                    half up = saturate(r.y * 0.5 + 0.5);
                    half3 env = lerp(half3(0.08, 0.09, 0.13), half3(1.25, 1.3, 1.4), up * up);
                    half tight = pow(saturate(dot(normal, halfDir)), exp2(12.0 * _Smoothness + 2.0)) * 2.2;
                    half fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 4.0) * 0.6;
                    half3 metal = _BaseColor.rgb * (env * 0.85 + light.color * diffuse * 0.35) + (light.color * tight + fresnel) * lerp(_BaseColor.rgb, 1.0, 0.5);
                    color = lerp(color, metal, _Metallic);
                }
                // Şeker parlaklığı (bloklar, pikseller): rengi koruyan doygunluk artışı, dar beyaz parlama ve kenarda açık parıltı.
                if (_Gloss > 0.001)
                {
                    // _GlossKeepDark (kapı kapakları): yalnız siyah (#2F2F2F) eski görünümde kalır; ortam ışığı ve geniş parlama
                    // olmadan siyah kapak koyu duvarla birleşir, açılışı görünmez.
                    half baseLum = dot(_BaseColor.rgb, half3(0.299, 0.587, 0.114));
                    half gloss = _Gloss * lerp(1.0, smoothstep(0.029, 0.032, baseLum), _GlossKeepDark);
                    // Gri ortam ışığı rengi soldurmasın: renk tabanı doğrudan taban renginden, doygunluk biraz artar;
                    // üstüne yalnız dar beyaz parlama ve kendi renginde ince kenar parıltısı.
                    half3 vivid = _BaseColor.rgb * (0.62 + 0.62 * diffuse);
                    half lum = dot(vivid, half3(0.299, 0.587, 0.114));
                    vivid = max(0, lerp(lum.xxx, vivid, 1.25));
                    half tightG = pow(saturate(dot(normal, halfDir)), 128.0) * 0.9;
                    half rim = pow(1.0 - saturate(dot(normal, viewDir)), 4.0) * 0.25;
                    color = lerp(color, vivid, gloss) + (light.color * tightG + rim * _BaseColor.rgb) * gloss;
                }
                half glow = _GlowGroup > 1.5 ? _AfjGlowPixels : (_GlowGroup > 0.5 ? _AfjGlowBlocks : 0.0);
                color += _BaseColor.rgb * glow;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                ApplyClip(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyClip(input.positionWS);
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
