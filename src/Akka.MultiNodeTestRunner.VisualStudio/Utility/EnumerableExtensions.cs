using System;
using System.Collections.Generic;

namespace Akka.MultiNodeTestRunner.VisualStudio.Utility
{
    internal static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> This, Action<T> action)
        {
            foreach (var item in This)
                action(item);
        }
    }
}