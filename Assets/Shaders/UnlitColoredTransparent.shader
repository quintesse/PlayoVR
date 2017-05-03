Shader "Unlit Colored Transparent" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }
    Category {
	   Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
	   Blend SrcAlpha OneMinusSrcAlpha
       Lighting Off
       ZWrite Off
       Cull Off
       SubShader {
            Pass {
               SetTexture [_MainTex] {
                    constantColor [_Color]
                    Combine texture * constant, texture * constant
                 }
            }
        }
    }
    Fallback "Diffuse"
}