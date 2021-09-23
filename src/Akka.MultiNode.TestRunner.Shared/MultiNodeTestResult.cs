using System.Collections.Generic;
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
            if (string.IsNullOrWhiteSpace(Test.SkipReason))
            {
                NodeResults = Test.Nodes.Select(n => new NodeResult
                {
                    Index = n.Node,
                    Role = n.Role
                }).ToArray();
            }
            else
            {
                NodeResults = new NodeResult[0];
            }
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
                return NodeResults.Any(result => result.Result == TestStatus.Failed) ? TestStatus.Failed : TestStatus.Passed;
            } 
        }
        /// <summary>
        /// Node Test results
        /// </summary>
        public NodeResult[] NodeResults { get; }
        
        public string ConsoleOutput { get; set; }

        public List<Attachment> Attachments { get; } = new List<Attachment>();

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder($"Test {Test.TestName}: {Status}");
            if (Test.SkipReason != null)
                sb.Append(" Skipped: ").Append(Test.SkipReason);
            foreach (var node in NodeResults)
            {
                sb.Append($"\n\tNode {node.Index} [{node.Role}]: {node.Result}");
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
        
        public class NodeResult
        {
            public int Index { get; set; }
            public string Role { get; set; }
            public TestStatus Result { get; set; }
        }
        
        public class Attachment
        {
            public string Title { get; set; }
            public string Path { get; set; }
        }
    }
}