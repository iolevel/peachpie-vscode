using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
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
using System.Threading;
using Microsoft.Build.Logging;

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

        private const string LogFileName = "build.log";

        private static SemaphoreSlim _buildManagerSemaphore = new SemaphoreSlim(1);

        public static async Task<ProjectHandler> TryGetFirstPhpProjectAsync(string directory)
        {
            foreach (var solutionPath in Directory.GetFiles(directory, SolutionNamePattern))
            {
                var solution = TryLoadSolution(solutionPath);
                if (solution != null)
                {
                    foreach (var project in solution.ProjectsInOrder)
                    {
                        var projectHandler = await TryGetPhpProjectAsync(project.AbsolutePath);
                        if (projectHandler != null)
                        {
                            return projectHandler;
                        }
                    }
                }
            }

            foreach (var projectPath in Directory.GetFiles(directory, ProjectNamePattern, SearchOption.AllDirectories))
            {
                var projectHandler = await TryGetPhpProjectAsync(projectPath);
                if (projectHandler != null)
                {
                    return projectHandler;
                }
            }

            return null;
        }

        private static async Task<ProjectHandler> TryGetPhpProjectAsync(string projectFile)
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

                var syntaxTrees = await ParseSourceFilesAsync(projectInstance);

                var compilation = PhpCompilation.Create(
                    projectName,
                    //ImmutableArray<PhpSyntaxTree>.Empty,
                    syntaxTrees,
                    metadataReferences,
                    options);

                return new ProjectHandler(compilation, projectInstance);
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

            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", EnvironmentUtils.NetCoreRuntimePath);
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", EnvironmentUtils.MSBuildSDKsPath);

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

        private static async Task<ProjectInstance> ResolveReferencesAsync(Project project)
        {

            var projectInstance = project.CreateProjectInstance();
            var buildRequestData = new BuildRequestData(projectInstance, new string[] { "ResolveReferences" });

            var buildManager = BuildManager.DefaultBuildManager;
            var buildParameters = new BuildParameters(project.ProjectCollection);

#if DEBUG
            // Log output in debug mode
            string logFilePath = Path.Combine(
                project.DirectoryPath,
                projectInstance.GetPropertyValue("BaseIntermediateOutputPath"),   // "obj" subfolder
                LogFileName);

            buildParameters.Loggers = new ILogger[]
            {
                new FileLogger()
                {
                    Verbosity = LoggerVerbosity.Detailed,
                    Parameters = $"LogFile={logFilePath}"
                }
            };
#endif

            await _buildManagerSemaphore.WaitAsync();
            try
            {
                return await RunBuildAsync(projectInstance, buildRequestData, buildManager, buildParameters);
            }
            finally
            {
                _buildManagerSemaphore.Release();
            }
        }

        private static Task<ProjectInstance> RunBuildAsync(
            ProjectInstance projectInstance,
            BuildRequestData buildRequestData,
            BuildManager buildManager,
            BuildParameters buildParameters)
        {
            var taskSource = new TaskCompletionSource<ProjectInstance>();

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

        private static async Task<PhpSyntaxTree[]> ParseSourceFilesAsync(ProjectInstance projectInstance)
        {
            // TODO: Determine the right files by inspecting the MSBuild project
            string[] sourceFiles = Directory.GetFiles(projectInstance.Directory, "*.php", SearchOption.AllDirectories);

            var syntaxTrees = new PhpSyntaxTree[sourceFiles.Length];

            var tasks = Enumerable.Range(0, sourceFiles.Length).Select((i) => Task.Run(async () =>
            {
                string path = PathUtils.NormalizePath(sourceFiles[i]);
                string code;
                using (var reader = File.OpenText(path))
                {
                    code = await reader.ReadToEndAsync();
                }
                syntaxTrees[i] = PhpSyntaxTree.ParseCode(code, PhpParseOptions.Default, PhpParseOptions.Default, path);
            })).ToArray();

            await Task.WhenAll(tasks);

            return syntaxTrees;
        }
    }
}
