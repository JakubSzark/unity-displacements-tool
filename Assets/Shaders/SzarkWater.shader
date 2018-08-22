// Made By: Jakub P. Szarkowicz
// Email: Jakubshark@gmail.com

Shader "Custom/SurfaceSzarkWater" 
{
	Properties 
	{
		[Header(Colors)]
		_Shallow ("Shallow", Color) = (0.65,0.9,1,1)
		_Deep ("Deep", Color) = (0.13,0.57,0.73,1)
		_MainTex ("Main Texture", 2D) = "white" {}
		_Smoothness ("Smoothness", Range(0, 1)) = 0

		[Header(Foam)]
		[Toggle]
		_UseRamp ("Use Ramp", Float) = 0
		_FoamRamp ("Foam Ramp", 2D) = "white" {}
		_FoamAmount ("Foam Amount", Range(0, 1)) = 0.1
		_FoamColor ("Foam Color", Color) = (1,1,1,1)
		
		[Header(Falloff)]
		_FalloffStrength ("Falloff Strength", Range(0, 1)) = 0.25
		_FalloffDepth ("Falloff Depth", Range(0, 1)) = 0.5

		[Header(Distortion)]
		_RefrStrength ("Refraction Strength", Range(0, 1)) = 0.30
		_TexDistortion ("Texture Distortion", Range(0, 1)) = 0.25

		[Header(Waves)]
		[Toggle]
		_UseNoise ("Use Noise", Float) = 0
		_WaveNoise ("Wave Noise", 2D) = "white" {}
		_WaveDir ("Wave Direction", Vector) = (0, 0, 0.1, 1)
		_WaveAmp ("Wave Amplitude", Range(0, 20)) = 3
		_WaveSpeed ("Wave Speed", Range(0, 10)) = 1

		[Header(Flow Mapping)]
		[Normal]
		_NormalWeak ("Normal Weak", 2D) = "bump" {}
		[Normal]
		_NormalStrong ("Normal Strong", 2D) = "bump" {}
		_FlowMap ("Flow Map", 2D) = "white" {}
		_NormStrength ("Normal Strength", Range(0, 1)) = 0.1
		_FlowSpeed ("Flow Speed", Float) = 1
	}
	SubShader 
	{
		Tags 
		{ 
			"RenderType"="Transparent" 
			"Queue"="Transparent" 
			"ForceNoShadowCasting" = "True" 
			"IgnoreProjector"="True"
		}

		GrabPass { "_Refraction" }
		Cull off

		CGPROGRAM

		#pragma surface surf Standard fullforwardshadows vertex:vert
		#pragma target 4.0

		struct Input 
		{
			float4 grabUV;
			float2 uv_MainTex;
			float2 uv_WaveNoise;
			float2 uv_NormalWeak;
			float4 screenPos;
			float3 normal;
		};

		sampler2D _MainTex;
		sampler2D _WaveNoise;
		sampler2D _CameraDepthTexture;
		sampler2D _FoamRamp;
		sampler2D _Refraction;
		sampler2D _NormalWeak;
		sampler2D _NormalStrong;
		sampler2D _FlowMap;

		fixed4 _Shallow;
		fixed4 _FoamColor;
		fixed4 _WaveDir;
		fixed4 _Deep;

		float _FoamAmount;
		float _FalloffStrength;
		float _TexDistortion;
		float _RefrStrength;
		float _NormStrength;
		float _FalloffDepth;
		float _Smoothness;
		float _UseRamp;
		float _WaveSpeed;
		float _UseNoise;
		float _WaveAmp;
		float _FlowSpeed;

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
			float4 pos = UnityObjectToClipPos(v.vertex);

			float waveForm = sin((worldPos.x * _WaveDir.x) + 
				(worldPos.z * _WaveDir.z) + (worldPos.y * _WaveDir.y) + 
					(_Time.y * _WaveSpeed)) * (0.25 * _WaveAmp);

			float4 noise = tex2Dlod(_WaveNoise, float4(v.texcoord.xy, 0, 0));
			v.vertex.y += _UseNoise == 0 ? waveForm : sin((_Time.y * _WaveSpeed) * noise) * _WaveAmp;

			o.grabUV = ComputeGrabScreenPos(pos);
			o.grabUV.y += waveForm * _RefrStrength;
			o.normal = abs(v.normal);

			o.screenPos = ComputeScreenPos(pos);
			COMPUTE_EYEDEPTH(o.screenPos.z);
		}

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			// Texture Distortion
			IN.uv_MainTex.x += (sin((IN.uv_MainTex.x + IN.uv_MainTex.y) * 
				8 + _Time.g * 1.3) * 0.02) * _TexDistortion;
			IN.uv_MainTex.y += (cos((IN.uv_MainTex.x - IN.uv_MainTex.y) * 
				8 + _Time.g * 2.7) * 0.02) * _TexDistortion;

			// Get Textures
			fixed4 foamTex = tex2D (_FoamRamp, IN.uv_MainTex);
			fixed4 refrTex = tex2Dproj(_Refraction, IN.grabUV) * _Shallow;
			fixed4 mainTex = tex2D (_MainTex, IN.uv_MainTex) * _Deep;

			// Get Depth 
			float depthSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, IN.screenPos);
			float depth = LinearEyeDepth(depthSample).r;

			// Foam and Falloff
			float foam = _UseRamp == 0 ? 1-step(_FoamAmount, abs(depth - IN.screenPos.w)) : 
				1 - saturate((1 - _FoamAmount) * abs(depth - IN.screenPos.w));
			float falloff = 1 - saturate((1 - _FalloffDepth) * (depth - IN.screenPos.w));
			float4 tex = lerp(mainTex, refrTex, falloff * _FalloffStrength);

			// Ramped Foam
			float4 foamRamp;
			if (_UseRamp == 1) {
				foamRamp = float4(tex2D(_FoamRamp, float2(foam, 1)).rgb, 1.0);
			}
			
			// Flow Map
			float3 flowMap = tex2D(_FlowMap, IN.uv_NormalWeak) * 2.0 - 1.0;
			flowMap *= _FlowSpeed;
			float phase0 = frac(_Time[1] * 0.5 + 0.5);
			float phase1 = frac(_Time[1] * 0.5 + 1.0);

			// Normals
			float3 normalWeak = tex2D(_NormalWeak, IN.uv_NormalWeak + flowMap * phase0);
			float3 normalStrong = tex2D(_NormalStrong, IN.uv_NormalWeak + flowMap * phase1);

			// Animated Flow
			float4 flowLerp = abs((0.5 - phase0) / 0.5);
			half3 finalColor = lerp(normalWeak, normalStrong, flowLerp);
			float3 normal = lerp(float3(0.5, 0.5, 1), finalColor,
				_NormStrength);

			o.Albedo = _UseRamp == 1 ? tex * (_FoamColor * foamRamp) :
				tex + (_FoamColor * foam);

			o.Normal = UnpackNormal(float4(normal, 1));
			o.Smoothness = _Smoothness;
		}

		ENDCG
	}
	FallBack "Diffuse"
}
