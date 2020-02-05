namespace Akka.MultiNode.TestAdapter.SampleTests
{
    /// <summary>
    /// SampleTestsMetadata
    /// </summary>
    public static class SampleTestsMetadata
    {
        /// <summary>
        /// Sample tests assembly path
        /// </summary>
        public static string AssemblyPath => typeof(SampleMultiNodeSpec).Assembly.Location;
    }
}