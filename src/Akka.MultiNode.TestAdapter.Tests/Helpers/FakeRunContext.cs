using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Akka.MultiNode.TestAdapter.Tests.Helpers
{
    /// <summary>
    /// Fake to pass to <see cref="ITestExecutor.RunTests"/> method
    /// </summary>
    class FakeRunContext : IRunContext
    {
        public ITestCaseFilterExpression GetTestCaseFilter(IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider)
        {
            return null;
        }
            
        public IRunSettings RunSettings { get; }
        public bool KeepAlive { get; }
        public bool InIsolation { get; }
        public bool IsDataCollectionEnabled { get; }
        public bool IsBeingDebugged { get; }
        public string TestRunDirectory { get; }
        public string SolutionDirectory { get; }
    }
}