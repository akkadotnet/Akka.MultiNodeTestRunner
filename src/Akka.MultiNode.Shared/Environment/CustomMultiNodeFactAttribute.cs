using System;
using Xunit;

namespace Akka.MultiNode.Shared.Environment
{
    // TODO: Remove this after Akka.Cluster.TestKit.MultiNodeFactAttribute will be updated (https://github.com/akkadotnet/akka.net/issues/4188)
    public class CustomMultiNodeFactAttribute : FactAttribute
    {
        /// <summary>
        /// Set by MultiNodeTestRunner when running multi-node tests
        /// </summary>
        public const string MultiNodeTestEnvironmentName = "__AKKA_MULTI_NODE_ENVIRONMENT";

        private static readonly Lazy<bool> ExecutedByMultiNodeRunner = new Lazy<bool>(() =>
        {
            return System.Environment.GetEnvironmentVariable(MultiNodeTestEnvironmentName) != null;
        });

        public override string Skip
        {
            get => !ExecutedByMultiNodeRunner.Value ? "Must be executed by multi-node test runner" : base.Skip;
            set => base.Skip = value;
        }
    }
}