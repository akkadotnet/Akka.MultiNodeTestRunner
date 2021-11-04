// -----------------------------------------------------------------------
// <copyright file="TestFailedException.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.MultiNode.TestAdapter.Internal
{
    internal class TestFailedException : Exception
    {
        private readonly string _stackTrace;

        public TestFailedException(string type, string message, string stacktrace):base($"Original exception: [{type}: {message}]")
        {
            _stackTrace = stacktrace;
        }

        public TestFailedException(string type, string message, string stacktrace, Exception innerException)
            : base($"Original exception: [{type}: {message}]", innerException)
        {
            _stackTrace = stacktrace;
        }
        
        public override string StackTrace => _stackTrace ?? base.StackTrace;
    }
}