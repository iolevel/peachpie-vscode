using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using NuGet.Frameworks;
using Pchp.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Peachpie.LanguageServer
{
    public static class ProjectUtils
    {
        private const string SolutionNamePattern = "*.sln";
        private const string ProjectNamePattern = "*.msbuildproj";

        private const string ToolsVersion = "15.0";
        private static readonly XmlReaderSettings XmlSettings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Prohibit
        };

        public struct ProjectSearchResult
        {
            public PhpCompilation Compilation { get; }
            public string ProjectPath { get; }

            public ProjectSearchResult(PhpCompilation compilation, string projectPath)
            {
                this.Compilation = compilation;
                this.ProjectPath = projectPath;
            }
        }

        public static async Task<ProjectSearchResult> TryGetFirstPhpProjectAsync(string directory)
        {
            foreach (var solutionPath in Directory.GetFiles(directory, SolutionNamePattern))
            {
                var solution = TryLoadSolution(solutionPath);
                if (solution != null)
                {
                    foreach (var project in solution.ProjectsInOrder)
                    {
                        var phpCompilation = await TryCreateCompilationFromProjectAsync(project.AbsolutePath);
                        if (phpCompilation != null)
                        {
                            return new ProjectSearchResult(phpCompilation, project.AbsolutePath);
                        }
                    }
                }
            }

            foreach (var projectPath in Directory.GetFiles(directory, ProjectNamePattern, SearchOption.AllDirectories))
            {
                var phpCompilation = await TryCreateCompilationFromProjectAsync(projectPath);
                if (phpCompilation != null)
                {
                    return new ProjectSearchResult(phpCompilation, projectPath);
                }
            }

            return default(ProjectSearchResult);
        }

        public static async Task<PhpCompilation> TryCreateCompilationFromProjectAsync(string projectFile)
        {
            try
            {
                Project project = LoadProject(projectFile);

                if (!IsPhpProject(project))
                {
                    return null;
                }

                var projectInstance = await ResolveReferencesAsync(project);
                if (projectInstance == null)
                {
                    return null;
                }

                var metadataReferences = projectInstance.GetItems("ReferencePath")
                    .Select(item => MetadataReference.CreateFromFile(item.EvaluatedInclude))
                    .ToArray();

                if (metadataReferences.Length == 0)
                {
                    // dotnet restore hasn't run yet
                    return null;
                }

                var options = new PhpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary,
                    baseDirectory: PathUtils.NormalizePath(Path.GetDirectoryName(projectFile)),
                    sdkDirectory: null);

                // TODO: Get from MSBuild
                string projectName = Path.GetFileNameWithoutExtension(projectFile);

                var compilation = PhpCompilation.Create(
                    projectName,
                    ImmutableArray<PhpSyntaxTree>.Empty,
                    metadataReferences,
                    options);

                return compilation;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SolutionFile TryLoadSolution(string solutionPath)
        {
            try
            {
                return SolutionFile.Parse(solutionPath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Project LoadProject(string projectFile)
        {
            var properties = new Dictionary<string, string>()
            {
                // In order not to build the dependent projects
                { "DesignTimeBuild", "true" },
            };

            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", AppContext.BaseDirectory);
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", @"C:\Program Files\dotnet\sdk\1.0.0\Sdks");

            // TODO: Make properly async
            var fileContents = new MemoryStream(File.ReadAllBytes(projectFile));
            var xmlReader = XmlReader.Create(fileContents, XmlSettings);
            var projectCollection = new ProjectCollection();
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);

            // In order to have it accessible from MSBuild
            projectRoot.FullPath = projectFile;

            return new Project(projectRoot, properties, toolsVersion: ToolsVersion, projectCollection: projectCollection);
        }

        private static bool IsPhpProject(Project project)
        {
            // It understands the word after the last dot as a file extension, hence the concatenation
            return project.GetItems("DotNetCliToolReference")
                .Any(item => item.GetMetadataValue("Filename") + item.GetMetadataValue("Extension") == "Peachpie.Compiler.Tools");
        }

        private static Task<ProjectInstance> ResolveReferencesAsync(Project project)
        {
            var taskSource = new TaskCompletionSource<ProjectInstance>();

            var projectInstance = project.CreateProjectInstance();

            var buildRequestData = new BuildRequestData(projectInstance, new string[] { "ResolveReferences" });

            // TODO: Implement async locking
            var buildManager = BuildManager.DefaultBuildManager;

            var buildParameters = new BuildParameters(project.ProjectCollection);
            buildManager.BeginBuild(buildParameters);

            buildManager.PendBuildRequest(buildRequestData).ExecuteAsync(sub =>
            {
                try
                {
                    buildManager.EndBuild();
                    taskSource.TrySetResult(projectInstance);
                }
                catch (Exception e)
                {
                    taskSource.TrySetException(e);
                }
            }, null);

            return taskSource.Task;
        }

        /// <remarks>
        /// Copied from Microsoft.DotNet.Tools.MSBuild.MSBuildForwardingApp.GetMSBuildSDKsPath().
        /// </remarks>
        private static string GetMSBuildSDKsPath()
        {
            string envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");
            if (envMSBuildSDKsPath != null)
            {
                return envMSBuildSDKsPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "Sdks");
        }
    }
}
