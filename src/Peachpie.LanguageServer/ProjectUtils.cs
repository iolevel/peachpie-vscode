using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;
using Pchp.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public static class ProjectUtils
    {
        private const string DefaultConfiguration = "Debug";
        private static readonly NuGetFramework DefaultFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
        private static ProjectContext _projectContext;

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
