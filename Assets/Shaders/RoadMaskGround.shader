Shader "Dungeon/RoadMaskGround"
{
    Properties
    {
        [NoScaleOffset] _GrassTex ("Grass", 2D) = "white" {}
        [NoScaleOffset] _DirtTex ("Dirt", 2D) = "white" {}
        [NoScaleOffset] _MaskTex ("Road Mask", 2D) = "black" {}
        _TextureScale ("Texture Scale", Float) = 1
        _GrassBrightness ("Grass Brightness", Range(0.5, 1.5)) = 0.9
        _DirtBrightness ("Dirt Brightness", Range(0.5, 1.5)) = 1.06
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "RoadMaskGround"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldXY : TEXCOORD1; };
            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            float _TextureScale;
            float _GrassBrightness;
            float _DirtBrightness;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(world);
                output.worldXY = world.xy;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                float2 textureUV = frac(input.worldXY * _TextureScale);
                half4 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, textureUV);
                half4 dirt = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, textureUV);
                grass.rgb *= _GrassBrightness;
                dirt.rgb *= _DirtBrightness;
                half road = step(0.5h, mask.r);
                half coverage = step(0.5h, mask.g);
                half4 color = lerp(grass, dirt, road);
                color.a *= coverage;
                return color;
            }
            ENDHLSL
        }
    }
}
