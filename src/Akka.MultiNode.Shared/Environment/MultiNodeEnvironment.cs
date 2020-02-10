using Akka.Remote.TestKit;

namespace Akka.MultiNode.Shared.Environment
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
            System.Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, "1");
        }
    }
}