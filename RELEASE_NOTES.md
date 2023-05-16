#### 1.5.6 May 16 2023

* [Updated Akka.NET to 1.5.6](https://github.com/akkadotnet/akka.net/releases/tag/1.5.6)

#### 1.5.0 March 02 2023

* [Updated Akka.NET to 1.5.0](https://github.com/akkadotnet/akka.net/releases/tag/1.5.0)

#### 1.5.0-beta1 February 21 2023

* [Updated Akka.NET to 1.5.0-beta1](https://github.com/akkadotnet/akka.net/releases/tag/1.5.0)
* [Bump XUnit from 2.4.1 to 2.4.2](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/163)

#### 1.1.1 April 21 2022 ####

* [Updated Akka.NET to 1.4.37](https://github.com/akkadotnet/akka.net/releases/tag/1.4.37)
* [Enabled the built-in TRX reporter](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/134) that is compatible with AzDo test error reporting.
To enable this TRX reporter, add `"useBuiltInTrxReporter": true` inside the `xunit.multinode.runner.json` settings file.

#### 1.1.0 January 6 2022 ####

Version 1.1.0 release.

#### 1.1.0-beta2 December 23 2021 ####
- [Add support for Xunit TestFrameworkAttribute attribute](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/116)

In this release we added `MultiNodeTestFramework` to simplify non-parallel test setup. This test 
framework is a simple override of the built-in `XunitTestFramework` that disables/ignores the
Xunit `CollectionBehaviorAttribute`, put all test classes from a single assembly into a single test 
collection, and disables the test collection parallelization.

To use this test framework, you will need to add an assembly level attribute that tells Xunit to
use this custom test framework instead:

```c#
[assembly: TestFramework("Akka.MultiNode.TestAdapter.MultiNodeTestFramework", "Akka.MultiNode.TestAdapter")]
```

Note that you can also use this assembly level attribute to achieve more or less the same effect:
```c#
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

#### 1.1.0-beta1 October 20 2021 ####

- [Switch to pure Xunit implementation](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/105)

In this release we removed VSTest Adapter and moved to a pure Xunit implementation. This brings about a few changes that needs to be observed:

- Moved `.runsettings` configuration feature to `xunit.multinode.runner.json` 
   
  `.runsettings` content are not passed downstream by `dotnet test` to the actual test runner, so this feature is moved to Xunit-like configuration through a .json file. You can declare your setting file name as either `{assembly_name}.xunit.multinode.runner.json` or `xunit.multinode.runner.json`. Supported settings are:
  - `outputDirectory`: the output directory where all the runner logs will be stored. Note that this is different than the `dotnet test --result-directory` settings which dictates where the VSTest reporter will export their outputs.
    __Default:__ `TestResults` in the folder where the tested assembly is located.
  - `failedSpecsDirectory`:  an output directory __inside the `outputDirectory`__ where all aggregated failed logs will be stored.
    __Default:__ `FAILED_SPECS_LOGS`
  - `listenAddress`: the host name or IP of the machine that is running the test. Will be bound to the TCP logging service. 
    __Default:__  `127.0.0.1` (localhost)
  - `listenPort`: the port where the TCP logging service will be listening to. a random free port will be used if set to 0. 
    __Default:__ 0
  - `appendLogOutput`: if set, all logs are appended to the old logs from previous runs.
    __Default:__ true

- Parallelized test support (__BETA__) 
  
  Tests can be run in parallel now, with caveats. Parallel test is not recommended if any of your tests are very timing dependent;
  it is still recommended that you __do not__ run your tests in parallel. Note that Xunit turns this feature on __by default__, so if your tests are failing, make sure that this feature is properly turned off. Please read the xunit [documentation](https://xunit.net/docs/running-tests-in-parallel) on how to set this up.
  
  Note that the `maxParallelThreads` in Xunit will not be honored by this test adapter because MultiNode tests will spawn a process for every cluster node being used inside the test, inflating the number of threads being used inside a test.

#### 1.0.0 October 20 2019 ####
- Fix [result folder clearing, add documentation](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/95)

#### 1.0.0-beta2 October 05 2019 ####
- Fix, [node runner should ignore runs not started by MNTR](https://github.com/akkadotnet/Akka.MultiNodeTestRunner/pull/93) 

#### 1.0.0-beta1 October 05 2019 ####
First beta release

#### 0.1.13 October 05 2019 ####
Initial commit
