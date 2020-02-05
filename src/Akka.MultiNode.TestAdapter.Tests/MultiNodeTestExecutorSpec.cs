using System.IO;
using Akka.MultiNode.TestAdapter.SampleTests;
using Akka.MultiNode.TestAdapter.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Akka.MultiNode.TestAdapter.Tests
{
    public class MultiNodeTestExecutorSpec
    {
        [Fact]
        public void Should_run_tests_and_report_results()
        {
            var sampleTestAssemblyPath = SampleTestsMetadata.AssemblyPath;
            File.Exists(sampleTestAssemblyPath).Should().BeTrue($"Assemblies with samples should exist at {sampleTestAssemblyPath}");

            var executor = new MultiNodeTestExecutor();
            var frameworkHandler = new FakeFrameworkHandler();
            executor.RunTests(new []{ sampleTestAssemblyPath }, new FakeRunContext(), frameworkHandler);
            frameworkHandler.TestResults.Should().NotBeEmpty();
        }
    }
}