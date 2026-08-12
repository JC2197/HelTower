Shader "Custom/SpriteEmission"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.0
        _BrightnessThreshold ("Brightness Threshold", Range(0, 1)) = 0.6
    }
    
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _EmissionColor;
                float _EmissionStrength;
                float _BrightnessThreshold;
            CBUFFER_END
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 color = texColor * _Color * IN.color;
                
                // Calculate brightness and saturation
                float brightness = max(max(color.r, color.g), color.b);
                float minC = min(min(color.r, color.g), color.b);
                float saturation = (brightness - minC) / (brightness + 0.001);
                
                // Add emission that preserves the original color
                if (brightness > _BrightnessThreshold && saturation > 0.3)
                {
                    // Multiply the original color by emission strength instead of adding white
                    color.rgb *= (1.0 + _EmissionStrength * brightness);
                }
                
                return color;
            }
            ENDHLSL
        }
    }
}