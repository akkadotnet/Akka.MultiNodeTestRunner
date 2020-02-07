using System;
using Xunit;

namespace Akka.MultiNode.TestAdapter.SampleTests
{
    public class IgnoredXunitTest
    {
        [Fact]
        public void Ignored_test()
        {
            throw new Exception("This test should be ignored by MNTR");
        }
    }
}