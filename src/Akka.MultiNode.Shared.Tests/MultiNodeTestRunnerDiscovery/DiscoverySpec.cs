//-----------------------------------------------------------------------
// <copyright file="DiscoverySpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.MultiNode.TestRunner;
using Akka.MultiNode.TestRunner.Shared;
using FluentAssertions;
using Xunit;

namespace Akka.MultiNode.Shared.Tests.MultiNodeTestRunnerDiscovery
{
    public class DiscoverySpec
    {
        [Fact(DisplayName = "Abstract classes are not discoverable")]
        public void No_abstract_classes()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.DoesNotContain(discoveredSpecs, s => s.TypeName == nameof(DiscoveryCases.NoAbstractClassesSpec));
        }

        [Fact(DisplayName = "Deeply inherited classes are discoverable")]
        public void Deeply_inherited_are_ok()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(
                "DeeplyInheritedChildRole", 
                discoveredSpecs
                    .FirstOrDefault(s => s.TypeName == KeyFromSpecName(nameof(DiscoveryCases.DeeplyInheritedChildSpec)))?.Nodes
                    .First().Role);
        }

        [Fact(DisplayName = "Child test class with default constructors are ok")]
        public void Child_class_with_default_constructor_are_ok()
        {
            Action testDelegate = () =>
            {
                var testCase = typeof(DiscoveryCases.DefaultConstructorOnDerivedClassSpec);
                var constructor = MultiNodeTest.FindConfigConstructor(testCase);
                constructor.Should().NotBeNull();
            };

            testDelegate.Should().NotThrow();
        }

        [Fact(DisplayName = "One test case per RoleName per Spec declaration with MultiNodeFact")]
        public void Discovered_count_equals_number_of_roles_mult_specs()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(5, discoveredSpecs
                .FirstOrDefault(s => s.TypeName == KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec1)))?.Nodes.Count);
            Assert.Equal(5, discoveredSpecs
                .FirstOrDefault(s => s.TypeName == KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec2)))?.Nodes.Count);
            Assert.Equal(5, discoveredSpecs
                .FirstOrDefault(s => s.TypeName == KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec3)))?.Nodes.Count);
        }

        [Fact(DisplayName = "Only public props and fields are considered when looking for RoleNames")]
        public void Public_props_and_fields_are_considered()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(
                discoveredSpecs
                    .FirstOrDefault(test => test.TypeName == KeyFromSpecName(nameof(DiscoveryCases.DiverseSpec)))?.Nodes
                    .Select(n => n.Role), new[] {"RoleProp", "RoleField"});
        }

        private static List<MultiNodeTest> DiscoverSpecs()
        {
            var assemblyPath = new Uri(typeof(DiscoveryCases).GetTypeInfo().Assembly.CodeBase).LocalPath; 
            using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
            {
                using (var discovery = new Discovery(assemblyPath))
                {
                    controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                    discovery.Finished.WaitOne();
                    return discovery.Tests;
                }
            }
        }

        private string KeyFromSpecName(string specName)
        {
            return typeof(DiscoveryCases).FullName + "+" + specName;
        }
    }
}
