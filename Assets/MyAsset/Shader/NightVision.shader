Shader "Post Process/Shader_NightVision"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" { }
        _ColorTint("Color Tint", Color) = (1, 1, 1, 1)
        _VignetteRadius("Vignette Radius", Range(0, 2)) = 0.25
        _VignetteSmoothness("Vignette Smoothness", Range(0, 2)) = 0.25
        _LightSensitivity("Light Sensitivity", Range(1, 100)) = 1
        _LightWhiteTreshold("Light White Treshold", Range(0, 1)) = 0.5
        _LightWhitePower("Light White Power", Float) = 2
    }
        SubShader
        {
            // No culling or depth
            Cull Off ZWrite Off ZTest Always

            Pass
            {
                CGPROGRAM

                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                struct appdata
                {
                    float4 vertex: POSITION;
                    float2 uv: TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv: TEXCOORD0;
                    float4 vertex: SV_POSITION;
                };

                float hash(float n)
                {
                    return frac(sin(n) * 43758.5453);
                }

                // The noise function returns a value in the range -1.0f -> 1.0f
                float noise(float3 x)
                {
                    float3 p = floor(x);
                    float3 f = frac(x);

                    f = f * f * (3.0 - 2.0 * f);
                    float n = p.x + p.y * 57.0 + 113.0 * p.z;

                    return lerp(lerp(lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                    lerp(hash(n + 57.0), hash(n + 58.0), f.x), f.y),
                    lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                    lerp(hash(n + 170.0), hash(n + 171.0), f.x), f.y), f.z);
                }

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                sampler2D _MainTex, _CameraGBufferTexture0;
                fixed4 _ColorTint;
                fixed _VignetteRadius, _VignetteSmoothness, _LightSensitivity, _LightWhitePower, _LightWhiteTreshold;

                fixed4 frag(v2f i) : SV_Target
                {
                    // Get pixel color
                    fixed4 col = tex2D(_MainTex, i.uv);
                    fixed4 diffuse = tex2D(_CameraGBufferTexture0, i.uv); // Diffuse RGB, Occlusion A
                    fixed4 nakedColor = col;

                    // Scene color - Keep only luminance
                    fixed luminance = (col.r + col.g + col.b) * 0.333f;
                    col.rgb = fixed3(luminance, luminance, luminance);

                    // GBuffer - Keep only luminance
                    float diffuseLuminance = (diffuse.r + diffuse.g + diffuse.b) * 0.333f;
                    diffuse.rgb = fixed3(diffuseLuminance, diffuseLuminance, diffuseLuminance);

                    // Blend scene color with GBuffer to keep luminance when no light still preserving light
                    col = lerp(diffuse, col * _LightSensitivity, luminance);

                    // Colorize pixel with a constant color
                    col *= _ColorTint;

                    // Blend high luminance pixel with white to empower the luminance
                    if (luminance > _LightWhiteTreshold)
                        col.rgb = lerp(col.rgb, fixed3(1, 1, 1) * _LightWhitePower, luminance);

                    // Noise
                    col.rgb *= noise(float3(i.uv.x * 1000 * _Time.w, i.uv.y * 1000 * _Time.y, 1000 * _Time.x));

                    // Scan lines
                    col.rgb *= min(step(1, fmod(i.uv.y / 0.005f, 2)) + (1 - 0.1f), 1);

                    // Vignette
                    fixed aspectRatio = _ScreenParams.x / _ScreenParams.y;
                    fixed2 position = (i.uv - fixed2(0.5f, 0.5f));
                    position.x *= aspectRatio;
                    fixed len = length(position) * 2;
                    col.rgb *= lerp(1, 0, smoothstep(_VignetteRadius, _VignetteRadius + _VignetteSmoothness, len));

                    return col;
                }
                ENDCG

            }
        }
}