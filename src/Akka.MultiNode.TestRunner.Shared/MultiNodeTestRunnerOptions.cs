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
        public static readonly MultiNodeTestRunnerOptions Default = new MultiNodeTestRunnerOptions(
            null, null, null, 0, null, null, null, false, false, false);

        public MultiNodeTestRunnerOptions(
            string outputDirectory,
            string failedSpecsDirectory, 
            string listenAddress,
            int listenPort,
            List<string> specNames,
            string platform,
            string reporter,
            bool clearOutputDirectory,
            bool teamCityFormattingOn,
            bool designMode)
        {
            OutputDirectory = outputDirectory ?? "TestResults";
            FailedSpecsDirectory = failedSpecsDirectory ?? "FAILED_SPECS_LOGS";
            ListenAddress = listenAddress ?? "127.0.0.1";
            ListenPort = listenPort;
            SpecNames = specNames;
            Platform = platform;
            Reporter = reporter ?? "console";
            ClearOutputDirectory = clearOutputDirectory;
            TeamCityFormattingOn = teamCityFormattingOn;
            DesignMode = designMode;
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
        
        public string Platform { get; }
        
        public bool DesignMode { get; }

        public MultiNodeTestRunnerOptions WithOutputDirectory(string outputDirectory)
            => Copy(outputDirectory: outputDirectory);

        public MultiNodeTestRunnerOptions WithFailedSpecDirectory(string failedSpecDirectory)
            => Copy(failedSpecsDirectory: failedSpecDirectory);
        
        public MultiNodeTestRunnerOptions WithListenAddress(string listenAddress)
            => Copy(listenAddress: listenAddress);
        
        public MultiNodeTestRunnerOptions WithListenPort(int listenPort)
            => Copy(listenPort: listenPort);

        public MultiNodeTestRunnerOptions WithSpecNames(List<string> specNames)
            => Copy(specNames: specNames);

        public MultiNodeTestRunnerOptions WithPlatform(string platform)
            => Copy(platform: platform);

        public MultiNodeTestRunnerOptions WithReporter(string reporter)
            => Copy(reporter: reporter);

        public MultiNodeTestRunnerOptions WithClearOutputDirectory(bool clearOutputDirectory)
            => Copy(clearOutputDirectory: clearOutputDirectory);

        public MultiNodeTestRunnerOptions WithTeamCityFormatting(bool teamCityFormattingOn)
            => Copy(teamCityFormattingOn: teamCityFormattingOn);
        
        public MultiNodeTestRunnerOptions WithDesignMode(bool designMode)
            => Copy(designMode: designMode);

        private MultiNodeTestRunnerOptions Copy(
            string outputDirectory = null,
            string failedSpecsDirectory = null,
            string listenAddress = null,
            int? listenPort = null,
            List<string> specNames = null,
            string platform = null,
            string reporter = null,
            bool? clearOutputDirectory = null,
            bool? teamCityFormattingOn = null,
            bool? designMode = null)
            => new MultiNodeTestRunnerOptions(
                outputDirectory: outputDirectory ?? OutputDirectory,
                failedSpecsDirectory: failedSpecsDirectory ?? FailedSpecsDirectory,
                listenAddress: listenAddress ?? ListenAddress,
                listenPort: listenPort ?? ListenPort,
                specNames: specNames ?? SpecNames,
                platform: platform ?? Platform,
                reporter: reporter ?? Reporter,
                clearOutputDirectory: clearOutputDirectory ?? ClearOutputDirectory,
                teamCityFormattingOn: teamCityFormattingOn ?? TeamCityFormattingOn,
                designMode: designMode ?? DesignMode);
    }
}