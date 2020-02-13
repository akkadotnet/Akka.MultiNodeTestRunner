namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// Runtime Detector
    /// </summary>
    public static class PlatformDetector
    {
#if CORECLR
        public static readonly PlatformType Current = PlatformType.NetCore;
#else
        public static readonly PlatformType Current = PlatformType.NetFramework;
#endif

        /// <summary>
        /// Shows if current runtime is .NET Core
        /// </summary>
        public static readonly bool IsNetCore = Current == PlatformType.NetCore;
        
        /// <summary>
        /// PlatformType
        /// </summary>
        public enum PlatformType
        {
            /// <summary>
            /// Full .NET Framework
            /// </summary>
            NetFramework,
            /// <summary>
            /// .NET Core
            /// </summary>
            NetCore
        }
    }
}