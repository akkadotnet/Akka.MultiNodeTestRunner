using System.Collections.Generic;
using System.IO;
using Akka.Util;

namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// MultiNodeTestRunnerOptions
    /// </summary>
    public class MultiNodeTestRunnerOptions
    {
        /// <summary>
        /// Default options
        /// </summary>
        public static readonly MultiNodeTestRunnerOptions Default = new MultiNodeTestRunnerOptions();

        public MultiNodeTestRunnerOptions(string outputDirectory = null, string failedSpecsDirectory = null, 
                                          string listenAddress = null, int listenPort = 0, List<string> specNames = null,
                                          string platform = null, string reporter = null, bool clearOutputDirectory = false,
                                          bool teamCityFormattingOn = false)
        {
            ListenPort = listenPort;
            SpecNames = specNames;
            ClearOutputDirectory = clearOutputDirectory;
            TeamCityFormattingOn = teamCityFormattingOn;
            Reporter = reporter ?? "console";
            Platform = platform ?? (RuntimeDetector.IsWindows ? "net" : "netcore");
            FailedSpecsDirectory = failedSpecsDirectory ?? "FAILED_SPECS_LOGS";
            ListenAddress = listenAddress ?? "127.0.0.1";
            OutputDirectory = outputDirectory ?? "TestResults";
        }
       
        /// <summary>
        /// File output directory
        /// </summary>
        public string OutputDirectory { get; }
        /// <summary>
        /// Subdirectory to store failed specs logs
        /// </summary>
        public string FailedSpecsDirectory { get; }
        /// <summary>
        /// MNTR controller listener address
        /// </summary>
        public string ListenAddress { get; }
        /// <summary>
        /// MNTR controller listener port. Set 0 to use random available port
        /// </summary>
        public int ListenPort { get; }
        /// <summary>
        /// List of spec names to be executed. Other specs are skipped 
        /// </summary>
        public List<string> SpecNames { get; }
        /// <summary>
        /// Current platform. "net" or "netcore"
        /// </summary>
        public string Platform { get; }
        /// <summary>
        /// Reporter. "trx"/"teamcity"/"console"
        /// </summary>
        public string Reporter { get; }
        /// <summary>
        /// If set, performs output directory cleanup before running tests
        /// </summary>
        public bool ClearOutputDirectory { get; }
        /// <summary>
        /// TeamCity formatting on/off
        /// </summary>
        public bool TeamCityFormattingOn { get; }
    }
}