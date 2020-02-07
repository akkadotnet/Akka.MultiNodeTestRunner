using System;
using System.Collections.Generic;
using System.Linq;
using Akka.MultiNode.TestRunner.Shared;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Akka.MultiNode.TestAdapter
{
    /// <summary>
    /// TestDiscoverer
    /// </summary>
    /// <remarks>
    /// See how it works here: https://github.com/Microsoft/vstest-docs/blob/master/RFCs/0004-Adapter-Extensibility.md
    /// </remarks>
    [FileExtension(".dll")]
    [DefaultExecutorUri(ExecutorMetadata.ExecutorUri)]
    public class TestDiscoverer : ITestDiscoverer
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
                    discoverySink.SendTestCase(new TestCase(discoveredSpec.SpecName, new Uri(ExecutorMetadata.ExecutorUri), assemblyPath));
                }
            }
        }
    }
}