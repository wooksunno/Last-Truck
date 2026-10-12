// Made with Amplify Shader Editor v1.9.9.5
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "AE/Leaves"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.2
		_Color( "Color", Color ) = ( 0.9716981, 0.9716981, 0.9716981, 0 )
		_Ambient_Occlusion( "Ambient_Occlusion", Range( 0, 3 ) ) = 0
		_Base_Color( "Base_Color", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TreeTransparentCutout"  "Queue" = "Geometry+0" }
		Cull Off
		CGPROGRAM
		#pragma target 3.0
		#define ASE_VERSION 19905
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
		};

		uniform float4 _Color;
		uniform sampler2D _Base_Color;
		uniform float4 _Base_Color_ST;
		uniform float _Ambient_Occlusion;
		uniform float _Cutoff = 0.2;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Base_Color = i.uv_texcoord * _Base_Color_ST.xy + _Base_Color_ST.zw;
			float4 tex2DNode89 = tex2D( _Base_Color, uv_Base_Color );
			o.Albedo = ( _Color * tex2DNode89 ).rgb;
			float lerpResult168 = lerp( 1.0 , i.vertexColor.r , _Ambient_Occlusion);
			o.Occlusion = lerpResult168;
			o.Alpha = 1;
			clip( tex2DNode89.a - _Cutoff );
		}

		ENDCG
	}
	Fallback "Unlit/Color"
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19905
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;144;560,-880;Inherit;False;Property;_Color;Color;1;0;Create;True;0;0;0;False;0;False;0.9716981,0.9716981,0.9716981,0;0.7132074,0.7132074,0.7132074,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;89;-304,-704;Inherit;True;Property;_Base_Color;Base_Color;4;0;Create;True;0;0;0;False;0;False;-1;None;631b73107c7720043be838b3c358168f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;167;48,320;Inherit;False;Property;_Ambient_Occlusion;Ambient_Occlusion;2;0;Create;True;0;0;0;False;0;False;0;0.551;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;166;144,80;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;165;-720,-288;Inherit;False;Property;_Tilling;Tilling;5;0;Create;True;0;0;0;False;0;False;0,0;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;155;-368,-160;Inherit;False;Property;_Tilling_Color;Tilling_Color;3;0;Create;True;0;0;0;False;0;False;1.49;1.66;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;154;-528,-368;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NoiseGeneratorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;152;0,-336;Inherit;True;Simplex2D;True;False;2;0;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;143;768,-592;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;168;464,160;Inherit;True;3;0;FLOAT;1;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;1461.67,-241.5397;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;0;Standard;AE/Leaves;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Custom;0.2;True;True;0;True;TreeTransparentCutout;;Geometry;All;14;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;0;4;10;25;False;0.5;True;0;5;False;;10;False;;0;4;False;;1;False;;0;False;;1;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;Unlit/Color;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;154;0;165;0
WireConnection;152;0;154;0
WireConnection;152;1;155;0
WireConnection;143;0;144;0
WireConnection;143;1;89;0
WireConnection;168;1;166;1
WireConnection;168;2;167;0
WireConnection;0;0;143;0
WireConnection;0;5;168;0
WireConnection;0;10;89;4
ASEEND*/
//CHKSM=97585D93B8F2791836982B08006A332680BAEC25