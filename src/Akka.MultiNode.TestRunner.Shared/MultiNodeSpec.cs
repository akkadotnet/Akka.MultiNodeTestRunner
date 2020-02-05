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
    }
}