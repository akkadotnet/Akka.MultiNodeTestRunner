// //-----------------------------------------------------------------------
// // <copyright file="FileLogger.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2019 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2019 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;

namespace Akka
{
    public static class FileLogger
    {
        public static bool Enabled { get; set; } = true;
        private const string Path = @"C:\Projects\Upwork\Aaron\DEBUG_LOG.txt";
        private static volatile object _lockObj = new object();
        private static readonly Random Random = new Random();
        private static readonly ConcurrentQueue<(DateTime Time, long Ticks, string Message)> PendingMessages = new ConcurrentQueue<(DateTime Time, long Ticks, string Message)>();
        
        [Obsolete("Better to use async version to reduce performance influence")]
        public static void Write(string message)
        {
            if (!Enabled)
                return;
            
            lock (_lockObj)
            {
                while (true)
                {
                    try
                    {
                        File.AppendAllText(Path, $"[{DateTime.UtcNow:yyyy-MM-dd_HH:mm:ss.fff}] {message}\r\n");
                        break;
                    }
                    catch
                    {
                        /* Sometimes may try logging to same file from multiple process, i.e. in MNTR tests, so need to try again */
                        Thread.Sleep(500 + Random.Next(0, 500));
                    }
                }
            }
        }

        public static void WriteAsync(string message)
        {
            if (!Enabled)
                return;
            
            var now = DateTime.UtcNow;
            var ticks = Util.MonotonicClock.ElapsedHighRes.Ticks;
            PendingMessages.Enqueue((now, ticks, message));
        }

        public static void Flush()
        {
            if (!Enabled)
                return;
            
            File.AppendAllText(
                Path, 
                string.Join("\r\n", PendingMessages.Select(pair => $"[{pair.Time:yyyy-MM-dd_HH:mm:ss.fff}][{pair.Ticks}] {pair.Message}"))
            );
        }
    }
}