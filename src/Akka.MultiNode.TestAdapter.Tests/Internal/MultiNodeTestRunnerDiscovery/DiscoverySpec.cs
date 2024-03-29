﻿//-----------------------------------------------------------------------
// <copyright file="DiscoverySpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.MultiNode.TestAdapter.Internal;
using FluentAssertions;
using Xunit;

namespace Akka.MultiNode.TestAdapter.Tests.Internal.MultiNodeTestRunnerDiscovery
{
    public class DiscoverySpec
    {
        [Fact(DisplayName = "Abstract classes are not discoverable")]
        public void No_abstract_classes()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.False(discoveredSpecs.ContainsKey(KeyFromSpecName(nameof(DiscoveryCases.NoAbstractClassesSpec))));
        }

        [Fact(DisplayName = "Deeply inherited classes are discoverable")]
        public void Deeply_inherited_are_ok()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(
                "DeeplyInheritedChildRole", 
                discoveredSpecs[KeyFromSpecName(nameof(DiscoveryCases.DeeplyInheritedChildSpec))].First().Role);
        }

        [Fact(DisplayName = "Child test class with default constructors are ok")]
        public void Child_class_with_default_constructor_are_ok()
        {
            Action testDelegate = () =>
            {
                var testCase = typeof(DiscoveryCases.DefaultConstructorOnDerivedClassSpec);
                var constructor = MultiNodeTestCase.FindConfigConstructor(testCase);
                constructor.Should().NotBeNull();
            };

            testDelegate.Should().NotThrow();
        }

        [Fact(DisplayName = "One test case per RoleName per Spec declaration with MultiNodeFact")]
        public void Discovered_count_equals_number_of_roles_mult_specs()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(5, discoveredSpecs[KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec1))].Count);
            Assert.Equal(5, discoveredSpecs[KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec2))].Count);
            Assert.Equal(5, discoveredSpecs[KeyFromSpecName(nameof(DiscoveryCases.FloodyChildSpec3))].Count);
        }

        [Fact(DisplayName = "Only the MultiNodeConfig.Roles property is used to compute the number of Roles in MultiNodeFact")]
        public void Only_MultiNodeConfig_role_count_used()
        {
            var discoveredSpecs = DiscoverSpecs();
            Assert.Equal(10, discoveredSpecs[KeyFromSpecName(nameof(DiscoveryCases.NoReflectionSpec))].Select(c => c.Role).Count());
        }
        
        private static Dictionary<string, List<NodeTest>> DiscoverSpecs()
        {
            var assemblyPath = new Uri(typeof(DiscoveryCases).GetTypeInfo().Assembly.Location).LocalPath; 
            
            using (var controller = new XunitFrontController(AppDomainSupport.IfAvailable, assemblyPath))
            {
                using (var discovery = new Discovery(assemblyPath))
                {
                    controller.Find(false, discovery, TestFrameworkOptions.ForDiscovery());
                    discovery.Finished.WaitOne();
                    return discovery
                        .TestCases
                        .ToDictionary(t => t.TypeName, t => t.Nodes);
                }
            }
        }

        private string KeyFromSpecName(string specName)
        {
            return typeof(DiscoveryCases).FullName + "+" + specName;
        }
    }
}
