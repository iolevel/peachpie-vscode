using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public static class EnvironmentUtils
    {
        public static string NetCoreRuntimePath { get; private set; }

        public static string MSBuildSDKsPath { get; private set; }

        /// <summary>
        /// Initializes the properties.
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (NetCoreRuntimePath != null)
            {
                return;
            }

            NetCoreRuntimePath = await GetNetCorePathAsync();

            MSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
            if (MSBuildSDKsPath == null)
            {
                MSBuildSDKsPath = Path.Combine(NetCoreRuntimePath, "Sdks");
            }
        }

        private static async Task<string> GetNetCorePathAsync()
        {
            using (var process = new Process()
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    FileName = "dotnet",
                    Arguments = "--info"
                },
            })
            {
                try
                {
                    process.Start();
                }
                catch
                {
                    // no `dotnet`
                    return null;
                }

                var output = await process.StandardOutput.ReadToEndAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    var regex = new Regex("Base Path:(.+)");
                    var matches = regex.Match(output);
                    if (matches.Groups.Count >= 2)
                    {
                        return matches.Groups[1].Value.Trim();
                    }
                }
            }

            return null;
        }

        private static void ProcessDotnetInfoOutput(string output, ref TaskCompletionSource<string> taskSource)
        {
            if (!string.IsNullOrEmpty(output))
            {
                var regex = new Regex("Base Path:(.+)");
                var matches = regex.Match(output);
                if (matches.Groups.Count >= 2)
                {
                    string result = matches.Groups[1].Value.Trim();
                    taskSource.SetResult(result);

                    return;
                }
            }

            taskSource.SetException(new IOException("Cannot obtain the base path of .NET Core"));
        }
    }
}
