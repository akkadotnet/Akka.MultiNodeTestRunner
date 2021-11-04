//-----------------------------------------------------------------------
// <copyright file="MultiNodeFact.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Xunit;
using Xunit.Sdk;

namespace Akka.MultiNode.TestAdapter
{
    // TODO: Remember to remove this in Akka.Remote.Testkit
    [XunitTestCaseDiscoverer("Akka.MultiNode.TestAdapter.MultiNodeFactDiscoverer", "Akka.MultiNode.TestAdapter")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MultiNodeFactAttribute : FactAttribute
    {
    }
}

