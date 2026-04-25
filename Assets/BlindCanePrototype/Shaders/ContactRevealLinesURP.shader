Shader "BlindPerception/Contact Reveal Lines URP"
{
    Properties
    {
        _LineColour ("Line Colour", Color) = (1, 1, 1, 1)
        _RevealRadius ("Default Reveal Radius", Float) = 0.45
        _LineSpacing ("Unused Legacy Line Spacing", Float) = 0.16
        _LineWidth ("Outline Width", Float) = 0.035
        _EdgeSoftness ("Outline Softness", Float) = 0.015
        _SurfaceFill ("Subtle Surface Fill", Range(0, 1)) = 0
        _SurfacePattern ("Surface Pattern", Float) = 1
        _SurfaceShapeStrength ("Surface Shape Strength", Range(0, 1)) = 0.16
        _SurfaceShapeSpacing ("Surface Shape Spacing", Float) = 0.2
        _SurfaceShapeWidth ("Surface Shape Width", Float) = 0.025
        _ContactRingStrength ("Contact Ring Strength", Range(0, 1)) = 0
        _AlwaysVisible ("Always Visible Outline", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ContactRevealLines"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColour;
                float _RevealRadius;
                float _LineSpacing;
                float _LineWidth;
                float _EdgeSoftness;
                float _SurfaceFill;
                float _SurfacePattern;
                float _SurfaceShapeStrength;
                float _SurfaceShapeSpacing;
                float _SurfaceShapeWidth;
                float _ContactRingStrength;
                float _AlwaysVisible;
                float _RevealCount;
            CBUFFER_END

            float4 _RevealPoints[16];
            float _RevealRadii[16];
            float _RevealRingStrengths[16];

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            void GetRevealMasks(float3 positionWS, out float revealMask, out float contactRingMask)
            {
                revealMask = 0.0;
                contactRingMask = 0.0;

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_RevealCount)
                    {
                        break;
                    }

                    float pointFade = saturate(_RevealPoints[i].w);
                    float ringStrength = saturate(_RevealRingStrengths[i]);
                    float radius = max(_RevealRadii[i], 0.001);
                    float distanceToPoint = distance(positionWS, _RevealPoints[i].xyz);
                    float outlineWidth = max(_LineWidth, 0.001);
                    float softness = max(_EdgeSoftness, 0.0001);

                    // Keep the centre crisp, then soften only the outer edge of the touched area.
                    float localMask = 1.0 - smoothstep(radius * 0.82, radius, distanceToPoint);
                    float localRing = 1.0 - smoothstep(outlineWidth, outlineWidth + softness, abs(distanceToPoint - radius * 0.82));
                    revealMask = max(revealMask, localMask * pointFade);
                    contactRingMask = max(contactRingMask, localRing * pointFade * ringStrength);
                }

                revealMask = saturate(revealMask);
                contactRingMask = saturate(contactRingMask);
            }

            float GetCubeEdgeMask(float3 positionOS)
            {
                float widthOS = max(_LineWidth, 0.001);
                float softnessOS = max(_EdgeSoftness, 0.0001);

                // Unity primitive Cubes use object-space bounds from -0.5 to 0.5.
                // A real cube edge happens where two axes are near a box side.
                float3 distanceToBoxSide = 0.5 - abs(positionOS);
                float3 nearSide = 1.0 - smoothstep(widthOS, widthOS + softnessOS, distanceToBoxSide);

                float xyEdge = nearSide.x * nearSide.y;
                float xzEdge = nearSide.x * nearSide.z;
                float yzEdge = nearSide.y * nearSide.z;
                return saturate(max(xyEdge, max(xzEdge, yzEdge)));
            }

            float2 GetSurfaceUV(float3 positionWS, float3 normalWS)
            {
                float3 normalAbs = abs(normalize(normalWS));
                float2 surfaceUV = positionWS.xy;

                if (normalAbs.x > normalAbs.y && normalAbs.x > normalAbs.z)
                {
                    surfaceUV = positionWS.zy;
                }
                else if (normalAbs.y > normalAbs.z)
                {
                    surfaceUV = positionWS.xz;
                }

                return surfaceUV;
            }

            float GetRepeatingLineMask(float coordinate, float spacing, float width, float softness)
            {
                float lineDistance = abs(frac(coordinate / spacing) - 0.5) * spacing;
                return 1.0 - smoothstep(width, width + softness, lineDistance);
            }

            float GetSurfaceShapeMask(float3 positionWS, float3 normalWS)
            {
                float pattern = _SurfacePattern;
                if (pattern < 0.5)
                {
                    return 0.0;
                }

                float2 surfaceUV = GetSurfaceUV(positionWS, normalWS);
                float spacing = max(_SurfaceShapeSpacing, 0.001);
                float width = max(_SurfaceShapeWidth, 0.001);
                float softness = max(_EdgeSoftness, 0.0001);

                if (pattern < 1.5)
                {
                    // A plain wall, floor, or block has a flat surface, so the local contact patch
                    // can be visible without pretending it has a tactile texture.
                    return 1.0;
                }

                if (pattern < 2.5)
                {
                    // Directional tactile paving: long raised bars.
                    return GetRepeatingLineMask(surfaceUV.x, spacing, width, softness);
                }

                if (pattern < 3.5)
                {
                    // Warning tactile paving: repeated raised dots.
                    float2 dotCell = (frac(surfaceUV / spacing) - 0.5) * spacing;
                    float dotDistance = length(dotCell);
                    return 1.0 - smoothstep(width, width + softness, dotDistance);
                }

                float horizontal = GetRepeatingLineMask(surfaceUV.x, spacing, width, softness);
                float vertical = GetRepeatingLineMask(surfaceUV.y, spacing, width, softness);
                return max(horizontal, vertical);
            }

            float GetViewSilhouetteMask(float3 positionWS, float3 normalWS)
            {
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - positionWS);
                float normalFacing = abs(dot(normalize(normalWS), viewDirection));
                float rim = 1.0 - normalFacing;
                return smoothstep(0.55, 0.9, rim);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float revealMask;
                float contactRingMask;
                GetRevealMasks(input.positionWS, revealMask, contactRingMask);

                float cubeEdgeMask = GetCubeEdgeMask(input.positionOS);
                float silhouetteMask = GetViewSilhouetteMask(input.positionWS, input.normalWS);
                float surfaceShapeMask = GetSurfaceShapeMask(input.positionWS, input.normalWS) * _SurfaceShapeStrength;
                float outlineMask = max(cubeEdgeMask, silhouetteMask);
                float contactMask = contactRingMask * _ContactRingStrength;
                float surfaceFill = revealMask * _SurfaceFill;
                float surfaceShape = revealMask * surfaceShapeMask;
                float visibilityMask = saturate(max(revealMask, _AlwaysVisible));
                float alpha = max(max(surfaceFill, surfaceShape), max(visibilityMask * outlineMask, revealMask * contactMask)) * _LineColour.a;

                clip(alpha - 0.001);
                return half4(_LineColour.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
