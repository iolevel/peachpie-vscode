using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;
using Pchp.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public static class ProjectUtils
    {
        private const string DefaultConfiguration = "Debug";
        private static readonly NuGetFramework DefaultFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
        private static ProjectContext _projectContext;
        private static GlobalSettings _globalSettings;

        public static PhpCompilation TryGetFirstPhpProject(string directory, out string rootPath)
        {
            GlobalSettings globalSettings;
            var settingsFile = Path.Combine(directory, GlobalSettings.FileName);
            if (GlobalSettings.TryGetGlobalSettings(settingsFile, out globalSettings))
            {
                _globalSettings = globalSettings;
                foreach (string searchRelativePath in globalSettings.ProjectSearchPaths)
                {
                    string searchPath = Path.Combine(directory, searchRelativePath);
                    var phpCompilation = TryCreateCompilationFromPath(searchPath);
                    if (phpCompilation != null)
                    {
                        rootPath = searchPath;
                        return phpCompilation;
                    }
                }

                rootPath = null;
                return null;
            }
            else
            {
                var phpCompilation = TryCreateCompilationFromPath(directory);
                if (phpCompilation != null)
                {
                    rootPath = directory;
                    return phpCompilation;
                }
                else
                {
                    rootPath = null;
                    return null;
                }
            }
        }

        private static PhpCompilation TryCreateCompilationFromPath(string directory)
        {
            string projectFile = Path.Combine(directory, Project.FileName);
            if (!File.Exists(projectFile))
            {
                return null;
            }
            else
            {
                return TryCreateCompilationFromProject(projectFile);
            }
        }

        public static PhpCompilation TryCreateCompilationFromProject(string projectFile)
        {
            var projectContext = ProjectContext.Create(projectFile, DefaultFramework);
            _projectContext = projectContext;
            if (!IsPhpProject(projectContext))
            {
                return null;
            }

            var exporter = projectContext.CreateExporter(DefaultConfiguration);
            var libraryExports = exporter.GetDependencies().ToArray();
            var metadataReferences = libraryExports
                .SelectMany(lib => lib.CompilationAssemblies)
                .Select(asset => MetadataReference.CreateFromFile(asset.ResolvedPath))
                .ToArray();
            var options = new PhpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                baseDirectory: PathUtils.NormalizePath(projectContext.ProjectDirectory),
                sdkDirectory: null);

            var compilation = PhpCompilation.Create(
                projectContext.ProjectFile.Name,
                ImmutableArray<PhpSyntaxTree>.Empty,
                metadataReferences,
                options);

            return compilation;
        }

        public static bool IsPhpProject(ProjectContext projectContext)
        {
            var projectTools = projectContext.RootProject.Project.Tools;
            return projectTools.Any(tool => tool.Name == "Peachpie.Compiler.Tools");
        }
    }
}
