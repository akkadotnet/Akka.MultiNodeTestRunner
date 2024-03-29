﻿// -----------------------------------------------------------------------
//  <copyright file="Identifier.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.MultiNode.TestAdapter.Internal.TrxReporter.Models
{
    public struct Identifier
    {
        public Identifier(Guid value)
        {
            Value = value;
        }

        public static readonly Identifier Empty = Create(Guid.Empty);

        public Guid Value { get; }

        public override string ToString() => Value.ToString("D");

        public static Identifier Create() => new Identifier(Guid.NewGuid());

        public static Identifier Create(Guid value) => new Identifier(value);
    }
}