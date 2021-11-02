// -----------------------------------------------------------------------
// <copyright file="XUnitSinkAdapter.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.MultiNode.TestAdapter.Internal.Reporting;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace Akka.MultiNode.TestAdapter.Internal.Sinks
{
    public class FrameworkHandleMessageSinkActor: TestCoordinatorEnabledMessageSink
    {
        private readonly IFrameworkHandle _frameworkHandle;
        private readonly ConsoleOutput _consoleOutput;
        
        public FrameworkHandleMessageSinkActor(
            bool useTestCoordinator,
            IFrameworkHandle frameworkHandle)
            : base(useTestCoordinator)
        {
            _frameworkHandle = frameworkHandle;
            _consoleOutput = ConsoleOutput.Instance;
        }

        #region Message handling

        protected override void AdditionalReceives()
        {
            Receive<FactData>(data => ReceiveFactData(data));
        }

        protected override void ReceiveFactData(FactData data)
        {
            PrintSpecRunResults(data);
        }

        private void PrintSpecRunResults(FactData data)
        {
            WriteSpecMessage($"Results for {data.FactName}");
            WriteSpecMessage($"Start time: {new DateTime(data.StartTime, DateTimeKind.Utc)}");
            foreach (var node in data.NodeFacts)
            {
                WriteSpecMessage(
                    $" --> Node {node.Value.NodeIndex}:{node.Value.NodeRole} : {(node.Value.Passed.GetValueOrDefault(false) ? "PASS" : "FAIL")} [{node.Value.Elapsed} elapsed]");
            }
            WriteSpecMessage(
                $"End time: {new DateTime(data.EndTime.GetValueOrDefault(DateTime.UtcNow.Ticks), DateTimeKind.Utc)}");
            WriteSpecMessage(
                $"FINAL RESULT: {(data.Passed.GetValueOrDefault(false) ? "PASS" : "FAIL")} after {data.Elapsed}.");

            //If we had a failure
            if (data.Passed.GetValueOrDefault(false) == false)
            {
                WriteSpecMessage("Failure messages by Node");
                foreach (var node in data.NodeFacts)
                {
                    if (node.Value.Passed.GetValueOrDefault(false) == false)
                    {
                        WriteSpecMessage($"<----------- BEGIN NODE {node.Key}:{node.Value.NodeRole} ----------->");
                        foreach (var resultMessage in node.Value.ResultMessages)
                        {
                            WriteSpecMessage($" --> {resultMessage.Message}");
                        }
                        if (node.Value.ResultMessages == null || node.Value.ResultMessages.Count == 0)
                            WriteSpecMessage("[received no messages - SILENT FAILURE].");
                        WriteSpecMessage($"<----------- END NODE {node.Key}:{node.Value.NodeRole} ----------->");
                    }
                }
            }
        }

        protected override void HandleNodeSpecFail(NodeCompletedSpecWithFail nodeFail)
        {
            WriteSpecFail(nodeFail.NodeIndex, nodeFail.NodeRole, nodeFail.Message);

            base.HandleNodeSpecFail(nodeFail);
        }

        protected override void HandleTestRunEnd(EndTestRun endTestRun)
        {
            WriteSpecMessage("Test run complete.");

            base.HandleTestRunEnd(endTestRun);
        }

        protected override void HandleTestRunTree(TestRunTree tree)
        {
            var passedSpecs = tree.Specs.Count(x => x.Passed.GetValueOrDefault(false));
            WriteSpecMessage(
                $"Test run completed in [{tree.Elapsed}] with {passedSpecs}/{tree.Specs.Count()} specs passed.");
            foreach (var factData in tree.Specs)
            {
                PrintSpecRunResults(factData);
            }
        }

        protected override void HandleNewSpec(BeginNewSpec newSpec)
        {
            WriteSpecMessage($"Beginning spec {newSpec.ClassName}.{newSpec.MethodName} on {newSpec.Nodes.Count} nodes");

            base.HandleNewSpec(newSpec);
        }

        protected override void HandleEndSpec(EndSpec endSpec)
        {
            WriteSpecMessage("Spec completed.");

            base.HandleEndSpec(endSpec);
        }

        protected override void HandleNodeMessageFragment(LogMessageFragmentForNode logMessage)
        {
            WriteNodeMessage(logMessage);

            base.HandleNodeMessageFragment(logMessage);
        }

        protected override void HandleRunnerMessage(LogMessageForTestRunner node)
        {
            WriteRunnerMessage(node);

            base.HandleRunnerMessage(node);
        }

        protected override void HandleNodeSpecPass(NodeCompletedSpecWithSuccess nodeSuccess)
        {
            WriteSpecPass(nodeSuccess.NodeIndex, nodeSuccess.NodeRole, nodeSuccess.Message);

            base.HandleNodeSpecPass(nodeSuccess);
        }

        #endregion

        #region FrameworkHandle output methods

        /// <summary>
        /// Used to print a spec status message (spec starting, finishing, failed, etc...)
        /// </summary>
        private void WriteSpecMessage(string message)
        {
            _frameworkHandle.SendMessage(
                TestMessageLevel.Informational, 
                $"[RUNNER][{DateTime.UtcNow.ToShortTimeString()}]: {message}");
        }

        private void WriteSpecPass(int nodeIndex, string nodeRole, string message)
        {
            _frameworkHandle.SendMessage(
                TestMessageLevel.Informational, 
                $"[NODE{nodeIndex}:{nodeRole}][{DateTime.UtcNow.ToShortTimeString()}]: SPEC PASSED: {message}");
        }

        private void WriteSpecFail(int nodeIndex, string nodeRole, string message)
        {
            _frameworkHandle.SendMessage(
                TestMessageLevel.Error, 
                $"[NODE{nodeIndex}:{nodeRole}][{DateTime.UtcNow.ToShortTimeString()}]: SPEC FAILED: {message}");
        }

        private void WriteRunnerMessage(LogMessageForTestRunner nodeMessage)
        {
            switch (nodeMessage.Level)
            {
                case LogLevel.WarningLevel:
                    _frameworkHandle.SendMessage(TestMessageLevel.Warning, nodeMessage.ToString());
                    break;
                case LogLevel.ErrorLevel:
                    _frameworkHandle.SendMessage(TestMessageLevel.Error, nodeMessage.ToString());
                    break;
                case LogLevel.DebugLevel:
                case LogLevel.InfoLevel:
                default:
                    _frameworkHandle.SendMessage(TestMessageLevel.Informational, nodeMessage.ToString());
                    break;
            }
        }

        private void WriteNodeMessage(LogMessageFragmentForNode nodeMessage)
        {
            _frameworkHandle.SendMessage(TestMessageLevel.Informational, nodeMessage.ToString());
        }

        #endregion
    }
    
    /// <summary>
    /// <see cref="IMessageSink"/> implementation that writes directly to the console.
    /// </summary>
    public class FrameworkHandleMessageSink : MessageSink
    {
        private readonly IFrameworkHandle _frameworkHandle;
        
        public FrameworkHandleMessageSink(IFrameworkHandle frameworkHandle)
            : base(Props.Create(() => new FrameworkHandleMessageSinkActor(true, frameworkHandle)))
        {
            _frameworkHandle = frameworkHandle;
        }

        protected override void HandleUnknownMessageType(string message)
        {
            _frameworkHandle.SendMessage(TestMessageLevel.Warning, $"Unknown message: {message}");
        }
    }
}