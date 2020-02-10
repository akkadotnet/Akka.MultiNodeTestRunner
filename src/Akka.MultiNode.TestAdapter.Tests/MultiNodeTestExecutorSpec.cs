using System.IO;
using Akka.MultiNode.TestAdapter.SampleTests;
using Akka.MultiNode.TestAdapter.SampleTests.Metadata;
using Akka.MultiNode.TestAdapter.Tests.Helpers;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit;

namespace Akka.MultiNode.TestAdapter.Tests
{
    [Collection(TestCollections.MultiNode)]
    public class MultiNodeTestExecutorSpec
    {
        public string SampleTestsAssemblyPath { get; }

        public MultiNodeTestExecutorSpec()
        {
            SampleTestsAssemblyPath = Path.GetFullPath(SampleTestsMetadata.AssemblyFileName);
            File.Exists(SampleTestsAssemblyPath).Should().BeTrue($"Assemblies with samples should exist at {SampleTestsAssemblyPath}");
        }
        
        [Fact]
        public void Should_run_tests_and_report_results()
        {
            var executor = new MultiNodeTestExecutor();
            var frameworkHandler = new FakeFrameworkHandler();
            executor.RunTests(new[] {SampleTestsAssemblyPath}, new FakeRunContext(), frameworkHandler);
            
            frameworkHandler.TestResults.Should().NotBeEmpty();
            
            Should_report_failures(frameworkHandler);
            Should_report_failures_for_one_node(frameworkHandler);
            Should_report_skipped_specs(frameworkHandler);
            Should_ignore_specs_with_bad_config(frameworkHandler);
        }

        private void Should_report_failures(FakeFrameworkHandler frameworkHandler)
        {
            frameworkHandler.TestResults.Should().Contain(r => r.TestCase.FullyQualifiedName.Contains(nameof(FailedMultiNodeSpec)) && r.Outcome == TestOutcome.Failed,
                                                          "Should report failed spec result");
            frameworkHandler.TestResults.Should().Contain(r => r.Outcome != TestOutcome.Failed, "Should still contain not-failed results");
        }
        
        private void Should_report_failures_for_one_node(FakeFrameworkHandler frameworkHandler)
        {
            frameworkHandler.TestResults
                .Should()
                .Contain(r => r.TestCase.FullyQualifiedName.Contains(nameof(OneNodeFailedMultiNodeSpec)) && r.Outcome == TestOutcome.Failed,
                         "Should report failed spec result when only one node failed");
            frameworkHandler.TestResults.Should().Contain(r => r.Outcome != TestOutcome.Failed, "Should still contain not-failed results");
        }
        
        private void Should_report_skipped_specs(FakeFrameworkHandler frameworkHandler)
        {
            frameworkHandler.TestResults
                .Should()
                .Contain(r => r.TestCase.FullyQualifiedName.Contains(nameof(SkippedMultiNodeSpec)) && r.Outcome == TestOutcome.Skipped,
                         "Should report skipped spec result");
            
            frameworkHandler.TestResults.Should().Contain(r => r.Outcome != TestOutcome.Skipped, "Should still contain not-failed results");
        }
        
        private void Should_ignore_specs_with_bad_config(FakeFrameworkHandler frameworkHandler)
        {
            frameworkHandler.TestResults
                .Should()
                .NotContain(r => r.TestCase.FullyQualifiedName.Contains(nameof(BadConfigMultiNodeSpec)),
                            "Should not even discover specs with bad configuration - because can not build configuration");
            
            frameworkHandler.TestResults.Should().NotBeEmpty("Should still report other spec results");
        }
    }
}