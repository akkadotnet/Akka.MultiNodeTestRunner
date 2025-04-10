# Akka.MultiNode.TestAdapter

Visual Studio 2019 Test Explorer, JetBrains Rider, and .NET CLI Test runner for the Akka.NET MultiNode tests

## Documentation
`Akka.MultiNode.TestAdapter` is a standalone test adapter for Akka.NET multi node testkit; it is based on the popular Xunit test framework to allow multinode tests to run directly inside popular C# IDE such as Microsoft Visual Studio and JetBrains Rider and run them using the `dotnet test` .NET CLI command.

To use the test adapter in your multinode spec projects, You will need to add these nuget packages:
  - [Akka.MultiNode.TestAdapter](https://www.nuget.org/packages/Akka.MultiNode.TestAdapter)
  - [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/)

Documentation regarding the multinode specs themselves can be read in the Akka.NET documentation pages:
  - [Using the MultiNode TestKit](https://getakka.net/articles/networking/multi-node-test-kit.html)
  - [Multi-Node Testing Distributed Akka.NET Applications](https://getakka.net/articles/testing/multi-node-testing.html)

### Json Settings

This test adapter follows the Xunit convention on loading test configuration via a json file. It will
check the directory where the test assembly is located for a `[Assembly Name].xunit.multinode.runner.json`
or a `xunit.multinode.runner.json` file.

```json
{
  "outputDirectory": "TestResults",
  "failedSpecsDirectory": "FAILED_SPECS_LOGS",
  "listenAddress": "127.0.0.1",
  "listenPort":  0,
  "clearOutputDirectory": false
}
```

* **outputDirectory**: Determines output directory for all log output files. 
* **failedSpecsDirectory**: Determines output directory for aggregated failed test logs.
* **listenAddress**: Determines the address that this multi-node test runner will use to listen for log messages from individual spec.
* **listenPort**: Determines the port number that this multi-node test runner will use to listen for log messages from individual spec.
* **ClearOutputDirectory**: Clear the output directory before running the test session. If set to false, all test logs are appended to the out file.
