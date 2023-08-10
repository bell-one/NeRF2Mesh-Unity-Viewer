public static class ViewDependenceNetworkShader {
    public const string Template = @"Shader ""nerf2mesh_shader/OBJECT_NAME"" {
    Properties {
        _MainTex (""Diffuse Texture"", 2D) = ""white"" {}
        _SpecularTex(""Specular Texture"", 2D) = ""white"" {}
        _Mode(""Mode"", Range(1, 3)) = 1
    }

    CGINCLUDE
    #include ""UnityCG.cginc""

    struct appdata_t
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float3 rayDirection : TEXCOORD1;
        float4 vertex : SV_POSITION;
    };

    v2f vert(appdata_t v)
    {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.rayDirection = -WorldSpaceViewDir(v.vertex);
        o.rayDirection.xz = -o.rayDirection.xz;o.rayDirection.xyz = o.rayDirection.xzy;

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
        
        float4x4 weights_zero[12]; 
        

        weights_zero[0] = (__W0_0__0);
        weights_zero[1] = (__W0_0__1);
        weights_zero[2] = (__W0_1__0);
        weights_zero[3] = (__W0_1__1);
        weights_zero[4] = (__W0_2__0);
        weights_zero[5] = (__W0_2__1);
        weights_zero[6] = (__W0_3__0);
        weights_zero[7] = (__W0_3__1);
        weights_zero[8] = (__W0_4__0);
        weights_zero[9] = (__W0_4__1);
        weights_zero[10] = (__W0_5__0);
        weights_zero[11] = (__W0_5__1);

                            
        float3 weights_one[32];
        weights_one[0] = (__W1_0__);
        weights_one[1] = (__W1_1__);
        weights_one[2] = (__W1_2__);
        weights_one[3] = (__W1_3__);
        weights_one[4] = (__W1_4__);
        weights_one[5] = (__W1_5__);
        weights_one[6] = (__W1_6__);
        weights_one[7] = (__W1_7__);
        weights_one[8] = (__W1_8__);
        weights_one[9] = (__W1_9__);
        weights_one[10] = (__W1_10__);
        weights_one[11] = (__W1_11__);
        weights_one[12] = (__W1_12__);
        weights_one[13] = (__W1_13__);
        weights_one[14] = (__W1_14__);
        weights_one[15] = (__W1_15__);
        weights_one[16] = (__W1_16__);
        weights_one[17] = (__W1_17__);
        weights_one[18] = (__W1_18__);
        weights_one[19] = (__W1_19__);
        weights_one[20] = (__W1_20__);
        weights_one[21] = (__W1_21__);
        weights_one[22] = (__W1_22__);
        weights_one[23] = (__W1_23__);
        weights_one[24] = (__W1_24__);
        weights_one[25] = (__W1_25__);
        weights_one[26] = (__W1_26__);
        weights_one[27] = (__W1_27__);
        weights_one[28] = (__W1_28__);
        weights_one[29] = (__W1_29__);
        weights_one[30] = (__W1_30__);
        weights_one[31] = (__W1_31__);
        // NUM_CHANNELS_ZERO (input_dim) is hard-coded as 6
        // NUM_CHANNELS_ONE (hidden_dim) can vary, but should be divisible by 4
        // NUM_CHANNELS_TWO (output_dim) is hard-coded as 3
    
        float4 v;
        float4x4 w;                

        float4 result_one[8];
        for (int i = 0; i < 8; i++) {
            result_one[i] = float4(0.0, 0.0, 0.0, 0.0);
        }

        v = float4(
            inputFetch(f0, viewdir, 0),
            inputFetch(f0, viewdir, 1),
            inputFetch(f0, viewdir, 2),
            inputFetch(f0, viewdir, 3)
        );

        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i += 1) {
            w = weights_zero[i];
            result_one[i] += mul(w, v);
        }

        v = float4(
            inputFetch(f0, viewdir, 4),
            inputFetch(f0, viewdir, 5),
            0.0,
            0.0
        );

        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i += 1) {
            w = weights_zero[i];
            result_one[i] +=mul(v, w);
        }

        // second layer: NUM_CHANNELS_ONE --> 3

        float3 result;
        result = float3(0, 0, 0);

        for (int i = 0; i < NUM_CHANNELS_ONE / 4; i++) {
            v = max(result_one[i], 0.0); // relu
            w = float4x4(
                float4(weights_one[i*3], 0),
                float4(weights_one[i*3+1], 0),
                float4(weights_one[i*3+2], 0),
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
