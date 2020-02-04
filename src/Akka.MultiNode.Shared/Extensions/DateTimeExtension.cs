﻿//-----------------------------------------------------------------------
// <copyright file="DateTimeExtension.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;

#if CORECLR
namespace Akka.MultiNode.Shared.Extensions
{
    internal static class DateTimeExtension
    {
        public static string ToShortTimeString(this DateTime dateTime)
        {
            return dateTime.ToString("d");
        }
    }
}
#endif
