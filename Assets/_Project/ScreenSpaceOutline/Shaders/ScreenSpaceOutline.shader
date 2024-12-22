Shader "Hidden/ScreenSpaceOutline"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalRenderPipeline"
        }
        
        Pass // outlined renderer drawing pass
        {
            Name "Renderer Drawing Pass"
            
            Blend Off
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };
            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
            };
            
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half Fragment() : SV_Target
            {
                return 1;
            }
            
            ENDHLSL
        }

        Pass // outline edge detection pass
        {
            Blend Off
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM

            #pragma vertex   OutlinePassVertex
            #pragma fragment OutlinePassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BlitTexture_TexelSize;
            half _OutlineWidth;
            
            struct CustomVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                float2 uvs[4]     : TEXCOORD1;
            };

            CustomVaryings OutlinePassVertex(Attributes input)
            {
                CustomVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);

                // multiply by 0.5 due to half-resolution texture
                float2 texelSize = _BlitTexture_TexelSize.xy * 0.5;
                const float halfWidthFloor = floor(_OutlineWidth * 0.5);
                const float halfWidthCeil = ceil(_OutlineWidth * 0.5);

                output.uvs[0] = output.texcoord + texelSize * float2(halfWidthFloor, halfWidthCeil)  * float2(-1,  1);
                output.uvs[1] = output.texcoord + texelSize * float2(halfWidthCeil,  halfWidthFloor) * float2( 1,  1);
                output.uvs[2] = output.texcoord + texelSize * float2(halfWidthFloor, halfWidthCeil)  * float2(-1, -1);
                output.uvs[3] = output.texcoord + texelSize * float2(halfWidthCeil,  halfWidthFloor) * float2( 1, -1);
                
                return output;
            }
            
            half RobertsCross(half samples[4])
            {
                const half difference_1 = samples[1] - samples[2];
                const half difference_2 = samples[0] - samples[3];
                return sqrt(difference_1 * difference_1 + difference_2 * difference_2);
            }
            
            half OutlinePassFragment(CustomVaryings input) : SV_Target
            {
                half colors[4];
                for (int i = 0; i < 4; i++)
                {
                    colors[i] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uvs[i]);
                }
                half edge = RobertsCross(colors);
                return edge;
            }
            
            ENDHLSL
        }
        
        Pass // outline composite pass
        {
            Name "Outline Composite Pass"
            
            Cull Off
            ZTest NotEqual ZWrite Off
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add
            
            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment Fragment

            half4 _OutlineColor;
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            half4 Fragment(Varyings input) : SV_Target
            {
                half outline = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord).r;
                half3 outlineColor = half3(outline, outline, outline) * _OutlineColor.rgb;
                return half4(outlineColor, 1);
            }
            ENDHLSL
            
        }
    }
}