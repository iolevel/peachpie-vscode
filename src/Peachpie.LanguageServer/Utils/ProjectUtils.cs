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

        private const string HelperReferenceReturnTarget = "ReturnReferences";

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

                SetupMultitargetingIfNecessary(project);

                var buildResult = await ResolveReferencesAsync(project);
                var projectInstance = buildResult.ProjectStateAfterBuild;

                var metadataReferences = GatherReferences(project, projectInstance, buildResult)
                    .Select(path => MetadataReference.CreateFromFile(path))
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

                var syntaxTrees = ParseSourceFiles(projectInstance);

                var compilation = PhpCompilation.Create(
                    projectName,
                    syntaxTrees,
                    references: metadataReferences,
                    options: options);

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
            var fileContents = new StringReader(File.ReadAllText(projectFile)); // read {projectFile} separately in order to avoid locking it on FS
            var xmlReader = XmlReader.Create(fileContents, XmlSettings);
            var projectCollection = new ProjectCollection();
            var projectRoot = ProjectRootElement.Create(xmlReader, projectCollection);

            // In order to have it accessible from MSBuild
            projectRoot.FullPath = projectFile;

            return new Project(projectRoot, properties, toolsVersion: ToolsVersion, projectCollection: projectCollection);
        }

        private static bool IsPeachPieCompilerImport(ResolvedImport import)
        {
            return import
                .ImportedProject
                .FullPath
                .IndexOf("Peachpie.Compiler.Tools", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPhpProject(Project project)
        {
            return project.Imports.Any(IsPeachPieCompilerImport);
        }

        private static bool IsMultitargetingProject(Project project)
        {
            return project.GetPropertyValue("IsCrossTargetingBuild") == "true";
        }

        private static void SetupMultitargetingIfNecessary(Project project)
        {
            if (IsMultitargetingProject(project))
            {
                // Force DispatchToInnerBuilds target to run a helper target from Peachpie SDK instead of Build
                project.SetProperty("InnerTargets", HelperReferenceReturnTarget);
                project.ReevaluateIfNecessary();
            }
        }

        private static async Task<BuildResult> ResolveReferencesAsync(Project project)
        {
            var projectInstance = project.CreateProjectInstance();
            string target = IsMultitargetingProject(project) ? "DispatchToInnerBuilds" : "ResolveReferences";
            var buildRequestData = new BuildRequestData(projectInstance, new string[] { target });

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

        private static Task<BuildResult> RunBuildAsync(
            ProjectInstance projectInstance,
            BuildRequestData buildRequestData,
            BuildManager buildManager,
            BuildParameters buildParameters)
        {
            var taskSource = new TaskCompletionSource<BuildResult>();

            buildManager.BeginBuild(buildParameters);

            buildManager.PendBuildRequest(buildRequestData).ExecuteAsync(sub =>
            {
                try
                {
                    buildManager.EndBuild();

                    sub.BuildResult.ProjectStateAfterBuild = projectInstance;
                    taskSource.TrySetResult(sub.BuildResult);
                }
                catch (Exception e)
                {
                    taskSource.TrySetException(e);
                }
            }, null);

            return taskSource.Task;
        }

        private static IEnumerable<string> GatherReferences(
            Project project,
            ProjectInstance projectInstance,
            BuildResult buildResult)
        {
            if (IsMultitargetingProject(project))
            {
                // Perform analysis always only on the first listed framework
                string frameworks = project.GetPropertyValue("TargetFrameworks");
                string firstFramework = frameworks.Split(';')[0];

                // Filter all the resulting references from the DispatchToInnerBuilds target by framework
                return buildResult.ResultsByTarget.First().Value.Items
                    .Where(item => item.GetMetadata("TargetFramework") == firstFramework)
                    .Select(item => item.ItemSpec);
            }
            else
            {
                return projectInstance.GetItems("ReferencePath")
                    .Select(item => item.EvaluatedInclude);
            }
        }

        private static PhpSyntaxTree[] ParseSourceFiles(ProjectInstance projectInstance)
        {
            var sourceFiles = projectInstance
                .GetItems("Compile")
                .Select(x => Path.Combine(projectInstance.Directory, x.EvaluatedInclude)).ToArray();

            var syntaxTrees = new PhpSyntaxTree[sourceFiles.Length];

            Parallel.For(0, sourceFiles.Length, i =>
            {
                var path = PathUtils.NormalizePath(sourceFiles[i]);
                if (path.EndsWith(".phar"))
                {
                    // TODO: process phar archives
                    syntaxTrees[i] = PhpSyntaxTree.ParseCode(string.Empty, PhpParseOptions.Default, PhpParseOptions.Default, path);
                }
                else
                {
                    syntaxTrees[i] = PhpSyntaxTree.ParseCode(File.ReadAllText(path), PhpParseOptions.Default, PhpParseOptions.Default, path);
                }
            });

            return syntaxTrees;
        }
    }
}
