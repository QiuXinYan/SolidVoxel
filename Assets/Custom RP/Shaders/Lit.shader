Shader "Custom/Lit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        Pass {
            Tags {
                "LightMode" = "CustomLit"
            }
            HLSLPROGRAM
			#pragma shader_feature _CLIPPING
			#pragma multi_compile_instancing
			#pragma vertex litPassVertex
			#pragma fragment litPassFragment
			#include "LitPass.hlsl"
			ENDHLSL
        }
    }
}