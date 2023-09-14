public static class ViewDependenceNetworkShader {
    public const string Template = @"Shader ""nerf2mesh_shader/OBJECT_NAME"" {
    Properties {
        _MainTex (""Diffuse Texture"", 2D) = ""white"" {}
        _SpecularTex(""Specular Texture"", 2D) = ""white"" {}
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
        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i++) {   //32
            result_one[i] = float4(0.0, 0.0, 0.0, 0.0);
        }

        v = float4(
            inputFetch(f0, viewdir, 0),
            inputFetch(f0, viewdir, 1),
            inputFetch(f0, viewdir, 2),
            inputFetch(f0, viewdir, 3)
        );

        float w0[NUM_CHANNELS_ZERO * NUM_CHANNELS_ONE] = {__W0_0__, __W0_1__, __W0_2__, __W0_3__, __W0_4__, __W0_5__};
    
        int width = NUM_CHANNELS_ZERO;
        int height = NUM_CHANNELS_ONE;
        int width_pad = NUM_CHANNELS_ZERO + (4 - NUM_CHANNELS_ZERO%4);
        float wd_pad[(NUM_CHANNELS_ZERO + (4 - NUM_CHANNELS_ZERO%4)) * NUM_CHANNELS_ONE];
        for(int j = 0; j < width_pad; j+=4){
            for(int i = 0; i < height; i++){
                for(int c = 0; c < 4; c++){
                    if(c + j >= width){ 
                        wd_pad[j * height + i * 4 + c] = 0.0; // zero padding
                    } else {
                        wd_pad[j * height + i * 4 + c] = w0[j + i * width + c];
                    }
                }
            }
        }


        
        for (int i = 0; i < NUM_CHANNELS_ONE ; i += 4) {   //32
            w = float4x4(
                wd_pad[i*4 + 0], wd_pad[i*4 + 1], wd_pad[i*4 + 2], wd_pad[i*4 + 3],
                wd_pad[i*4 + 4], wd_pad[i*4 + 5], wd_pad[i*4 + 6], wd_pad[i*4 + 7],
                wd_pad[i*4 + 8], wd_pad[i*4 + 9], wd_pad[i*4 + 10], wd_pad[i*4 + 11],
                wd_pad[i*4 + 12], wd_pad[i*4 + 13], wd_pad[i*4 + 14], wd_pad[i*4 + 15]
            );
            result_one[i / 4] += mul(v, w);
        }

        v = float4(
            inputFetch(f0, viewdir, 4),
            inputFetch(f0, viewdir, 5),
            0.0,
            0.0
        );

        for (int i = 0; i < NUM_CHANNELS_ONE ; i += 4) {   //32
            w = float4x4(
                wd_pad[i*4 + 0 + NUM_CHANNELS_ONE], wd_pad[i*4 + 1 + NUM_CHANNELS_ONE], wd_pad[i*4 + 2 + NUM_CHANNELS_ONE], wd_pad[i*4 + 3 + NUM_CHANNELS_ONE],
                wd_pad[i*4 + 4 + NUM_CHANNELS_ONE], wd_pad[i*4 + 5 + NUM_CHANNELS_ONE], wd_pad[i*4 + 6 + NUM_CHANNELS_ONE], wd_pad[i*4 + 7 + NUM_CHANNELS_ONE],
                wd_pad[i*4 + 8 + NUM_CHANNELS_ONE], wd_pad[i*4 + 9 + NUM_CHANNELS_ONE], wd_pad[i*4 + 10+ NUM_CHANNELS_ONE], wd_pad[i*4 + 11+ NUM_CHANNELS_ONE],
                wd_pad[i*4 + 12+ NUM_CHANNELS_ONE], wd_pad[i*4 + 13+ NUM_CHANNELS_ONE], wd_pad[i*4 + 14+ NUM_CHANNELS_ONE], wd_pad[i*4 + 15+ NUM_CHANNELS_ONE]
            );
            result_one[i / 4] +=mul(v, w);
        }

        // second layer: NUM_CHANNELS_ONE --> 32
        
          float w1[NUM_CHANNELS_ONE * NUM_CHANNELS_TWO] = {__W1_0__, __W1_1__, __W1_2__, __W1_3__,
                __W1_4__, __W1_5__, __W1_6__, __W1_7__,  __W1_8__, __W1_9__, __W1_10__, __W1_11__,
                __W1_12__, __W1_13__, __W1_14__, __W1_15__,  __W1_16__, __W1_17__, __W1_18__, __W1_19__,
                __W1_20__, __W1_21__, __W1_22__, __W1_23__,  __W1_24__, __W1_25__, __W1_26__, __W1_27__,
                __W1_28__, __W1_29__, __W1_30__, __W1_31__};

        float3 result;
        result = float3(0, 0, 0);

        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i++) {  // 32  8 * 3 
            v = max(result_one[i], 0.0); // relu
            w = float4x4(
                w1[i*9+0], w1[i*9+1], w1[i*9+2], 0,
                w1[i*9+3], w1[i*9+4], w1[i*9+5], 0,
                w1[i*9+6], w1[i*9+7], w1[i*9+8], 0,
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
