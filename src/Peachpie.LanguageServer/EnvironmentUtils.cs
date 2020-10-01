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
        const string DOTNET_CLI_UI_LANGUAGE = "DOTNET_CLI_UI_LANGUAGE";

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
            // Ensure that we set the DOTNET_CLI_UI_LANGUAGE environment variable to "en-US" before
            // running 'dotnet --info'. Otherwise, we may get localized results.
            string originalCliLanguage = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, "en-US");

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
                finally
                {
                    Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, originalCliLanguage);
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

        public static Dictionary<string, string> GetCoreGlobalProperties(string projectPath, string toolsPath)
        {
            string solutionDir = Path.GetDirectoryName(projectPath);
            string extensionsPath = toolsPath;
            string sdksPath = MSBuildSDKsPath;
            string roslynTargetsPath = Path.Combine(toolsPath, "Roslyn");

            return new Dictionary<string, string>
            {
                { "SolutionDir", solutionDir },
                { "MSBuildExtensionsPath", extensionsPath },
                { "MSBuildSDKsPath", sdksPath },
                { "RoslynTargetsPath", roslynTargetsPath },
                { "DesignTimeBuild", "true" },
                { "SkipCompilerExecution", "true" },
                { "ProvideCommandLineArgs", "true", },
            };
        }
    }
}
