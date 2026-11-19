// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedWater3
{
    public static partial class RenderTargetDebugger
    {
        public class RenderTarget
        {
            public string name;
            public int order = 1000;
            
            public string description = string.Empty;

            public string textureName;
            public int propertyID;
        }
        
        [AutoStaticsCleanup]
        public static List<RenderTarget> renderTargets = new List<RenderTarget>();
        //For dropdown menus
        [AutoStaticsCleanup]
        public static string[] renderTargetNames;

        [NoAutoStaticsCleanup]
        public static int InspectedProperty = -1;
        [NoAutoStaticsCleanup]
        public static RTHandle CurrentRT;
        [NoAutoStaticsCleanup]
        public static string CurrentCameraName;

        public static void Initialize()
        {
            renderTargets.Clear();
        
#if UNITY_6000_4_OR_NEWER
            var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var assembly in assemblies)
            {
                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;

                    if (type.IsSubclassOf(typeof(RenderTarget)))
                    {
                        //Debug.Log($"Found {type}");

                        RenderTarget rt = Activator.CreateInstance(type) as RenderTarget;
                        
                        renderTargets.Add(rt);
                    }
                }
            }

            renderTargets = renderTargets.OrderBy(o => o.order).ToList();

            renderTargetNames = new string[renderTargets.Count];
            for (int i = 0; i < renderTargetNames.Length; i++)
            {
                renderTargetNames[i] = renderTargets[i].name;
            }
        }

        public static void Cleanup()
        {
            InspectedProperty = -1;
            CurrentRT?.Release();
            CurrentCameraName = string.Empty;
        }
    }
}