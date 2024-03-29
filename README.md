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

## Building this solution
To run the build script associated with this solution, execute the following:

**Windows**
```
c:\> build.cmd all
```

**Linux / OS X**
```
c:\> build.sh all
```

If you need any information on the supported commands, please execute the `build.[cmd|sh] help` command.

This build script is powered by [FAKE](https://fake.build/); please see their API documentation should you need to make any changes to the [`build.fsx`](build.fsx) file.

### Conventions
The attached build script will automatically do the following based on the conventions of the project names added to this project:

* Any project name ending with `.Tests` will automatically be treated as a [XUnit2](https://xunit.github.io/) project and will be included during the test stages of this build script;
* Any project name ending with `.Tests` will automatically be treated as a [NBench](https://github.com/petabridge/NBench) project and will be included during the test stages of this build script; and
* Any project meeting neither of these conventions will be treated as a NuGet packaging target and its `.nupkg` file will automatically be placed in the `bin\nuget` folder upon running the `build.[cmd|sh] all` command.

### DocFx for Documentation
This solution also supports [DocFx](http://dotnet.github.io/docfx/) for generating both API documentation and articles to describe the behavior, output, and usages of your project. 

All of the relevant articles you wish to write should be added to the `/docs/articles/` folder and any API documentation you might need will also appear there.

All of the documentation will be statically generated and the output will be placed in the `/docs/_site/` folder. 

#### Previewing Documentation
To preview the documentation for this project, execute the following command at the root of this folder:

```
C:\> serve-docs.cmd
```

This will use the built-in `docfx.console` binary that is installed as part of the NuGet restore process from executing any of the usual `build.cmd` or `build.sh` steps to preview the fully-rendered documentation. For best results, do this immediately after calling `build.cmd buildRelease`.

### Release Notes, Version Numbers, Etc
This project will automatically populate its release notes in all of its modules via the entries written inside [`RELEASE_NOTES.md`](RELEASE_NOTES.md) and will automatically update the versions of all assemblies and NuGet packages via the metadata included inside [`common.props`](src/common.props).

If you add any new projects to the solution created with this template, be sure to add the following line to each one of them in order to ensure that you can take advantage of `common.props` for standardization purposes:

```
<Import Project="..\common.props" />
```

### Code Signing via SignService
This project uses [SignService](https://github.com/onovotny/SignService) to code-sign NuGet packages prior to publication. The `build.cmd` and `build.sh` scripts will automatically download the `SignClient` needed to execute code signing locally on the build agent, but it's still your responsibility to set up the SignService server per the instructions at the linked repository.

Once you've gone through the ropes of setting up a code-signing server, you'll need to set a few configuration options in your project in order to use the `SignClient`:

* Add your Active Directory settings to [`appsettings.json`](appsettings.json) and
* Pass in your signature information to the `signingName`, `signingDescription`, and `signingUrl` values inside `build.fsx`.

Whenever you're ready to run code-signing on the NuGet packages published by `build.fsx`, execute the following command:

```
C:\> build.cmd nuget SignClientSecret={your secret} SignClientUser={your username}
```

This will invoke the `SignClient` and actually execute code signing against your `.nupkg` files prior to NuGet publication.

If one of these two values isn't provided, the code signing stage will skip itself and simply produce unsigned NuGet code packages.