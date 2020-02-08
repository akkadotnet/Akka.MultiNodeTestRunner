using Akka.Cluster.TestKit;
using Akka.MultiNode.Shared.Environment;
using Akka.Remote.TestKit;

namespace Akka.MultiNode.TestAdapter.SampleTests
{
    public class SampleMultiNodeSpecConfig : MultiNodeConfig
    {
        public RoleName First { get; }
        public RoleName Second { get; }

        public SampleMultiNodeSpecConfig()
        {
            First = Role("first");
            Second = Role("second");
            
            CommonConfig = DebugConfig(true)
                .WithFallback(MultiNodeClusterSpec.ClusterConfig());
        }
    }

    public class SampleMultiNodeSpec : MultiNodeClusterSpec
    {
        private readonly SampleMultiNodeSpecConfig _config;

        public SampleMultiNodeSpec() : this(new SampleMultiNodeSpecConfig())
        {
        }

        private SampleMultiNodeSpec(SampleMultiNodeSpecConfig config) : base(config, typeof(SampleMultiNodeSpec))
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