using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Akka.MultiNode.TestRunner.Shared;
using Akka.Util;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Akka.MultiNode.TestAdapter
{
    /// <summary>
    /// TestExecutor
    /// </summary>
    /// <remarks>
    /// See how it works here: https://github.com/Microsoft/vstest-docs/blob/master/RFCs/0004-Adapter-Extensibility.md
    /// </remarks>
    [ExtensionUri(ExecutorMetadata.ExecutorUri)]
    public class MultiNodeTestExecutor : ITestExecutor
    {
        /// <summary>
        /// Cancel the execution of the tests.
        /// </summary>
        public void Cancel()
        {
            // TODO: Implement proper cancellation
        }

        /// <summary>
        /// Runs only the tests specified by parameter 'tests'. 
        /// </summary>
        /// <remarks>
        /// ITestExecutor.RunTests with a set of test cases gets called in mostly VS IDE scenarios("Run Selected tests" scenarios)
        /// where a discovery operation has already been performed.
        /// This would already have the information as to what ITestExecutor can run the test case via a URI property.
        /// The platform would then just call into that specific executor to run the test cases.
        /// </remarks>
        /// <param name="tests">Tests to be run.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            throw new NotImplementedException("Running from VS is not implemented yet");
            
            // This is called from VS "Run Selected tests" command.
            // Need to get assembly paths and perform specs filtering by name
            List<string> assemblyPaths = null;
            
            var filteredSpecNames = tests.Select(t => t.FullyQualifiedName).ToList();
            RunTestsWithOptions(assemblyPaths, frameworkHandle, new MultiNodeTestRunnerOptions(specNames: filteredSpecNames));
        }

        /// <summary>
        /// Runs 'all' the tests present in the specified 'containers'. 
        /// </summary>
        /// <param name="sources">Path to test container files to look for tests in.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
#if CORECLR
            var options = new MultiNodeTestRunnerOptions(platform: RuntimeDetector.IsWindows ? "net" : "netcore");
#else
            var options = new MultiNodeTestRunnerOptions(platform: "net");
#endif
            RunTestsWithOptions(sources, frameworkHandle, options);
        }

        private void RunTestsWithOptions(IEnumerable<string> sources, IFrameworkHandle frameworkHandle, MultiNodeTestRunnerOptions options)
        {
            var testAssemblyPaths = sources.ToList();
            frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Loading tests from assemblies: {string.Join(", ", testAssemblyPaths)}");

            foreach (var assemblyPath in testAssemblyPaths)
            {
                TestCase BuildTestCase(string name) => new TestCase(name, new Uri(ExecutorMetadata.ExecutorUri), assemblyPath);

                var testCases = new ConcurrentDictionary<string, TestCase>();
                try
                {
                    var runner = new MultiNodeTestRunner();
                    runner.TestStarted += testName =>
                    {
                        var testCase = BuildTestCase(testName);
                        testCases.AddOrUpdate(testName, name => testCase, (name, existingCase) => testCase);
                        frameworkHandle.RecordStart(testCase);
                    };

                    runner.TestFinished += testResult =>
                    {
                        var testCase = testCases[testResult.TestName];
                        frameworkHandle.RecordResult(new TestResult(testCase)
                        {
                            // TODO: Set other props
                            Outcome = MapToOutcome(testResult.Status)
                        });
                        frameworkHandle.RecordEnd(testCase, MapToOutcome(testResult.Status));
                    };
                    
                    runner.Execute(assemblyPath, options);
                }
                catch (Exception ex)
                {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, $"Failed during test execution: {ex}");
                }
            }
        }

        private TestOutcome MapToOutcome(MultiNodeTestResult.TestStatus status)
        {
            switch (status)
            {
                case MultiNodeTestResult.TestStatus.Passed:
                    return TestOutcome.Passed;
                case MultiNodeTestResult.TestStatus.Skipped:
                    return TestOutcome.Skipped;
                case MultiNodeTestResult.TestStatus.Failed:
                    return TestOutcome.Failed;
                default:
                    return TestOutcome.None; // Unknown result
            }
        }
    }
}
