﻿//-----------------------------------------------------------------------
// <copyright file="EnumerableExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;

namespace Akka.MultiNode.TestAdapter.Internal.Persistence
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, T item)
        {
            foreach (var cur in source)
            {
                yield return cur;
            }
            yield return item;
        }
    }
}
