using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit;

namespace Akka.MultiNodeTestRunner.VisualStudio.Utility
{
    public class AssemblyRunInfo
    {
        public string AssemblyFileName;
        public TestAssemblyConfiguration Configuration;
        public IList<TestCase> TestCases;
    }
}
