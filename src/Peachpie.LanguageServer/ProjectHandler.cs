using Microsoft.Build.Execution;
using Pchp.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer
{
    public class ProjectHandler
    {
        public PhpCompilation Compilation { get; }

        public ProjectInstance BuildInstance { get; }

        public string RootPath => BuildInstance.Directory;

        public ProjectHandler(PhpCompilation compilation, ProjectInstance buildInstance)
        {
            this.Compilation = compilation;
            this.BuildInstance = buildInstance;
        }
    }
}
