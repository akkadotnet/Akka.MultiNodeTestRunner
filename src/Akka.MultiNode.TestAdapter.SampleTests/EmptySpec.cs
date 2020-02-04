using Akka.Cluster.TestKit;
using Akka.MultiNodeTestRunner.Shared;
using Akka.Remote.TestKit;

namespace Akka.MultiNodeTestRunner.SampleTests
{
    public class EmptySpecConfig : MultiNodeConfig
    {
        public RoleName First { get; }
        public RoleName Second { get; }

        public EmptySpecConfig()
        {
            First = Role("first");
            Second = Role("second");
        }
    }

    public class EmptySpec : MultiNodeClusterSpec
    {
        readonly EmptySpecConfig _config;

        public EmptySpec() : this(new EmptySpecConfig())
        {
        }

        private EmptySpec(EmptySpecConfig config) : base(config, typeof(EmptySpec))
        {
            _config = config;
        }

        [MultiNodeFact]
        public void Should_start_and_join_cluster()
        {
            RunOn(StartClusterNode, _config.First);

            EnterBarrier("first-started");

            RunOn(() => Cluster.Join(GetAddress(_config.First)), _config.Second);
            
            EnterBarrier("after");
        }
    }
}