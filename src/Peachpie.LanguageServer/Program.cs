using Peachpie.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            await EnvironmentUtils.InitializeAsync();

            var options = ServerOptions.ParseFromArguments(args);

            var requestReader = new MessageReader(Console.OpenStandardInput());
            var messageWriter = new MessageWriter(Console.OpenStandardOutput());

            AssemblyLoadContext.Default.Resolving += Assembly_Resolving;

            var server = new PhpLanguageServer(options, requestReader, messageWriter);
            await server.Run();
        }

        private static Assembly Assembly_Resolving(AssemblyLoadContext ctx, AssemblyName name)
        {
            // I'm not proud with this but we need to get things working quickly
            // ... we should not reference those packages at all and make use of msbuild on user's machine

            // Sdk is attempting to load its versions from actual NuGetSdkResolver, but we have a different version,
            // and it does not load the ones located in 'C:\Program Files\dotnet\sdk' by itself ...

            if (name.Name == "NuGet.ProjectModel")
            {
                return typeof(NuGet.ProjectModel.BuildOptions).Assembly; // our version :/
            }
            if (name.Name == "NuGet.Frameworks")
            {
                return typeof(NuGet.Frameworks.NuGetFramework).Assembly; // our version :/
            }
            if (name.Name == "NuGet.Common")
            {
                return typeof(NuGet.Common.FileUtility).Assembly; // our version :/
            }
            if (name.Name == "NuGet.Packaging")
            {
                return typeof(NuGet.Packaging.FrameworkReference).Assembly; // our version :/
            }
            if (name.Name == "NuGet.Versioning")
            {
                return typeof(NuGet.Versioning.SemanticVersion).Assembly; // our version :/
            }

            return null;
        }
    }
}
