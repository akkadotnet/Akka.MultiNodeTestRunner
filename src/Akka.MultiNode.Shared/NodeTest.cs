//-----------------------------------------------------------------------
// <copyright file="NodeTest.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Akka.MultiNode.Shared
{
    public class NodeTest
    {
        public int Node { get; set; }
        public string Role { get; set; }
        public MultiNodeTest Test { get; set; }

        public string Name => $"{Test.TestName}_node{Node}[{Role}]";
    }
}

