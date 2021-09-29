using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Akka.MultiNode.TestAdapter.Tests.Helpers
{
    /// <summary>
    /// Fake to pass to <see cref="ITestExecutor.RunTests"/> method
    /// </summary>
    class FakeFrameworkHandler : IFrameworkHandle
    {
        public List<(TestMessageLevel Level, string Message)> Messages { get; } = new List<(TestMessageLevel Level, string Message)>();
        public List<TestResult> TestResults { get; } = new List<TestResult>();
        public List<TestCase> StartedTestCases { get; } = new List<TestCase>();
        public List<(TestCase TestCase, TestOutcome Outcome)> FinishedTestCases { get; } = new List<(TestCase TestCase, TestOutcome Outcome)>();

        /// <inheritdoc />
        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            Messages.Add((testMessageLevel, message));
        }

        /// <inheritdoc />
        public void RecordResult(TestResult testResult)
        {
            TestResults.Add(testResult);
        }

        /// <inheritdoc />
        public void RecordStart(TestCase testCase)
        {
            StartedTestCases.Add(testCase);
        }

        /// <inheritdoc />
        public void RecordEnd(TestCase testCase, TestOutcome outcome)
        {
            FinishedTestCases.Add((testCase, outcome));
        }

        /// <inheritdoc />
        public void RecordAttachments(IList<AttachmentSet> attachmentSets)
        {
        }

        /// <inheritdoc />
        public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments,
                                                    IDictionary<string, string> environmentVariables)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool EnableShutdownAfterTestRun { get; set; }
    }
}