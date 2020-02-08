using Akka.MultiNode.Shared;

namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// MultiNodeTestResult
    /// </summary>
    public class MultiNodeTestResult
    {
        /// <summary>
        /// MultiNodeTestResult
        /// </summary>
        public MultiNodeTestResult(MultiNodeSpec spec, NodeTest test, TestStatus status)
        {
            Spec = spec;
            Test = test;
            Status = status;
        }

        /// <summary>
        /// Spec name
        /// </summary>
        public MultiNodeSpec Spec { get; }
        /// <summary>
        /// Full name of executed test
        /// </summary>
        public NodeTest Test { get; }
        /// <summary>
        /// Test result
        /// </summary>
        public TestStatus Status { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Spec {Spec.SpecName} result for {Test.MethodName}: {Status}";
        }

        public enum TestStatus
        {
            /// <summary>
            /// Test passed
            /// </summary>
            Passed,
            /// <summary>
            /// Test skipped
            /// </summary>
            Skipped,
            /// <summary>
            /// Test failed
            /// </summary>
            Failed
        }
    }
}