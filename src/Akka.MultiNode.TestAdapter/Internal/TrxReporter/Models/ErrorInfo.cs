﻿// -----------------------------------------------------------------------
//  <copyright file="ErrorInfo.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Xml.Linq;
using static Akka.MultiNode.TestAdapter.Internal.TrxReporter.Models.XmlHelper;

namespace Akka.MultiNode.TestAdapter.Internal.TrxReporter.Models
{
    public class ErrorInfo : ITestEntity
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }

        public XElement Serialize()
        {
            return Elem("ErrorInfo",
                Elem("Message", Text(Message ?? "")),
                Elem("StackTrace", Text(StackTrace ?? ""))
            );
        }
    }
}