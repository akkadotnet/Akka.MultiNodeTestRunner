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
using Akka.MultiNode.Shared;
using Akka.Remote.TestKit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.MultiNode.TestRunner.Shared
{
#if CORECLR
    public class Discovery : IMessageSink, IDisposable
#else
    public class Discovery : MarshalByRefObject, IMessageSink, IDisposable
#endif
    {
        // There can be multiple fact attributes in a single class, but our convention
        // limits them to 1 fact attribute per test class
        public List<MultiNodeTest> Tests { get; }
        public List<ErrorMessage> Errors { get; } = new List<ErrorMessage>();
        public bool WasSuccessful => Errors.Count == 0;

        private readonly string _assemblyPath;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Discovery"/> class.
        /// </summary>
        public Discovery(string assemblyPath)
        {
            _assemblyPath = assemblyPath;
            Tests = new List<MultiNodeTest>();
            Finished = new ManualResetEvent(false);
        }

        public ManualResetEvent Finished { get; }

        public virtual bool OnMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestCaseDiscoveryMessage discovery:
                    var testClass = discovery.TestClass.Class;
                    if (testClass.IsAbstract) 
                        break;
                    if (!discovery.TestMethod.Method.GetCustomAttributes(typeof(MultiNodeFactAttribute)).Any())
                        break;
                    
                    Tests.Add(new MultiNodeTest(discovery, _assemblyPath));
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

        /// <inheritdoc/>
        public void Dispose()
        {
            Finished.Dispose();
        }
    }
}
