#if NETCOREAPP

using System;
using System.IO;

namespace Akka.MultiNodeTestRunner.VisualStudio.Utility.AssemblyResolution.Microsoft.Extensions.DependencyModel
{
    internal interface IDependencyContextReader: IDisposable
    {
        DependencyContext Read(Stream stream);
    }
}

#endif
