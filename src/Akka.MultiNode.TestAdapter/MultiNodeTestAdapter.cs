using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Akka.MultiNode.TestAdapter.Internal;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Resources;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

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
        /// <param name="rawTestCases">Tests to be run.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<TestCase> rawTestCases, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var testCases = rawTestCases.ToList();
            var testResults = new ConcurrentDictionary<string, TestResult>();
            var options = BuildOptions(runContext, frameworkHandle);

            foreach (var group in testCases.GroupBy(t => Path.GetFullPath(t.Source)))
            {
                var assemblyPath = group.Key;
                var groupOptions = options;
                if (options.OutputDirectory == null || options.DesignMode)
                    groupOptions =
                        options.WithOutputDirectory(
                            Path.Combine(Path.GetDirectoryName(assemblyPath) ?? "", 
                            "TestResults"));
                
                var tests = GetTests(assemblyPath, group.GetEnumerator());
                using (var runner = CreateRunner(frameworkHandle, testResults, assemblyPath))
                {
                    foreach (var test in tests)
                    {
                        try
                        {
                            runner.ExecuteSpec(test, groupOptions);
                        }
                        catch (Exception ex)
                        {
                            frameworkHandle.SendMessage(TestMessageLevel.Error, $"Failed during test execution: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Runs 'all' the tests present in the specified 'containers'. 
        /// </summary>
        /// <param name="sources">Path to test container files to look for tests in.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var options = BuildOptions(runContext, frameworkHandle);
            var testResults = new ConcurrentDictionary<string, TestResult>();
            
            var testAssemblyPaths = sources.ToList();
            frameworkHandle.SendMessage(TestMessageLevel.Informational, $"Loading tests from assemblies: {string.Join(", ", testAssemblyPaths)}");
            
            foreach (var maybeRelativeAssemblyPath in testAssemblyPaths)
            {
                var assemblyPath = Path.GetFullPath(maybeRelativeAssemblyPath);
                using (var runner = CreateRunner(frameworkHandle, testResults, assemblyPath))
                {
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
        
        private static MultiNodeTestRunnerOptions BuildOptions(IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var runSettings = runContext.RunSettings;
            if (runSettings == null)
            {
                return MultiNodeTestRunnerOptions.Default;
            }
            
            var runConfiguration = XmlRunSettingsUtilities.GetRunConfigurationNode(runSettings.SettingsXml);
            var options = GetMultiNodeTestRunnerOptions(runSettings.SettingsXml);
            
            if (runConfiguration.DesignModeSet)
                options = options.WithDesignMode(runConfiguration.DesignMode);
            if (runConfiguration.ResultsDirectorySet)
                options = options.WithOutputDirectory(runConfiguration.ResultsDirectory);
            
            return options;
        }

        private static MultiNodeTestRunner CreateRunner(
            IFrameworkHandle frameworkHandle,
            ConcurrentDictionary<string, TestResult> testResults,
            string assemblyPath)
        {
            var localFrameworkHandle = frameworkHandle;
            var localTestResults = testResults;
            var localAssemblyPath = assemblyPath;
            
            #region Callbacks
            void RecordEnd(TestResult result)
            {
                localFrameworkHandle.RecordEnd(result.TestCase, result.Outcome);
                localFrameworkHandle.RecordResult(result);
            }

            void TestStarted(MultiNodeTest test)
            {
                var result = new TestResult(new TestCase(test.TestName, Constants.ExecutorUri, localAssemblyPath));
                localTestResults[test.TestName] = result;
                
                result.StartTime = DateTimeOffset.Now;
                localFrameworkHandle.RecordStart(result.TestCase);
            }

            void TestSkipped(MultiNodeTest test, string reason)
            {
                var result = localTestResults[test.TestName];
                result.Outcome = TestOutcome.Skipped;
                result.EndTime = DateTimeOffset.Now;
                result.Messages.Add(new TestResultMessage(
                    TestResultMessage.AdditionalInfoCategory, $"Skipped: {reason}"));
                RecordEnd(result);
            }

            void TestPassed(MultiNodeTestResult testResult)
            {
                var result = localTestResults[testResult.Test.TestName];
                result.Outcome = TestOutcome.Passed;
                result.EndTime = DateTimeOffset.Now;
                result.Duration = result.EndTime - result.StartTime;
                if(!string.IsNullOrWhiteSpace(testResult.ConsoleOutput))
                    result.Messages.Add(new TestResultMessage(
                        TestResultMessage.StandardOutCategory, testResult.ConsoleOutput));
                
                result.Messages.Add(new TestResultMessage(
                    TestResultMessage.StandardOutCategory, testResult.ToString()));

                var attachments = new AttachmentSet(new Uri(Path.GetDirectoryName(testResult.Test.AssemblyPath) ?? ""), "Test logs");
                result.Attachments.Add(attachments);
                foreach (var entry in testResult.Attachments)
                {
                    attachments.Attachments.Add(UriDataAttachment.CreateFrom(entry.Path, entry.Title));
                }
                RecordEnd(result);
            }

            void TestFailed(MultiNodeTestResult testResult)
            {
                var result = localTestResults[testResult.Test.TestName];
                result.Outcome = TestOutcome.Failed;
                result.EndTime = DateTimeOffset.Now;
                result.Duration = result.EndTime - result.StartTime;
                if(!string.IsNullOrWhiteSpace(testResult.ConsoleOutput))
                    result.Messages.Add(new TestResultMessage(
                        TestResultMessage.StandardOutCategory, testResult.ConsoleOutput));
                
                result.ErrorMessage = testResult.ToString();
                
                var attachments = new AttachmentSet(new Uri(Path.GetDirectoryName(testResult.Test.AssemblyPath) ?? ""), "Test logs");
                result.Attachments.Add(attachments);
                foreach (var entry in testResult.Attachments)
                {
                    attachments.Attachments.Add(UriDataAttachment.CreateFrom(entry.Path, entry.Title));
                }
                RecordEnd(result);
            }

            void Exception(MultiNodeTest test, Exception exception)
            {
                var result = localTestResults[test.TestName];

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
                        result.Duration = result.EndTime - result.StartTime;
                        break;
                }
                        
                RecordEnd(result);
            }
            #endregion
            
            var runner = new MultiNodeTestRunner();
            runner.TestStarted += TestStarted;
            runner.TestSkipped += TestSkipped;
            runner.TestPassed += TestPassed;
            runner.TestFailed += TestFailed;
            runner.Exception += Exception;

            return runner;
        }
        
        private static List<MultiNodeTest> GetTests(string assemblyPath, IEnumerator<TestCase> cases)
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
        
        private static MultiNodeTestRunnerOptions GetMultiNodeTestRunnerOptions(string settingsXml) => 
            GetNodeValue(settingsXml, "MultiNodeTestRunnerOptions", FromXml) ?? MultiNodeTestRunnerOptions.Default;
        
        private static MultiNodeTestRunnerOptions FromXml(XmlReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            
            var options = MultiNodeTestRunnerOptions.Default;
            ThrowOnHasAttributes(reader);
            reader.Read();

            if (!reader.IsEmptyElement)
            { 
                while (reader.NodeType == XmlNodeType.Element)
                {
                    var name = reader.Name;
                    switch (name)
                    {
                        case "ListenAddress":
                        {
                            ThrowOnHasAttributes(reader);
                            var xmlValue = reader.ReadElementContentAsString();
                            options = options.WithListenAddress(xmlValue);
                            continue;
                        }

                        case "ListenPort":
                        {
                            ThrowOnHasAttributes(reader);
                            var xmlValue = reader.ReadElementContentAsString();
                            options = options.WithListenPort(
                                int.TryParse(xmlValue, out var port) && port >= 0
                                    ? port
                                    : throw new SettingsException(
                                        $"Invalid XML settings for {nameof(MultiNodeTestRunnerOptions)} element {name}, value: {xmlValue}"));
                            continue;
                        }

                        case "ClearOutputDirectory":
                        {
                            ThrowOnHasAttributes(reader);
                            var xmlValue = reader.ReadElementContentAsString();
                            if (!bool.TryParse(xmlValue, out var value)) 
                                throw new SettingsException($"Invalid XML settings for {nameof(MultiNodeTestRunnerOptions)} element {name}, value: {xmlValue}");
                            options = options.WithClearOutputDirectory(value);
                            continue;
                        }

                        case "UseTeamCityFormatting":
                        {
                            ThrowOnHasAttributes(reader);
                            var xmlValue = reader.ReadElementContentAsString();
                            if (!bool.TryParse(xmlValue, out var value)) 
                                throw new SettingsException($"Invalid XML settings for {nameof(MultiNodeTestRunnerOptions)} element {name}, value: {xmlValue}");
                            options = options.WithTeamCityFormatting(value);
                            continue;
                        } 

                        default:
                            //if (EqtTrace.IsErrorEnabled)
                              //EqtTrace.Warning($"Invalid XML Element for {nameof(MultiNodeTestRunnerOptions)}, unknown element: {name}");
                            reader.Skip(); 
                            continue; 
                    } 
                }
                reader.ReadEndElement();
            }

            return options;
        }
        
        private static void ThrowOnHasAttributes(XmlReader reader)
        {
            if (reader.HasAttributes)
            {
                var name = reader.Name;
                reader.MoveToNextAttribute();
                throw new SettingsException($"Invalid XML attribute for XML element {name}, attribute: {reader.Name}");
            }
        }
        
        private static T GetNodeValue<T>(
            string settingsXml,
            string nodeName,
            Func<XmlReader, T> nodeParser)
        {
            if (!string.IsNullOrWhiteSpace(settingsXml))
            {
                try
                {
                    using (var stringReader = new StringReader(settingsXml))
                    {
                        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
                        XmlReaderUtilities.ReadToRootNode(reader);
                        reader.ReadToNextElement();
                        while (!string.Equals(reader.Name, nodeName, StringComparison.OrdinalIgnoreCase) && !reader.EOF)
                            reader.SkipToNextElement();
                        if (!reader.EOF)
                            return nodeParser(reader);
                    }
                }
                catch (XmlException ex)
                {
                    throw new SettingsException(string.Format(CultureInfo.CurrentCulture, "{0} {1}", CommonResources.MalformedRunSettingsFile, ex.Message), ex);
                }
            }
            return default;
        }
    }
}