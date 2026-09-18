// Luma, vertical flip and box-average downscale in one pass, so the CPU only ever
// touches 1 byte per output pixel. Output rows come back from AsyncGPUReadback in
// Unity's bottom-up order; with _FlipVertically on, the first readback row is the
// displayed top row, which is the package's top-left-origin Gray8 contract.
Shader "Hidden/ZXingCpp/QRCode/Gray8Readback"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            // x, y: source size in pixels; z, w: output size in pixels.
            float4 _Dimensions;
            float _Factor;
            float _FlipVertically;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                int factor = (int)_Factor;
                int sourceWidth = (int)_Dimensions.x;
                int sourceHeight = (int)_Dimensions.y;
                bool flip = _FlipVertically > 0.5;

                // uv.y == 0 is the first readback row; it should show the displayed top row.
                int2 output = (int2)floor(i.uv * _Dimensions.zw);
                int left = output.x * factor;
                int top = output.y * factor;
                int right = min(left + factor, sourceWidth);
                int bottom = min(top + factor, sourceHeight);

                float sum = 0.0;
                float count = 0.0;
                for (int y = top; y < bottom; y++)
                {
                    int sourceRow = flip ? sourceHeight - 1 - y : y;
                    for (int x = left; x < right; x++)
                    {
                        float2 uv = (float2(x, sourceRow) + 0.5) * _MainTex_TexelSize.xy;
                        float3 c = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).rgb;
                        #ifndef UNITY_COLORSPACE_GAMMA
                        // GetPixels32 hands out the stored sRGB bytes; match them instead of the linear sample.
                        c = LinearToGammaSpace(c);
                        #endif
                        // Integer Rec. 601 weights of the CPU path (77, 151, 28 over 256).
                        sum += dot(c, float3(77.0, 151.0, 28.0) / 256.0);
                        count += 1.0;
                    }
                }

                return float4(sum / count, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
