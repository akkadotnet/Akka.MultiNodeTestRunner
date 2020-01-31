﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || NETCOREAPP

namespace Akka.MultiNodeTestRunner.VisualStudio.Utility.AssemblyResolution.Microsoft.Extensions.DependencyModel
{
    internal interface IEnvironment
    {
        string GetEnvironmentVariable(string name);
    }
}

#endif
