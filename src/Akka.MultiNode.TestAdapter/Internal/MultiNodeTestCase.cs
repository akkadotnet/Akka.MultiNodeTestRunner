using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Akka.Remote.TestKit;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using TestCaseFinished = Xunit.Sdk.TestCaseFinished;
using TestCaseStarting = Xunit.Sdk.TestCaseStarting;
using TestMethodDisplay = Xunit.Sdk.TestMethodDisplay;
using TestMethodDisplayOptions = Xunit.Sdk.TestMethodDisplayOptions;
using TestResultMessage = Xunit.Sdk.TestResultMessage;
using TestSkipped = Xunit.Sdk.TestSkipped;
using TestStarting = Xunit.Sdk.TestStarting;
using TestPassed = Xunit.Sdk.TestPassed;
using TestFailed = Xunit.Sdk.TestFailed;
using TestFinished = Xunit.Sdk.TestFinished;

namespace Akka.MultiNode.TestAdapter.Internal
{
    public class MultiNodeTestCase : XunitTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public MultiNodeTestCase() { }

        public MultiNodeTestCase(
            IMessageSink diagnosticMessageSink,
            TestMethodDisplay defaultMethodDisplay,
            TestMethodDisplayOptions defaultMethodDisplayOptions,
            ITestMethod testMethod,
            object[] testMethodArguments = null)
            : base(
                diagnosticMessageSink,
                defaultMethodDisplay,
                defaultMethodDisplayOptions,
                testMethod,
                testMethodArguments)
        {
            AssemblyPath = Path.GetFullPath(testMethod.TestClass.Class.Assembly.AssemblyPath);
        }
        
        public virtual string AssemblyPath { get; protected set; }
        public virtual string TypeName => TestMethod.TestClass.Class.Name;
        public virtual string MethodName => TestMethod.Method.Name;

        private List<NodeTest> _nodes;

        /// <exception cref="TestBaseTypeException">Spec did not inherit from <see cref="MultiNodeSpec"/></exception>
        /// <exception cref="TestConfigurationException">Invalid configuration class</exception>
        public List<NodeTest> Nodes
        {
            get
            {
                EnsureInitialized();
                return _nodes;
            }
        }

        private string _skipReason;
        public new string SkipReason
        {
            get => _skipReason ?? base.SkipReason;
            set => _skipReason = value;
        }

        protected override void Initialize()
        {
            base.Initialize();
            try
            {
                _nodes = LoadDetails();
            }
            catch (Exception e)
            {
                InitializationException = e;
                DisplayName = $"{BaseDisplayName}(???)";
            }
        }

        internal void Load()
        {
            EnsureInitialized();
        }

        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            
            Environment.SetEnvironmentVariable(MultiNodeFactAttribute.MultiNodeTestEnvironmentName, "1");
            var summary = new RunSummary();
            
