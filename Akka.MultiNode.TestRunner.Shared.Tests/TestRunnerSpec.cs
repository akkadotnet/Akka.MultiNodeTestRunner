using System;
using System.IO;
using Akka.MultiNode.TestAdapter.SampleTests;
using Akka.MultiNode.TestRunner.Shared;
using FluentAssertions;
using Xunit;

namespace Akka.MultiNode.TestRunner.Tests
{
    public class TestRunnerSpec
    {
        [Fact]
        public void Should_discover_sample_tests_and_run_them()
        {
            var sampleTestAssemblyPath = SampleTestsMetadata.AssemblyPath;
            File.Exists(sampleTestAssemblyPath).Should().BeTrue($"Assemblies with samples should exist at {sampleTestAssemblyPath}");
            
            var runner = new MultiNodeTestRunner();
            var results = runner.Execute(sampleTestAssemblyPath, MultiNodeTestRunnerOptions.Default);
            results.Should().NotBeEmpty();
        }
    }
}