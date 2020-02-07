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
        public MultiNodeTestResult(string testName, TestStatus status)
        {
            TestName = testName;
            Status = status;
        }

        /// <summary>
        /// Full name of executed test
        /// </summary>
        public string TestName { get; }
        /// <summary>
        /// Test result
        /// </summary>
        public TestStatus Status { get; }
        
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