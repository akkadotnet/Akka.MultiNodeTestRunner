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
        /// MNTR controller listener port. Set 0 to use random available port
        /// </summary>
        public int ListenPort { get; set; }

        /// <summary>
        /// Reporter. "trx"/"teamcity"/"console"
        /// </summary>
        public string Reporter { get; set; } = "console";

        /// <summary>
        /// If set, performs output directory cleanup before running tests
        /// </summary>
        public bool ClearOutputDirectory { get; set; }

        /// <summary>
        /// TeamCity formatting on/off
        /// </summary>
        public bool TeamCityFormattingOn { get; set; }
        
        public string Platform { get; set; }
    }
}