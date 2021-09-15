using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Akka.Remote.TestKit;
using Xunit.Abstractions;

namespace Akka.MultiNode.Shared
{
    public class MultiNodeTest
    {
        protected MultiNodeTest() { }
        public MultiNodeTest(ITestCaseDiscoveryMessage discovery, string assemblyPath)
        {
            _discovery = discovery;
            AssemblyPath = Path.GetFullPath(assemblyPath);
            Console.WriteLine($"!!!!!!!!!!!! {AssemblyPath}"); 
            
            TypeName = discovery.TestClass.Class.Name;
            MethodName = discovery.TestMethod.Method.Name;
            SkipReason = discovery.TestCase.SkipReason;
        }

        private ITestCaseDiscoveryMessage _discovery;

        public virtual string AssemblyPath { get; }
        public virtual string TypeName { get; }
        public virtual string MethodName { get; }
        public string TestName => $"{TypeName}.{MethodName}";
        public virtual string SkipReason { get; set; }

        protected List<NodeTest> InternalNodes;

        /// <exception cref="TestBaseTypeException">Spec did not inherit from <see cref="MultiNodeSpec"/></exception>
        /// <exception cref="TestConfigurationException">Invalid configuration class</exception>
        public List<NodeTest> Nodes => InternalNodes ?? (InternalNodes = LoadDetails());

        /// <exception cref="TestBaseTypeException">Spec did not inherit from <see cref="MultiNodeSpec"/></exception>
        /// <exception cref="TestConfigurationException">Invalid configuration class</exception>
        protected virtual List<NodeTest> LoadDetails()
        {
#if CORECLR
            var specType = _discovery.TestAssembly.Assembly.GetType(TypeName).ToRuntimeType();
#else
            var testAssembly = Assembly.LoadFrom(discovery.TestAssembly.Assembly.AssemblyPath);
            var specType = testAssembly.GetType(TypeName);
#endif
            if (!typeof(MultiNodeSpec).IsAssignableFrom(specType))
            {
                throw new TestBaseTypeException();
            }
                
            var roles = RoleNames(specType);
            _discovery = null;
            
            return roles.Select((r, i) => new NodeTest
            {
                Node = i + 1,
                Role = r.Name,
                Test = this
            }).ToList();
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
                throw new TestConfigurationException(specType, e);
            }
        }
        
        internal static ConstructorInfo FindConfigConstructor(Type configUser)
        {
            var baseConfigType = typeof(MultiNodeConfig);
            var current = configUser;
            while (current != null)
            {

#if CORECLR
                var ctorWithConfig = current
                    .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(c => null != c.GetParameters().FirstOrDefault(p => p.ParameterType.GetTypeInfo().IsSubclassOf(baseConfigType)));
            
                current = current.GetTypeInfo().BaseType;
#else
                var ctorWithConfig = current
                    .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(c => null != c.GetParameters().FirstOrDefault(p => p.ParameterType.IsSubclassOf(baseConfigType)));

                current = current.BaseType;
#endif
                if (ctorWithConfig != null) return ctorWithConfig;
            }

            throw new TestConfigurationException(configUser);
        }

        private object[] ConfigConstructorParamValues(Type configType)
        {
            var ctors = configType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var empty = ctors.FirstOrDefault(c => !c.GetParameters().Any());

#if CORECLR
            return empty != null
                ? new object[0]
                : ctors.First().GetParameters().Select(p => p.ParameterType.GetTypeInfo().IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
#else
            return empty != null
                ? new object[0]
                : ctors.First().GetParameters().Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
#endif
        }
    }
}