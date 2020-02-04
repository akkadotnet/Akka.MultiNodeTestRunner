using System;
using System.Collections.Generic;
using System.Linq;
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
    public class TestExecutor : ITestExecutor
    {
        /// <summary>
        /// Cancel the execution of the tests.
        /// </summary>
        public void Cancel()
        {
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
            var settings = runContext.RunSettings.SettingsXml;
            frameworkHandle.SendMessage(TestMessageLevel.Error, "RunTests");
        }

        /// <summary>
        /// Runs 'all' the tests present in the specified 'containers'. 
        /// </summary>
        /// <param name="sources">Path to test container files to look for tests in.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var testAssemblyPaths = sources.ToList();
            FileLogger.Write($"Starting tests from {string.Join(", ", testAssemblyPaths)}");
            frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Loading tests from assemblies: {string.Join(", ", testAssemblyPaths)}");
            var settings = runContext.RunSettings.SettingsXml;
            
            foreach (var assemblyPath in testAssemblyPaths)
            {
                var testCase = new TestCase("All tests in assemby", new Uri(ExecutorMetadata.ExecutorUri), assemblyPath);
                frameworkHandle.RecordStart(testCase);
                
                try
                {
                    FileLogger.Write($"Starting tests from {assemblyPath}");
                    var runner = new MultiNode.TestRunner.Shared.MultiNodeTestRunner();
                    var retCode = runner.Execute(assemblyPath);
                    frameworkHandle.RecordEnd(testCase, retCode > 0 ? TestOutcome.Failed : TestOutcome.Passed);
                }
                catch (Exception ex)
                {
                    FileLogger.Write($"Failure: {ex}");
                    frameworkHandle.SendMessage(TestMessageLevel.Error, $"Failed during test execution: {ex}");
                    frameworkHandle.RecordEnd(testCase, TestOutcome.Failed);
                }
                
                frameworkHandle.RecordResult(new TestResult(testCase)
                {
                    Outcome = TestOutcome.Passed
                });
                
                FileLogger.Write($"Finished tests from {assemblyPath}");
            }
        }
    }
}
