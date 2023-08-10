public static class ViewDependenceNetworkShader {
    public const string Template = @"Shader ""nerf2mesh_shader/OBJECT_NAME"" {
    Properties {
        _MainTex (""Diffuse Texture"", 2D) = ""white"" {}
        _SpecularTex(""Specular Texture"", 2D) = ""white"" {}
        _MLP0 (""MLP0 Texture"", 2D) = ""white"" {}
        _MLP1(""MLP1 Texture"", 2D) = ""white"" {}
        _Mode(""Mode"", Range(1, 3)) = 1
    }

    CGINCLUDE
    #include ""UnityCG.cginc""

    struct appdata_t {
        float3 position : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f {
        float2 uv : TEXCOORD0;
        float3 rayDirection : TEXCOORD1;
        float4 pos : SV_POSITION;
    };



    v2f vert(appdata_t v)
    {
        v2f o;

        o.uv = v.uv;
        o.pos = UnityObjectToClipPos(v.position);
        o.rayDirection = mul(unity_ObjectToWorld, float4(v.position, 1.0)).rgb - _WorldSpaceCameraPos.xyz;

        return o;
    }

    sampler2D _MainTex;
    sampler2D _SpecularTex;
    sampler2D _MLP0;
    sampler2D _MLP1;

    float inputFetch(float4 f0, float3 viewdir, int j) {
        float input_value = 0.0;

        if (j < 4) {
            input_value = (j == 0) ? viewdir.r : ((j == 1) ? viewdir.g : ((j == 2) ? viewdir.b : f0.r));
        } else {
            input_value = (j == 4) ? f0.g : ((j == 5) ? f0.b : f0.a);
        }

        return input_value;
    }


    float3 evaluateNetwork(float4 f0, float3 viewdir) {
        
        // NUM_CHANNELS_ZERO (input_dim) is hard-coded as 6
        // NUM_CHANNELS_ONE (hidden_dim) can vary, but should be divisible by 4
        // NUM_CHANNELS_TWO (output_dim) is hard-coded as 3
    
        float4 v;
        float4x4 w;                

        float4 result_one[NUM_CHANNELS_ONE / 4];
        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i++) {
            result_one[i] = float4(0.0, 0.0, 0.0, 0.0);
        }

        v = float4(
            inputFetch(f0, viewdir, 0),
            inputFetch(f0, viewdir, 1),
            inputFetch(f0, viewdir, 2),
            inputFetch(f0, viewdir, 3)
        );

        for (int i = 0; i < NUM_CHANNELS_ONE ; i += 4) {
            w = float4x4(
                tex2D(_MLP0, int2(0, i)),
                tex2D(_MLP0, int2(0, i+1)),
                tex2D(_MLP0, int2(0, i+2)),
                tex2D(_MLP0, int2(0, i+3))
            );
            result_one[i / 4] += mul(w, v);
        }

        v = float4(
            inputFetch(f0, viewdir, 4),
            inputFetch(f0, viewdir, 5),
            0.0,
            0.0
        );

        for (int i = 0; i < NUM_CHANNELS_ONE ; i += 4) {
            w = float4x4(
                tex2D(_MLP0, int2(0, NUM_CHANNELS_ONE + i)),
                tex2D(_MLP0, int2(0, NUM_CHANNELS_ONE + i+1)),
                tex2D(_MLP0, int2(0, NUM_CHANNELS_ONE + i+2)),
                tex2D(_MLP0, int2(0, NUM_CHANNELS_ONE + i+3))

            );
            result_one[i / 4] +=mul(v, w);
        }

        // second layer: NUM_CHANNELS_ONE --> 3

        float3 result;
        result = float3(0, 0, 0);

        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i++) {
            v = max(result_one[i], 0.0); // relu
            w = float4x4(
                tex2D(_MLP1, int2(0, i*3)),
                tex2D(_MLP1, int2(0, i*3+1)),
                tex2D(_MLP1, int2(0, i*3+2)),
                float4(0, 0, 0, 0) // padding
            );
            result += mul(v, w).xyz;
        }

        // sigmoid
        return 1.0 / (1.0 + exp(-result)); 

    }
    ENDCG

    SubShader
    {
        Tags { ""RenderType"" = ""Opaque"" }
        LOD 100
        
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            int _Mode;

            float4 frag(v2f i) : SV_Target
            {
                float4 diffuse = tex2D(_MainTex, i.uv);
                float4 specular = tex2D(_SpecularTex, i.uv);
                
                if(_Mode==1){
                    diffuse.a = 1.0;
                    return diffuse;
                }
                else
                {
                    fixed4 normalizedRayDir = fixed4(normalize(i.rayDirection), 1.0);
                    fixed3 result = evaluateNetwork(specular, normalizedRayDir);           
                
                    if (_Mode == 2) // specular
                    {
                        return fixed4(result, 1.0);
                    }
                    else // full
                    {
                        fixed3 finalColor = clamp(diffuse.rgb + result, 0.0, 1.0);
                        return fixed4(finalColor, 1.0);
                    }
                }
            }
            ENDCG
        }
    }
}";
}