            if (!messageBus.QueueMessage(new TestCaseStarting(this)))
                cancellationTokenSource.Cancel();
            else
            {
                try
                {
                    #region XunitTestRunner.RunAsync(), TestRunner.RunAsync()

                    var test = new XunitTest(this, DisplayName);
                    var runSummary = new RunSummary {Total = 1};
                    var output = string.Empty;

                    if (!messageBus.QueueMessage(new TestStarting(test)))
                    {
                        cancellationTokenSource.Cancel();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(SkipReason))
                        {
                            runSummary.Skipped++;
                            if(!messageBus.QueueMessage(new TestSkipped(test, SkipReason)))
                                cancellationTokenSource.Cancel();
                        }
                        else
                        {
                            var testAggregator = new ExceptionAggregator(aggregator);
                            if (!testAggregator.HasExceptions)
                            {
                                var tuple = await testAggregator.RunAsync(async () =>
                                {
                                    // TODO: Actual test goes here?
                                    await Task.Delay(200);
                                    return new Tuple<decimal, string>( new decimal(0.2), "TestResult");
                                });
                                if (tuple != null)
                                {
                                    runSummary.Time = tuple.Item1;
                                    output = tuple.Item2;
                                }
                            }

                            var exception = testAggregator.ToException();
                            TestResultMessage testResult;
                            if (exception == null)
                                testResult = new TestPassed(test, runSummary.Time, output);
                            else
                            {
                                testResult = new TestFailed(test, runSummary.Time, output, exception);
                                runSummary.Failed++;
                            }

                            if (!cancellationTokenSource.IsCancellationRequested)
                                if (!messageBus.QueueMessage(testResult))
                                    cancellationTokenSource.Cancel();
                        }

                        if(!messageBus.QueueMessage(new TestFinished(test, runSummary.Time, output)))
                            cancellationTokenSource.Cancel();
                    }
                    #endregion
                    
                    //summary = await base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
                    summary.Aggregate(runSummary);
                }
                finally
                {
                    if(!messageBus.QueueMessage(new TestCaseFinished(this, summary.Time, summary.Total, summary.Failed, summary.Skipped)))
                        cancellationTokenSource.Cancel();
                }
            }
            
            return summary;
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);
            data.AddValue(nameof(AssemblyPath), AssemblyPath);
            data.AddValue(nameof(_skipReason), _skipReason);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);
            AssemblyPath = data.GetValue<string>(nameof(AssemblyPath));
            _skipReason = data.GetValue<string>(nameof(_skipReason));
        }

        /// <exception cref="TestBaseTypeException">Spec did not inherit from <see cref="MultiNodeSpec"/></exception>
        /// <exception cref="TestConfigurationException">Invalid configuration class</exception>
        protected virtual List<NodeTest> LoadDetails()
        {
            var specType = TestMethod.TestClass.Class.Assembly.GetType(TypeName).ToRuntimeType();
            if (!typeof(MultiNodeSpec).IsAssignableFrom(specType))
            {
                throw new TestBaseTypeException();
            }
            
            var roles = RoleNames(specType);

            return roles.Select((r, i) => new NodeTest(this, i + i, r.Name)).ToList();
        }
        
        private IEnumerable<RoleName> RoleNames(Type specType)
        {
            var ctorWithConfig = FindConfigConstructor(specType);
            try
            {
                var configType = ctorWithConfig.GetParameters().First().ParameterType;
                var args = ConfigConstructorParamValues(configType);
                var configInstance = Activator.CreateInstance(configType, args);
                var roleType = typeof(RoleName);
                var configProps = configType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var roleProps = configProps.Where(p => p.PropertyType == roleType && p.Name != "Myself")
                    .Select(p => (RoleName) p.GetValue(configInstance));
                var configFields = configType.GetFields(BindingFlags.Instance | BindingFlags.Public);
                var roleFields = configFields.Where(f => f.FieldType == roleType && f.Name != "Myself")
                    .Select(f => (RoleName) f.GetValue(configInstance));
                var roles = roleProps.Concat(roleFields).Distinct();
                return roles;
            }
            catch (Exception e)
            {
                throw new TestConfigurationConstructorException(specType, e);
            }
        }
        
        internal static ConstructorInfo FindConfigConstructor(Type configUser)
        {
            var baseConfigType = typeof(MultiNodeConfig);
            var current = configUser;
            while (current != null)
            {

                var ctorWithConfig = current
                    .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(c => null != c.GetParameters().FirstOrDefault(p => p.ParameterType.GetTypeInfo().IsSubclassOf(baseConfigType)));
            
                current = current.GetTypeInfo().BaseType;
                if (ctorWithConfig != null) return ctorWithConfig;
            }

            throw new TestConfigurationException(configUser);
        }

        private object[] ConfigConstructorParamValues(Type configType)
        {
            var ctors = configType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var empty = ctors.FirstOrDefault(c => !c.GetParameters().Any());

            return empty != null
                ? new object[0]
                : ctors.First().GetParameters().Select(p => p.ParameterType.GetTypeInfo().IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        }
    }
}