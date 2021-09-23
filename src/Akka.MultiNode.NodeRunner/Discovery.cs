//-----------------------------------------------------------------------
// <copyright file="Discovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Akka.MultiNode.NodeRunner
{
    [Serializable]
    public class Discovery : IMessageSink, IDisposable
    {
        private readonly string _assemblyName;
        private readonly string _className;
        public List<ITestCase> TestCases { get; private set; }
        public List<ErrorMessage> Errors { get; } = new List<ErrorMessage>();
        public bool WasSuccessful => Errors.Count == 0;
        public ManualResetEvent Finished { get; }

        public Discovery(string assemblyName, string className)
        {
            _assemblyName = assemblyName;
            _className = className;
            TestCases = new List<ITestCase>();
            Finished = new ManualResetEvent(false);
        }

        public bool OnMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestCaseDiscoveryMessage discovery:
                    var name = discovery.TestAssembly.Assembly.AssemblyPath.Split('\\').Last();
                    if (!name.Equals(_assemblyName, StringComparison.OrdinalIgnoreCase))
                        break;

                    var testName = discovery.TestClass.Class.Name;
                    if (testName.Equals(_className, StringComparison.OrdinalIgnoreCase))
                    {
                        TestCases.Add(discovery.TestCase);
                    }
                    break;
                case IDiscoveryCompleteMessage discoveryComplete:
                    Finished.Set();
                    break;
                case ErrorMessage err:
                    Errors.Add(err);
                    break;
            }

            return true;
        }

        public void Dispose()
        {
            Finished.Dispose();
        }
    }

}
