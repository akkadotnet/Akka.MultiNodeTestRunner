using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable
namespace Akka.MultiNode.TestAdapter.Configuration
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

        [Obsolete(message:"Only used for deserialization")]
        public MultiNodeTestRunnerOptions()
        { }
        
        public MultiNodeTestRunnerOptions(
            string? outputDirectory = null,
            string? failedSpecsDirectory = null, 
            string? listenAddress = null,
            int? listenPort = null,
            string? platform = null,
            string? reporter = null,
            bool? clearOutputDirectory = null,
            bool? teamCityFormattingOn = null)
        {
            OutputDirectory = outputDirectory ?? "TestResults";
            FailedSpecsDirectory = failedSpecsDirectory ?? "FAILED_SPECS_LOGS";
            ListenAddress = listenAddress ?? "127.0.0.1";
            ListenPort = listenPort ?? 0;
            Platform = platform ?? string.Empty;
            Reporter = reporter ?? "console";
            ClearOutputDirectory = clearOutputDirectory ?? false;
            TeamCityFormattingOn = teamCityFormattingOn ?? false;
        }

        /// <summary>
        /// File output directory
        /// </summary>
        [JsonProperty("outputDirectory")]
        public string OutputDirectory { get; } = "TestResult";
        /// <summary>
        /// Subdirectory to store failed specs logs
        /// </summary>
        [JsonProperty("failedSpecsDirectory")] 
        public string FailedSpecsDirectory { get; } = "FAILED_SPECS_LOGS";
        /// <summary>
        /// MNTR controller listener address
        /// </summary>
        [JsonProperty("listenAddress")] 
        public string ListenAddress { get; } = "127.0.0.1";

        /// <summary>
        /// MNTR controller listener port. Set 0 to use random available port
        /// </summary>
        [JsonProperty("listenPort")]
        public int ListenPort { get; } = 0;

        /// <summary>
        /// Reporter. "trx"/"teamcity"/"console"
        /// </summary>
        [JsonProperty("reporter")]
        public string Reporter { get; } = "console";

        /// <summary>
        /// If set, performs output directory cleanup before running tests
        /// </summary>
        [JsonProperty("clearOutputDirectory")]
        public bool ClearOutputDirectory { get; } = false;

        /// <summary>
        /// TeamCity formatting on/off
        /// </summary>
        [JsonProperty("teamCityFormatting")]
        public bool TeamCityFormattingOn { get; } = false;
        
        public string Platform { get; }
        
        public MultiNodeTestRunnerOptions WithOutputDirectory(string outputDirectory)
            => Copy(outputDirectory: outputDirectory);

        public MultiNodeTestRunnerOptions WithFailedSpecDirectory(string failedSpecDirectory)
            => Copy(failedSpecsDirectory: failedSpecDirectory);
        
        public MultiNodeTestRunnerOptions WithListenAddress(string listenAddress)
            => Copy(listenAddress: listenAddress);
        
        public MultiNodeTestRunnerOptions WithListenPort(int listenPort)
            => Copy(listenPort: listenPort);

        public MultiNodeTestRunnerOptions WithPlatform(string platform)
            => Copy(platform: platform);

        public MultiNodeTestRunnerOptions WithReporter(string reporter)
            => Copy(reporter: reporter);

        public MultiNodeTestRunnerOptions WithClearOutputDirectory(bool clearOutputDirectory)
            => Copy(clearOutputDirectory: clearOutputDirectory);

        public MultiNodeTestRunnerOptions WithTeamCityFormatting(bool teamCityFormattingOn)
            => Copy(teamCityFormattingOn: teamCityFormattingOn);
        
        private MultiNodeTestRunnerOptions Copy(
            string? outputDirectory = null,
            string? failedSpecsDirectory = null,
            string? listenAddress = null,
            int? listenPort = null,
            string? platform = null,
            string? reporter = null,
            bool? clearOutputDirectory = null,
            bool? teamCityFormattingOn = null)
            => new MultiNodeTestRunnerOptions(
                outputDirectory: outputDirectory ?? OutputDirectory,
                failedSpecsDirectory: failedSpecsDirectory ?? FailedSpecsDirectory,
                listenAddress: listenAddress ?? ListenAddress,
                listenPort: listenPort ?? ListenPort,
                platform: platform ?? Platform,
                reporter: reporter ?? Reporter,
                clearOutputDirectory: clearOutputDirectory ?? ClearOutputDirectory,
                teamCityFormattingOn: teamCityFormattingOn ?? TeamCityFormattingOn);
    }
}