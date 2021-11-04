// -----------------------------------------------------------------------
// <copyright file="ConfigReader.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using Newtonsoft.Json;

namespace Akka.MultiNode.TestAdapter.Configuration
{
    internal class OptionsException : Exception
    {
        public OptionsException(string message) : base(message)
        {
            
        }
    }
    
    public static class OptionsReader
    {
        public static MultiNodeTestRunnerOptions Load(string assemblyFileName)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyFileName);
            var directoryName = Path.GetDirectoryName(assemblyFileName) ?? "";
            return LoadFile(Path.Combine(directoryName, $"{assemblyName}.xunit.runner.json"))
                   ?? LoadFile(Path.Combine(directoryName, "xunit.runner.json")) 
                   ?? MultiNodeTestRunnerOptions.Default;
        }

        private static MultiNodeTestRunnerOptions LoadFile(string configFileName)
        {
            try
            {
                using var stream = File.OpenRead(configFileName);
                return Load(stream);
            }
            catch
            {
                return null;
            }
        }

        private static MultiNodeTestRunnerOptions Load(Stream configStream)
        {
            using (var reader = new StreamReader(configStream))
            {
                return JsonConvert.DeserializeObject<MultiNodeTestRunnerOptions>(reader.ReadToEnd(),
                    new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    });
            }
        }        
    }
}