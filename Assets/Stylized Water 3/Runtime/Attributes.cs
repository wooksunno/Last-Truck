#if UNITY_6000_5_OR_NEWER
#define STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
#endif

using System;
using System.Runtime.CompilerServices;

//Dummy attributes so their usage ensures they can be added error-free in older Unity versions
#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
[assembly: InternalsVisibleTo("sc.stylizedwater3.editor")]
namespace StylizedWater3
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class NoAutoStaticsCleanupAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class AutoStaticsCleanupAttribute : Attribute { }
}
#endif

//Dummy attributes in this namespace, so attributes can be added error free in older Unity versions
#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
namespace Unity.Scripting.LifecycleManagement
{
    class DummyClassSW3 { }
}
#endif