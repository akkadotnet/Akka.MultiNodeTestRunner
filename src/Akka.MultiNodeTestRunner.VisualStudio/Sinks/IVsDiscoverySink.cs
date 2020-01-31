using System;
using Xunit;

namespace Akka.MultiNodeTestRunner.VisualStudio.Sinks
{
    internal interface IVsDiscoverySink : IMessageSinkWithTypes, IDisposable
    {
        int Finish();
    }
}
