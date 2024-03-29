﻿//-----------------------------------------------------------------------
// <copyright file="FileNameGenerator.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.IO;

namespace Akka.MultiNode.TestAdapter.Internal.Persistence
{
    internal static class FileNameGenerator
    {
        public static string GenerateFileName(string assemblyName, string platform, string fileExtension)
        {
            return GenerateFileName(assemblyName, platform, fileExtension, DateTime.UtcNow);
        }

        public static string GenerateFileName(string assemblyName, string platform, string fileExtension, DateTime utcNow)
        {
            return $"{assemblyName.Replace(".dll", "")}-{platform}{fileExtension}";
        }

        public static string GenerateFileName(string folderPath, string assemblyName, string platform, string fileExtension, DateTime utcNow)
        {
            if(string.IsNullOrEmpty(folderPath))
                return GenerateFileName(assemblyName, platform, fileExtension, utcNow);
            var assemblyNameOnly = Path.GetFileName(assemblyName);
            return Path.Combine(folderPath, GenerateFileName(assemblyNameOnly, platform, fileExtension, utcNow));
        }
    }
}
