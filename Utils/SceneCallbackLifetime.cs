using System.Threading;

namespace DedicatedServerMod.Utils
{
    /// <summary>Invalidates deferred callbacks captured before a dedicated-session scene cleanup.</summary>
    internal static class SceneCallbackLifetime
    {
        private static long _generation;
        internal static long Generation => Interlocked.Read(ref _generation);
        internal static void Invalidate() => Interlocked.Increment(ref _generation);
        internal static bool IsCurrent(long generation) => generation == Generation;
    }
}
