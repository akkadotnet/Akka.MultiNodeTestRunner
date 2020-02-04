using Akka.Cluster.TestKit;
using Akka.MultiNode.NodeRunner;
using Akka.MultiNode.Shared.Environment;
using Akka.MultiNode.TestRunner.Shared;
using Akka.Remote.TestKit;

namespace Akka.MultiNode.TestAdapter.SampleTests
{
    public class EmptySpecConfig : MultiNodeConfig
    {
        public RoleName First { get; }
        public RoleName Second { get; }

        public EmptySpecConfig()
        {
            First = Role("first");
            Second = Role("second");
            
            CommonConfig = DebugConfig(true)
                .WithFallback(MultiNodeClusterSpec.ClusterConfig());
        }
    }

    public class EmptySpec : MultiNodeClusterSpec
    {
        private readonly EmptySpecConfig _config;

        public EmptySpec() : this(new EmptySpecConfig())
        {
        }

        private EmptySpec(EmptySpecConfig config) : base(config, typeof(EmptySpec))
        {
            _config = config;
        }

        // [MultiNodeFact]
        [CustomMultiNodeFact]
        public void Should_start_and_join_cluster()
        {
            RunOn(StartClusterNode, _config.First);

            EnterBarrier("first-started");

            RunOn(() => Cluster.Join(GetAddress(_config.First)), _config.Second);
            
            EnterBarrier("after");
        }
    }
}