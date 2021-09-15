//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Akka.MultiNode.Shared;
using Akka.MultiNode.Shared.Persistence;
using Akka.MultiNode.Shared.Sinks;
using Akka.MultiNode.Shared.TrxReporter;
using Akka.MultiNode.TestRunner.Shared;
using Akka.Remote.TestKit;
using Xunit;

#if CORECLR
using System.Runtime.Loader;
#endif

namespace Akka.MultiNode.TestRunner
{
    /// <summary>
    /// Entry point for the MultiNodeTestRunner
    /// </summary>
    class Program
    {
        /// <summary>
        /// MultiNodeTestRunner takes the following <see cref="args"/>:
        /// 
        /// C:\> Akka.MultiNode.TestRunner.exe [assembly name] [-Dmultinode.enable-filesink=on] [-Dmultinode.output-directory={dir path}] [-Dmultinode.spec={spec name}]
        /// 
        /// <list type="number">
        /// <listheader>
        ///     <term>Argument</term>
        ///     <description>The name and possible value of a given Akka.MultiNode.TestRunner.exe argument.</description>
        /// </listheader>
        /// <item>
        ///     <term>AssemblyName</term>
        ///     <description>
        ///         The full path or name of an assembly containing as least one MultiNodeSpec in the current working directory.
        /// 
        ///         i.e. "Akka.Cluster.Tests.MultiNode.dll"
        ///              "C:\akka.net\src\Akka.Cluster.Tests\bin\Debug\Akka.Cluster.Tests.MultiNode.dll"
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.enable-filesink</term>
        ///     <description>Having this flag set means that the contents of this test run will be saved in the
        ///                 current working directory as a .JSON file.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.multinode.output-directory</term>
        ///     <description>Setting this flag means that any persistent multi-node test runner output files
        ///                  will be written to this directory instead of the default, which is the same folder
        ///                  as the test binary.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.listen-address={ip}</term>
        ///     <description>
        ///             Determines the address that this multi-node test runner will use to listen for log messages from
        ///             individual NodeTestRunner.exe processes.
        /// 
        ///             Defaults to 127.0.0.1
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.listen-port={port}</term>
        ///     <description>
        ///             Determines the port number that this multi-node test runner will use to listen for log messages from
        ///             individual NodeTestRunner.exe processes.
        /// 
        ///             Defaults to 6577
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>-Dmultinode.spec={spec name}</term>
        ///     <description>
        ///             Setting this flag means that only tests which contains the spec name will be executed
        ///             otherwise all tests will be executed
        ///     </description>
        /// </item>
        /// </list>
        /// </summary>
        static void Main(string[] args)
        {
            var assemblyPath = Path.GetFullPath(args[0].Trim('"')); // unquote the string first
            var outputDirectory = CommandLine.GetPropertyOrDefault("multinode.output-directory", string.Empty);
            var failedSpecsDirectory = CommandLine.GetPropertyOrDefault("multinode.failed-specs-directory", "FAILED_SPECS_LOGS");
            var listenAddress = CommandLine.GetPropertyOrDefault("multinode.listen-address", "127.0.0.1");
            var listenPort = CommandLine.GetInt32OrDefault("multinode.listen-port", 6577);
            var specName = CommandLine.GetPropertyOrDefault("multinode.spec", "");
            var platform = CommandLine.GetPropertyOrDefault("multinode.platform", "net");
            var reporter = CommandLine.GetPropertyOrDefault("multinode.reporter", "console");
            var clearOutputDirectory = CommandLine.GetInt32OrDefault("multinode.clear-output", 0) > 0;
            var teamCityFormattingStr = CommandLine.GetPropertyOrDefault("multinode.teamcity", "false");
            if (!bool.TryParse(teamCityFormattingStr, out var teamCityFormattingOn))
                throw new ArgumentException("Invalid argument provided for -Dteamcity");

            int retCode;
            using (var runner = new MultiNodeTestRunner())
            {
                var results = runner.ExecuteAssembly(assemblyPath, new MultiNodeTestRunnerOptions(
                    outputDirectory: outputDirectory,
                    failedSpecsDirectory: failedSpecsDirectory,
                    teamCityFormattingOn: teamCityFormattingOn,
                    listenAddress: listenAddress,
                    listenPort: listenPort,
                    specNames: !string.IsNullOrEmpty(specName) ? new List<string>() { specName } : null,
                    platform: platform,
                    reporter: reporter,
                    clearOutputDirectory: clearOutputDirectory
                ));
                
                // Return the proper exit code
                retCode = results.Any(r => r.Status == MultiNodeTestResult.TestStatus.Failed) ? 1 : 0;
            }

            if (Debugger.IsAttached)
                Console.ReadLine(); // block when debugging

            Environment.Exit(retCode);
        }
    }
}