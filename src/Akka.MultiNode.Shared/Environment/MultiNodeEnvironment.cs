using System;
using Akka.MultiNode.Shared.Environment;

namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// MultiNodeEnvironment
    /// </summary>
    public static class MultiNodeEnvironment
    {
        /// <summary>
        /// Initializes multi-node test environment. Used by <see cref="MultiNodeTestRunner"/> and NodeRunner.
        /// </summary>
        public static void Initialize()
        {
            Environment.SetEnvironmentVariable(CustomMultiNodeFactAttribute.MultiNodeTestEnvironmentName, "1");
        }
    }
}