﻿//-----------------------------------------------------------------------
// <copyright file="CompilerErrorCollection.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

#if CORECLR
namespace Akka.MultiNode.Shared
{
    public class CompilerErrorCollection : List<CompilerError>
    {
    }

    public class CompilerError
    {
        public string ErrorText { get; set; }
        public bool IsWarning { get; set; }
    }
}
#endif
