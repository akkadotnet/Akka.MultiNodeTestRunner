//-----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Event;

namespace Akka.MultiNode.Shared.Sinks
{
    #region Message types

    /// <summary>
    /// Message type for signaling that a new spec is ready to be run
    /// </summary>
    public class BeginNewSpec
    {
        public BeginNewSpec(MultiNodeTest test)
        {
            Test = test;
        }
        
        public MultiNodeTest Test { get; }

        public string ClassName => Test.TypeName;

        public string MethodName => Test.MethodName;

        public IList<NodeTest> Nodes => Test.Nodes;
    }

    /// <summary>
    /// Message type for indicating that the current spec has ended.
    /// </summary>
    public class EndSpec
    {
        public EndSpec()
        { }

        public EndSpec(MultiNodeTest test, SpecLog log)
        {
            Test = test;
            Log = log;
        }
        
        public MultiNodeTest Test { get; }

        public string ClassName => Test?.TypeName;
        public string MethodName => Test?.MethodName;
        public SpecLog Log { get; }
    }

    /// <summary>
    /// Message type for signaling that a node has completed a spec successfully
    /// </summary>
    public class NodeCompletedSpecWithSuccess
    {
        public NodeCompletedSpecWithSuccess(int nodeIndex, string nodeRole, string message)
        {
            Message = message;
            NodeIndex = nodeIndex;
            NodeRole = nodeRole;
        }

        public int NodeIndex { get; private set; }

        public string NodeRole { get; private set; }

        public string Message { get; private set; }
    }

    /// <summary>
    /// Message type for signaling that a node has completed a spec unsuccessfully
    /// </summary>
    public class NodeCompletedSpecWithFail
    {
        public NodeCompletedSpecWithFail(int nodeIndex, string nodeRole, string message)
        {
            Message = message;
            NodeIndex = nodeIndex;
            NodeRole = nodeRole;
        }

        public int NodeIndex { get; private set; }

        public string NodeRole { get; private set; }

        public string Message { get; private set; }
    }

    /// <summary>
    /// Truncated message - cut off from it's parent due to line break in I/O redirection
    /// </summary>
    public class LogMessageFragmentForNode
    {
        public LogMessageFragmentForNode(int nodeIndex, string nodeRole, string message, DateTime when)
        {
            NodeIndex = nodeIndex;
            NodeRole = nodeRole;
            Message = message;
            When = when;
        }

        public int NodeIndex { get; private set; }
        public string NodeRole { get; private set; }

        public DateTime When { get; private set; }

        public string Message { get; private set; }

        public override string ToString()
        {
            return string.Format("[NODE{1}:{2}][{0}]: {3}", When, NodeIndex, NodeRole, Message);
        }
    }

    /// <summary>
    /// Message for an individual node participating in a spec
    /// </summary>
    public class LogMessageForTestRunner
    {
        public LogMessageForTestRunner(string message, LogLevel level, DateTime when, string logSource)
        {
            LogSource = logSource;
            When = when;
            Level = level;
            Message = message;
        }

        public DateTime When { get; private set; }

        public string Message { get; private set; }

        public string LogSource { get; private set; }

        public LogLevel Level { get; private set; }

        public override string ToString()
        {
            return string.Format("[RUNNER][{0}][{1}][{2}]: {3}", When,
                Level.ToString().Replace("Level", "").ToUpperInvariant(), LogSource,
                Message);
        }
    }


    /// <summary>
    /// Message used to signal the end of the test run.
    /// </summary>
    public class EndTestRun
    {
        
    }

    #endregion
}

