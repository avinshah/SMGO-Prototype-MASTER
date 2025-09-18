Shader "Custom/Quest3_PBR_Final"
{
    Properties
    {
        // Mode toggles
        [Toggle(USE_TEXTURE_ARRAY)] _UseTextureArray ("Use Texture Arrays", Float) = 0
        
        // Single Textures (when not using arrays)
        _MainTex ("Base Color", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _RoughnessTexture ("Roughness", 2D) = "white" {}
        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        
        // Texture Arrays (when USE_TEXTURE_ARRAY is on)
        _BaseMapArray ("Base Color Array", 2DArray) = "white" {}
        _NormalArray ("Normal Array", 2DArray) = "bump" {}
        _MetallicArray ("Metallic Array", 2DArray) = "white" {}
        _RoughnessArray ("Roughness Array", 2DArray) = "white" {}
        _AOArray ("Ambient Occlusion Array", 2DArray) = "white" {}
        
        // Array specific properties
        _TextureIndex ("Texture Index (Array Mode)", Range(0, 15)) = 0
        _UseRandomPerObject ("Random Per Object (Array Mode)", Float) = 0
        _RandomSeed ("Random Seed (Array Mode)", Float) = 0
        
        // PBR Controls
        _MetallicStrength ("Metallic Strength", Range(0, 1)) = 1.0
        _RoughnessStrength ("Roughness Strength", Range(0, 1)) = 1.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _AOStrength ("AO Strength", Range(0, 1)) = 1.0
        
        // Tiling and Offset
        _Tiling ("Tiling", Vector) = (1, 1, 0, 0)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        LOD 100
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Shader variants
            #pragma shader_feature_local USE_TEXTURE_ARRAY
            
            // Standard URP keywords for proper lighting
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            
            // Optimize for Quest 3
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 3.0
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 1);
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                half3 viewDirWS : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD6;
                #endif
                half fogFactor : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // Single texture samplers - using different names to avoid conflicts
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_RoughnessTexture); SAMPLER(sampler_RoughnessTexture);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            
            #ifdef USE_TEXTURE_ARRAY
                // Texture Array samplers
                TEXTURE2D_ARRAY(_BaseMapArray); SAMPLER(sampler_BaseMapArray);
                TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
                TEXTURE2D_ARRAY(_MetallicArray); SAMPLER(sampler_MetallicArray);
                TEXTURE2D_ARRAY(_RoughnessArray); SAMPLER(sampler_RoughnessArray);
                TEXTURE2D_ARRAY(_AOArray); SAMPLER(sampler_AOArray);
            #endif
            
            CBUFFER_START(UnityPerMaterial)
                float _TextureIndex;
                float _UseRandomPerObject;
                float _RandomSeed;
                float _MetallicStrength;
                float _RoughnessStrength;
                float _NormalStrength;
                float _AOStrength;
                float4 _Tiling;
            CBUFFER_END
            
            #ifdef USE_TEXTURE_ARRAY
            float random(float3 seed)
            {
                return frac(sin(dot(seed, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }
            
            float getTextureIndex(float3 worldPos)
            {
                if (_UseRandomPerObject > 0.5)
                {
                    float randValue = random(floor(worldPos * 0.1) + _RandomSeed);
                    return floor(randValue * 16.0);
                }
                return _TextureIndex;
            }
            #endif
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.texcoord * _Tiling.xy + _Tiling.zw;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif
                
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Sample textures based on mode
                half4 baseColor, normalMap;
                half metallic, roughness, ao;
                
                #ifdef USE_TEXTURE_ARRAY
                    float texIndex = getTextureIndex(input.positionWS);
                    baseColor = SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, sampler_BaseMapArray, input.uv, texIndex);
                    normalMap = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, input.uv, texIndex);
                    metallic = SAMPLE_TEXTURE2D_ARRAY(_MetallicArray, sampler_MetallicArray, input.uv, texIndex).r;
                    roughness = SAMPLE_TEXTURE2D_ARRAY(_RoughnessArray, sampler_RoughnessArray, input.uv, texIndex).r;
                    ao = SAMPLE_TEXTURE2D_ARRAY(_AOArray, sampler_AOArray, input.uv, texIndex).r;
                #else
                    baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    normalMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    metallic = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).r;
                    roughness = SAMPLE_TEXTURE2D(_RoughnessTexture, sampler_RoughnessTexture, input.uv).r;
                    ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                #endif
                
                // Apply strengths
                metallic = saturate(metallic * _MetallicStrength);
                roughness = saturate(roughness * _RoughnessStrength);
                ao = lerp(1.0, ao, _AOStrength);
                
                // Unpack and transform normal
                half3 normalTS = UnpackNormalScale(normalMap, _NormalStrength);
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                normalWS = NormalizeNormalPerPixel(normalWS);
                
                // Setup InputData for URP lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(input.viewDirWS);
                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #endif
                
                // Setup SurfaceData for proper PBR
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = 1.0 - roughness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = ao;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;
                
                // Use Unity's proper PBR lighting function
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // Apply fog
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow Caster Pass
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        
        // Depth Only Pass
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        
        // Depth Normals Pass  
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    
    FallBack "Universal Render Pipeline/Lit"
    // CustomEditor "Quest3ShaderGUI" // Uncomment this line after creating the editor script
}