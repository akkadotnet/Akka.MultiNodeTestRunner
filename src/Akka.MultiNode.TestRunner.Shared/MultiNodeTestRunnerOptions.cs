using System.Collections.Generic;
using System.IO;

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
        
        /// <summary>
        /// TeamCity formatting on/off
        /// </summary>
        public bool TeamCityFormattingOn { get; set; }
        /// <summary>
        /// File output directory
        /// </summary>
        public string OutputDirectory { get; set; } = "TestResults";
        /// <summary>
        /// Subdirectory to store failed specs logs
        /// </summary>
        public string FailedSpecsDirectory { get; set; } = "FAILED_SPECS_LOGS";
        /// <summary>
        /// MNTR controller listener address
        /// </summary>
        public string ListenAddress { get; set; } = "127.0.0.1";
        /// <summary>
        /// MNTR controller listener port
        /// </summary>
        public int ListenPort { get; set; } = 6577;
        /// <summary>
        /// List of spec names to be executed. Other specs are skipped 
        /// </summary>
        public List<string> SpecNames { get; set; }
        /// <summary>
        /// Current platform. "net" or "netcore"
        /// </summary>
        public string Platform { get; set; } = "net";
        /// <summary>
        /// Reporter. "trx"/"teamcity"/"console"
        /// </summary>
        public string Reporter { get; set; } = "console";
        /// <summary>
        /// If set, performs output directory cleanup before running tests
        /// </summary>
        public bool ClearOutputDirectory { get; set; }

        /// <summary>
        /// Sets <see cref="SpecNames"/>
        /// </summary>
        public MultiNodeTestRunnerOptions WithSpecNames(List<string> specNames)
        {
            SpecNames = specNames;
            return this;
        }
    }
}