namespace Akka.MultiNode.TestRunner.Shared
{
    /// <summary>
    /// MultiNodeTestRunnerOptions
    /// </summary>
    public class MultiNodeTestRunnerOptions
    {
        /// <summary>
        /// TeamCity formatting on/off
        /// </summary>
        public bool TeamCityFormattingOn { get; set; }
        /// <summary>
        /// File output directory
        /// </summary>
        public string OutputDirectory { get; set; } = string.Empty;
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
        /// Spec name
        /// </summary>
        public string SpecName { get; set; } = string.Empty;
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
    }
}