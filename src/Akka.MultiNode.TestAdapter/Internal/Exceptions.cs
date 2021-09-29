using System;
using Akka.Remote.TestKit;

namespace Akka.MultiNode.TestAdapter.Internal
{
    public class TestConfigurationException: Exception
    {
        public TestConfigurationException(Type type)
            : base(
                $"[{type}] or one of its base classes must specify constructor, " +
                $"which first parameter is a subclass of {typeof(MultiNodeConfig)}")
        { }

        public TestConfigurationException(Type type, Exception innerException)
            : base(
                $"[{type}] or one of its base classes must specify constructor, " +
                $"which first parameter is a subclass of {typeof(MultiNodeConfig)}", 
                innerException)
        { }
    }

    public class TestBaseTypeException: Exception
    {
        public TestBaseTypeException() 
            : base($"MultiNode.TestRunner spec should inherit from {typeof(MultiNodeSpec).FullName}")
        { }
    }
}