// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AmplifyImpostors.ShaderGraph
{
	sealed class AIDepthOffsetLitGUI : ShaderGraphLitGUI
	{
		MaterialProperty[] m_Properties;

		public override void FindProperties( MaterialProperty[] properties )
		{
			m_Properties = properties;
			base.FindProperties( properties );
		}

		// keyword toggles that shader graph and the default shaders both use, the keyword state is authoritative
		static readonly string[] KeepKeywords = { "EFFECT_HUE_VARIATION", "_SPECULARMAP", "_OCCLUSIONMAP", "_EMISSIONMAP", "_POSITIONMAP" };

		public override void AssignNewShaderToMaterial( Material material, Shader oldShader, Shader newShader )
		{
			// the base class clears all keywords and the property drawers apply the toggle floats, which the default shaders do not have
			var states = new bool[ KeepKeywords.Length ];
			for ( int i = 0; i < KeepKeywords.Length; i++ )
				states[ i ] = material.IsKeywordEnabled( KeepKeywords[ i ] );

			base.AssignNewShaderToMaterial( material, oldShader, newShader );

			for ( int i = 0; i < KeepKeywords.Length; i++ )
			{
				// only the hue keyword has a toggle property, the texture map keywords are not exposed
				if ( newShader.FindPropertyIndex( KeepKeywords[ i ] ) >= 0 )
					material.SetFloat( KeepKeywords[ i ], states[ i ] ? 1 : 0 );

				if ( states[ i ] )
					material.EnableKeyword( KeepKeywords[ i ] );
				else
					material.DisableKeyword( KeepKeywords[ i ] );
			}
		}

		public override void ValidateMaterial( Material material )
		{
			base.ValidateMaterial( material );

			// the default shaders call the hue toggle _Hue, keep it in sync for switching back
			if ( material.HasProperty( "EFFECT_HUE_VARIATION" ) )
				material.SetFloat( "_Hue", material.GetFloat( "EFFECT_HUE_VARIATION" ) );
		}

		public override void DrawSurfaceInputs( Material material )
		{
			// shader graph's drawer ignores [HideInInspector]
		#if UNITY_6000_6_OR_NEWER
			DrawShaderGraphProperties( WithoutHidden( material.shader, m_Properties ) );
		#else
			DrawShaderGraphProperties( material, WithoutHidden( material.shader, m_Properties ) );
		#endif
		}

		static MaterialProperty[] WithoutHidden( Shader shader, MaterialProperty[] properties )
		{
			if ( properties == null )
				return properties;

			var visible = new List<MaterialProperty>( properties.Length );
			foreach ( var property in properties )
			{
				int index = shader.FindPropertyIndex( property.name );
				if ( index < 0 || ( shader.GetPropertyFlags( index ) & ShaderPropertyFlags.HideInInspector ) == 0 )
					visible.Add( property );
			}
			return visible.ToArray();
		}
	}
}
