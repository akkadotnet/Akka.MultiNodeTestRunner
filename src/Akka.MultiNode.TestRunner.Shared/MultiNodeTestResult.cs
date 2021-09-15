using System.Linq;
using System.Text;
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
        public MultiNodeTestResult(MultiNodeTest test)
        {
            Test = test;
            NodeResults = string.IsNullOrWhiteSpace(Test.SkipReason)
                ? new TestStatus[Test.Nodes.Count]
                : new TestStatus[0];
        }

        /// <summary>
        /// Test name
        /// </summary>
        public MultiNodeTest Test { get; }
        /// <summary>
        /// Test result
        /// </summary>
        public TestStatus Status {
            get
            {
                if (!string.IsNullOrWhiteSpace(Test.SkipReason))
                    return TestStatus.Skipped;
                return NodeResults.Any(result => result == TestStatus.Failed) ? TestStatus.Failed : TestStatus.Passed;
            } 
        }
        /// <summary>
        /// Node Test results
        /// </summary>
        public TestStatus[] NodeResults { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder($"Test {Test.TestName}: {Status}");
            if (Test.SkipReason != null)
                sb.Append(" Skipped: ").Append(Test.SkipReason);
            for (var i = 0; i < NodeResults.Length; i++)
            {
                sb.Append("\n\tNode ").Append(i).Append(": ").Append(NodeResults[i]);
            }

            return sb.ToString();
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