﻿using System;
using Xunit;

namespace Akka.MultiNodeTestRunner.VisualStudio.Sinks
{
    /// <summary>
    /// Used to discover tests before running when VS says "run everything in the assembly".
    /// </summary>
    internal class VsExecutionDiscoverySink : TestDiscoverySink, IVsDiscoverySink
    {
        public VsExecutionDiscoverySink(Func<bool> cancelThunk)
            : base(cancelThunk) { }

        public int Finish()
        {
            Finished.WaitOne();
            return TestCases.Count;
        }
    }
}
