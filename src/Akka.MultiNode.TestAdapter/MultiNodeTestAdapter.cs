using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Akka.MultiNode.Shared;
using Akka.MultiNode.Shared.Environment;
using Akka.MultiNode.TestRunner.Shared;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Xunit.Sdk;
using TestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;

namespace Akka.MultiNode.TestAdapter
{
    /// <summary>
    /// TestDiscoverer
    /// </summary>
    /// <remarks>
    /// See how it works here: https://github.com/Microsoft/vstest-docs/blob/master/RFCs/0004-Adapter-Extensibility.md
    /// </remarks>
    [FileExtension(".dll")]
    [FileExtension(".exe")]
    [DefaultExecutorUri(Constants.ExecutorUriString)]
    [ExtensionUri(Constants.ExecutorUriString)]
    public class MultiNodeTestAdapter : ITestDiscoverer, ITestExecutor
    {
        public MultiNodeTestAdapter()
        {
            MultiNodeEnvironment.Initialize();
        }
        
        /// <summary>
        /// Discovers the tests available from the provided container.
        /// </summary>
        /// <param name="sources">Collection of test containers.</param>
        /// <param name="discoveryContext">Context in which discovery is being performed.</param>
        /// <param name="logger">Logger used to log messages.</param>
        /// <param name="discoverySink">Used to send testcases and discovery related events back to Discoverer manager.</param>
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            foreach (var assemblyPath in sources)
            {
                var (specs, errors) = MultiNodeTestRunner.DiscoverSpecs(assemblyPath);

                foreach (var discoveryErrorMessage in errors.SelectMany(e => e.Messages))
                {
                    logger.SendMessage(TestMessageLevel.Error, discoveryErrorMessage);
                }

                foreach (var discoveredSpec in specs)
                {
                    discoverySink.SendTestCase(new TestCase(discoveredSpec.TestName, new Uri(Constants.ExecutorUriString), assemblyPath));
                }
            }
        }
        
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
        public void RunTests(IEnumerable<TestCase> rawTests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            // throw new NotImplementedException("Running from VS is not implemented yet");
            var tests = rawTests.ToList();
            RunTestsWithOptions(tests, frameworkHandle, MultiNodeTestRunnerOptions.Default);
        }

