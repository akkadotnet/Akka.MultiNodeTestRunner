using System.Collections.Generic;
using System.Linq;
using Akka.MultiNode.Shared;

namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// Spec found by <see cref="Discovery"/>
    /// </summary>
    public class MultiNodeSpec
    {
        public MultiNodeSpec(string specName, List<NodeTest> tests)
        {
            SpecName = specName;
            Tests = tests;
        }

        public string SpecName { get; }
        public List<NodeTest> Tests { get; }

        public NodeTest FirstTest => Tests.First();

        /// <summary>
        /// Gets full spec-quantified test name
        /// </summary>
        public string GetFullTestName(NodeTest test) => $"{SpecName}_{test.MethodName}_node{test.Node}[{test.Role}]";
    }
}