About

  Shader Graph templates and sub graphs for Amplify Impostors, for URP and HDRP. They are an
  alternative for projects that want to stay within Shader Graph. They bake and render with the
  regular Amplify Impostor component.

Performance Note

  For best performance we recommend the included Amplify Impostors shaders, or shaders made
  with Amplify Shader Editor. The Shader Graph versions write per-pixel depth without
  conservative depth, which disables early depth testing (early-z) and makes heavy overdraw
  noticeably more expensive on the GPU. The included Amplify Impostors shaders and the Amplify
  Shader Editor shaders use conservative depth and keep early-z.

Requirements

  * Unity 2022.3 or newer
  * URP or HDRP 14 or newer, and Shader Graph

  Without URP or HDRP installed, the scripts in this folder are excluded from compilation and
  cause no compile errors.

Contents

  * Templates for URP, in "URP > Templates"
    - Octahedron Impostor URP
    - Spherical Impostor URP
  * Templates for HDRP, in "HDRP > Templates"
    - Octahedron Impostor HDRP
    - Spherical Impostor HDRP
  * Shader names follow "Amplify Impostors/Shader Graph/<template name>"
  * The Octahedron templates cover both Octahedron and HemiOctahedron bakes (the Hemi toggle is
    set by the bake, same _Hemi property as the default shaders)
  * Sub graphs, in "SubGraphs", for building your own graphs
    - Octahedron Impostor Vertex / Fragment
    - Spherical Impostor Vertex / Fragment
  * Shader Graph's URP Lit target cannot write depth, so the URP templates use a custom target,
    "Lit (Depth Offset)", shipped in "URP > Editor". The HDRP templates use HDRP's native
    Depth Offset.

How to Use

  To bake with a Shader Graph shader:

  1) Create a Bake Preset (or duplicate "AmplifyImpostors > Plugins > EditorResources > Presets >
     BakePreset") and set its Runtime Shader to one of the templates. Use Octahedron for
     Octahedron and HemiOctahedron bakes, and Spherical for Spherical bakes
  2) Assign that preset on the Amplify Impostor component
  3) Press "Bake Impostor"

  Without a preset Runtime Shader, the bake uses the default Amplify Impostors shaders.

  The URP and HDRP samples include a ready-made "Shader Graph Preset" and a
  "Barrels & Boulders SG" scene.

Custom Graphs

  Duplicate a template, or build a graph from the sub graphs.

  * Keep the hidden baked properties, with the same reference names:
    - _Frames, _FramesX, _FramesY, _ImpostorSize, _Offset, _AI_SizeOffset, _AI_BoundsMin,
      _AI_BoundsSize, _DepthSize, _AI_AngleRange
  * Keep the hidden _AI_ImpostorType property, with the same reference name
  * The baker fills these in, and the Amplify Impostor inspector reads _AI_ImpostorType to know
    the shader's impostor type, since Shader Graph cannot write SubShader tags
  * The templates use a custom material inspector that hides these baked properties. If you set
    your own Custom Editor GUI on the graph, they will show up in the material inspector
  * Feed the Hemi input of the Octahedron Vertex sub graph with a Boolean property that has the
    reference name _Hemi, the baker sets it and the default shaders use the same name

Supported Features

  * Dynamic lights and shadows
  * Depth writing for object intersections
  * LOD cross fade (dithered)
  * GPU instancing

Known Differences from the Default Shaders

  * Hemi uses the same _Hemi material property as the default shaders, as a runtime branch
    instead of a shader keyword
  * The other toggles follow Shader Graph naming. For example, Use SpeedTree Hue is
    EFFECT_HUE_VARIATION where the default shaders use _Hue
  * The Shader Graph material inspectors carry Use SpeedTree Hue and the texture map toggles
    over when switching an impostor material between the default shaders and the Shader Graph
    ones in the inspector. Other ways of changing the shader (scripts, the Amplify Impostor
    component) do not, so rebake or check its toggles