        /// <summary>
        /// Runs 'all' the tests present in the specified 'containers'. 
        /// </summary>
        /// <param name="sources">Path to test container files to look for tests in.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            RunTestsWithOptions(sources, frameworkHandle, MultiNodeTestRunnerOptions.Default);
        }

        private void RunTestsWithOptions(IEnumerable<string> sources, IFrameworkHandle frameworkHandle, MultiNodeTestRunnerOptions options)
        {
            var testResults = new ConcurrentDictionary<string, TestResult>();
            
            var testAssemblyPaths = sources.ToList();
            frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Loading tests from assemblies: {string.Join(", ", testAssemblyPaths)}");
            
            foreach (var maybeRelativeAssemblyPath in testAssemblyPaths)
            {
                var assemblyPath = Path.GetFullPath(maybeRelativeAssemblyPath);

                #region Callbacks
                void RecordEnd(TestResult result)
                {
                    frameworkHandle.RecordEnd(result.TestCase, result.Outcome);
                    frameworkHandle.RecordResult(result);
                }

                void TestStarted(MultiNodeTest test)
                {
                    var result = new TestResult(new TestCase(test.TestName, Constants.ExecutorUri, assemblyPath));
                    testResults[test.TestName] = result;
                    
                    result.StartTime = DateTimeOffset.Now;
                    frameworkHandle.RecordStart(result.TestCase);
                }

                void TestSkipped(MultiNodeTest test, string reason)
                {
                    var result = testResults[test.TestName];
                    result.Outcome = TestOutcome.Skipped;
                    result.EndTime = DateTimeOffset.Now;
                    result.Messages.Add(new TestResultMessage(
                        TestResultMessage.AdditionalInfoCategory, $"Skipped: {reason}"));
                    RecordEnd(result);
                }

                void TestPassed(MultiNodeTestResult testResult)
                {
                    var result = testResults[testResult.Test.TestName];
                    result.Outcome = TestOutcome.Passed;
                    result.EndTime = DateTimeOffset.Now;
                    result.Duration = result.StartTime - result.EndTime;
                    result.Messages.Add(new TestResultMessage(
                        TestResultMessage.StandardOutCategory, testResult.ToString()));
                    RecordEnd(result);
                }

                void TestFailed(MultiNodeTestResult testResult)
                {
                    var result = testResults[testResult.Test.TestName];
                    result.Outcome = TestOutcome.Failed;
                    result.EndTime = DateTimeOffset.Now;
                    result.Duration = result.StartTime - result.EndTime;
                    result.ErrorMessage = testResult.ToString();
                    RecordEnd(result);
                }

                void Exception(MultiNodeTest test, Exception exception)
                {
                    var result = testResults[test.TestName];

                    switch (exception)
                    {
                        case TestConfigurationException ex:
                            result.Outcome = TestOutcome.Skipped;
                            result.EndTime = DateTimeOffset.Now;
                            result.Messages.Add(new TestResultMessage(
                                TestResultMessage.AdditionalInfoCategory, $"Skipped: {ex.Message}"));
                            break;
                                
                        case TestBaseTypeException ex:
                            result.Outcome = TestOutcome.Skipped;
                            result.EndTime = DateTimeOffset.Now;
                            result.Messages.Add(new TestResultMessage(
                                TestResultMessage.AdditionalInfoCategory, $"Skipped: {ex.Message}"));
                            break;
                                
                        default:
                            result.Outcome = TestOutcome.Failed;
                            result.ErrorMessage = exception.Message;
                            result.ErrorStackTrace = exception.StackTrace;
                            result.EndTime = DateTimeOffset.Now;
                            result.Duration = result.StartTime - result.EndTime;
                            break;
                    }
                            
                    RecordEnd(result);
                }
                #endregion

                using (var runner = new MultiNodeTestRunner())
                {
                    runner.TestStarted += TestStarted;
                    runner.TestSkipped += TestSkipped;
                    runner.TestPassed += TestPassed;
                    runner.TestFailed += TestFailed;
                    runner.Exception += Exception;
                        
                    try
                    {
                        runner.ExecuteAssembly(assemblyPath, options);
                    }
                    catch (Exception ex)
                    {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, $"Failed during test execution: {ex}");
                    }
                }
            }
        }
        
        private void RunTestsWithOptions(IEnumerable<TestCase> rawTestCases, IFrameworkHandle frameworkHandle, MultiNodeTestRunnerOptions options)
        {
            var testCases = rawTestCases.ToList();
            var testResults = new ConcurrentDictionary<string, TestResult>(
                testCases.Select(t => new KeyValuePair<string, TestResult>(t.FullyQualifiedName, new TestResult(t)))
            );

            #region Callbacks
            void RecordEnd(TestResult result)
            {
                frameworkHandle.RecordEnd(result.TestCase, result.Outcome);
                frameworkHandle.RecordResult(result);
            }

            void TestStarted(MultiNodeTest test)
            {
                var result = testResults[test.TestName];
                result.StartTime = DateTimeOffset.Now;
                frameworkHandle.RecordStart(result.TestCase);
            }

            void TestSkipped(MultiNodeTest test, string reason)
            {
                var result = testResults[test.TestName];
                result.Outcome = TestOutcome.Skipped;
                result.EndTime = DateTimeOffset.Now;
                result.Messages.Add(new TestResultMessage(
                    TestResultMessage.AdditionalInfoCategory, $"Skipped: {reason}"));
                RecordEnd(result);
            }

            void TestPassed(MultiNodeTestResult testResult)
            {
                var result = testResults[testResult.Test.TestName];
                result.Outcome = TestOutcome.Passed;
                result.EndTime = DateTimeOffset.Now;
                result.Duration = result.StartTime - result.EndTime;
                result.Messages.Add(new TestResultMessage(
                    TestResultMessage.StandardOutCategory, testResult.ToString()));
                RecordEnd(result);
            }

            void TestFailed(MultiNodeTestResult testResult)
            {
                var result = testResults[testResult.Test.TestName];
                result.Outcome = TestOutcome.Failed;
                result.EndTime = DateTimeOffset.Now;
                result.Duration = result.StartTime - result.EndTime;
                result.ErrorMessage = testResult.ToString();
                RecordEnd(result);
            }

            void Exception(MultiNodeTest test, Exception exception)
            {
                var result = testResults[test.TestName];

                switch (exception)
                {
                    case TestConfigurationException ex:
                        result.Outcome = TestOutcome.Skipped;
                        result.EndTime = DateTimeOffset.Now;
                        result.Messages.Add(new TestResultMessage(
                            TestResultMessage.AdditionalInfoCategory, $"Skipped: {ex.Message}"));
                        break;
                            
                    case TestBaseTypeException ex:
                        result.Outcome = TestOutcome.Skipped;
                        result.EndTime = DateTimeOffset.Now;
                        result.Messages.Add(new TestResultMessage(
                            TestResultMessage.AdditionalInfoCategory, $"Skipped: {ex.Message}"));
                        break;
                            
                    default:
                        result.Outcome = TestOutcome.Failed;
                        result.ErrorMessage = exception.Message;
                        result.ErrorStackTrace = exception.StackTrace;
                        result.EndTime = DateTimeOffset.Now;
                        result.Duration = result.StartTime - result.EndTime;
                        break;
                }
                        
                RecordEnd(result);
            }
            #endregion
            
            foreach (var group in testCases.GroupBy(t => Path.GetFullPath(t.Source)))
            {
                var tests = GetTests(group.Key, group.GetEnumerator());
                foreach (var test in tests)
                {
                    using (var runner = new MultiNodeTestRunner())
                    {
                        runner.TestStarted += TestStarted;
                        runner.TestSkipped += TestSkipped;
                        runner.TestPassed += TestPassed;
                        runner.TestFailed += TestFailed;
                        runner.Exception += Exception;
                        
                        try
                        {
                            runner.ExecuteSpec(test, options);
                        }
                        catch (Exception ex)
                        {
                            frameworkHandle.SendMessage(TestMessageLevel.Error, $"Failed during test execution: {ex}");
                        }
                    }
                }
            }
        }

        private List<MultiNodeTest> GetTests(string assemblyPath, IEnumerator<TestCase> cases)
        {
            var tests = MultiNodeTestRunner.DiscoverSpecs(assemblyPath);
            var result = new List<MultiNodeTest>();
            while (cases.MoveNext())
            {
                var c = cases.Current;
                result.Add(tests.Tests.First(t => t.TestName.Equals(c.FullyQualifiedName))); 
            }

            return result;
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