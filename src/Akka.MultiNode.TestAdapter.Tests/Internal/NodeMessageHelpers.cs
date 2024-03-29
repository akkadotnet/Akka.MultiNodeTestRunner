﻿//-----------------------------------------------------------------------
// <copyright file="NodeMessageHelpers.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Event;
using Akka.MultiNode.TestAdapter.Internal;
using Akka.MultiNode.TestAdapter.Internal.Reporting;
using Akka.MultiNode.TestAdapter.Tests.Internal.Utils;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.MultiNode.TestAdapter.Tests.Internal
{
    /// <summary>
    /// Helper class for creating <see cref="MultiNodeMessage"/>
    /// </summary>
    public static class NodeMessageHelpers
    {
        internal const string DummyRoleFor = "Dummy_role_for_";
        internal static readonly Random Random = new Random();

        public static MultiNodeTestCase BuildNodeTests(IEnumerable<int> nodeIndicies)
            => new MockMultiNodeTestCase(nodeIndicies);

        /// <summary>
        /// Meta-function for generating a distribution of messages across multiple nodes
        /// </summary>
        private static SortedSet<MultiNodeMessage> GenerateMessageDistributionForNodes(IEnumerable<int> nodeIndices,
            int count, Func<int, int, SortedSet<MultiNodeMessage>> messageGenerator)
        {
            var nodes = nodeIndices.ToList();
            var messages = new SortedSet<MultiNodeMessage>();

            //special case for 1:1 distribution
            if (nodes.Count == count)
            {
                foreach (var node in nodes)
                {
                    messages.UnionWith(messageGenerator(node, node));
                }
                return messages;
            }

            // Key = nodeIndex, Value = # of allocated messages
            var messageDistribution = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                messageDistribution[node] = 0;
            }

            var remainingMessages = count;
            var nodeIterator = nodes.GetContinuousEnumerator();

            while (remainingMessages > 0)
            {
                nodeIterator.MoveNext();
                var node = nodeIterator.Current;
                var added = Random.Next(1, Math.Max(1, remainingMessages / 2));

                //Don't go over the message count
                if (added > remainingMessages)
                    added = remainingMessages;

                messageDistribution[node] += added;
                remainingMessages -= added;
            }

            //generate the assigned sequence for each node
            foreach (var node in messageDistribution)
                messages.UnionWith(messageGenerator(node.Key, node.Value));

            return messages;
        }

        public static SortedSet<MultiNodeMessage> GenerateMessageSequence(IEnumerable<int> nodeIndices, int count)
        {
            return GenerateMessageDistributionForNodes(nodeIndices, count, GenerateMessageSequence);
        }

        public static SortedSet<MultiNodeMessage> GenerateMessageSequence(int nodeIndex, int count)
        {
            var messages = new SortedSet<MultiNodeMessage>();
            var startTime = DateTime.UtcNow;
            foreach (var i in Enumerable.Range(0, count))
            {
                messages.Add(new MultiNodeLogMessage(
                    GetTimeStamp(startTime, startTime + TimeSpan.FromSeconds(20)), 
                    String.Format("Message {0}", i), nodeIndex, DummyRoleFor + nodeIndex,
                    "/foo", LogLevel.InfoLevel));
            }
            return messages;
        }

        public static SortedSet<MultiNodeMessage> GenerateMessageFragmentSequence(IEnumerable<int> nodeIndices, int count)
        {
            return GenerateMessageDistributionForNodes(nodeIndices, count, GenerateMessageFragmentSequence);
        }

        public static SortedSet<MultiNodeMessage> GenerateMessageFragmentSequence(int nodeIndex, int count)
        {
            var messages = new SortedSet<MultiNodeMessage>();
            var startTime = DateTime.UtcNow;
            foreach (var i in Enumerable.Range(0, count))
            {
                messages.Add(new MultiNodeLogMessageFragment(
                    GetTimeStamp(startTime, startTime + TimeSpan.FromSeconds(20)),
                    String.Format("Message {0}", i), nodeIndex, DummyRoleFor + nodeIndex));
            }
            return messages;
        }

        public static SortedSet<MultiNodeMessage> GenerateTestRunnerMessageSequence(int count)
        {
            var messages = new SortedSet<MultiNodeMessage>();
            var startTime = DateTime.UtcNow;
            foreach (var i in Enumerable.Range(0, count))
            {
                messages.Add(new MultiNodeTestRunnerMessage(GetTimeStamp(startTime, startTime + TimeSpan.FromSeconds(20)), String.Format("Message {0}", i),
                    "/foo", LogLevel.InfoLevel));
            }
            return messages;
        }

        public static SortedSet<MultiNodeMessage> GenerateResultMessage(IEnumerable<int> nodeIndices, bool pass)
        {
            var messages = new SortedSet<MultiNodeMessage>();
            var enumerable = nodeIndices as int[] ?? nodeIndices.ToArray();
            return GenerateMessageDistributionForNodes(enumerable, enumerable.Count(),
                (i, i1) => GenerateResultMessage(i, pass));
        }

        public static SortedSet<MultiNodeMessage> GenerateResultMessage(int nodeIndex, bool pass)
        {
            var messages = new SortedSet<MultiNodeMessage>();
            var startTime = DateTime.UtcNow;
            messages.Add(
                new MultiNodeResultMessage(
                    GetTimeStamp(startTime, startTime + TimeSpan.FromSeconds(30)),
                    String.Format("Test passed? {0}", pass), nodeIndex, DummyRoleFor + nodeIndex, pass));
            return messages;
        }

        #region Faker functions
        private static DateTime GetDateTime(DateTime from, DateTime to)
        {
            TimeSpan timeSpan = new TimeSpan(to.Ticks - from.Ticks);
            return from + new TimeSpan((long)(timeSpan.Ticks * Random.NextDouble()));
        }

        private static DateTime GetDateTime()
        {
            return GetDateTime(DateTime.Now.AddYears(-70), DateTime.Now.AddYears(70));
        }

        private static long GetTimeStamp(DateTime when)
        {
            return (long)(when - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToUniversalTime()).TotalSeconds;
        }

        private static long GetTimeStamp(DateTime from, DateTime to)
        {
            return GetTimeStamp(GetDateTime(from, to));
        }

        private static long GetTimeStamp()
        {
            return GetTimeStamp(GetDateTime());
        }

        private static string AlphaNumericString(int minLength = 10, int maxLength = 40)
        {
            return new string(
                Enumerable.Repeat<string>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789",
                        Random.Next(minLength, maxLength))
                    .Select(x => x[Random.Next(x.Length)])
                    .ToArray());
        }

        private static int Range(int min = 0, int max = 2147483647)
        {
            return Random.Next(min, max);
        }
        #endregion
        
        internal class MockMultiNodeTestCase : MultiNodeTestCase
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public MockMultiNodeTestCase() { }
            
            public MockMultiNodeTestCase(IEnumerable<int> nodeIndices)
            {
                _indices = nodeIndices;
                MethodName = AlphaNumericString();
                TypeName = AlphaNumericString();
                AssemblyPath = AlphaNumericString();
            }

            private readonly IEnumerable<int> _indices;
            
            public override string MethodName { get; }
            public override string TypeName { get; }
            public override string AssemblyPath { get; protected set; }

            protected override void Initialize()
            {
                InternalNodes = LoadDetails();
            }

            protected override List<NodeTest> LoadDetails()
            {
                return _indices.Select(i => new NodeTest(this, i, DummyRoleFor + i)).ToList();
            }

            public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
                ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
            {
                return Task.FromResult(new RunSummary());
            }
        }
        
    }
}

