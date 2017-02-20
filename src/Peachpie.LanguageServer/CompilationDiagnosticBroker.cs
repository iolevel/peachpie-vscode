using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public class CompilationDiagnosticBroker
    {
        // How long to wait before running the analysis after a modification
        private const int MillisecondsDelay = 1000;

        private readonly Action<IEnumerable<Diagnostic>> _resultHandler;
        private Task<IEnumerable<Diagnostic>> _diagnosticTask;

        public CompilationDiagnosticBroker(Action<IEnumerable<Diagnostic>> resultHandler)
        {
            _resultHandler = resultHandler;
        }

        public PhpCompilation Compilation { get; private set; }

        public async void UpdateCompilation(PhpCompilation updatedCompilation)
        {
            Compilation = updatedCompilation;

            await AnalyseLazily(updatedCompilation);
        }

        private async Task AnalyseLazily(PhpCompilation updatedCompilation)
        {
            await Task.Delay(MillisecondsDelay);

            // Don't run the analysis if the compilation was modified since then (to enforce the delay)
            // or it is already running (to prevent running two tasks simultaneously)
            if (this.Compilation == updatedCompilation
                && (_diagnosticTask == null || _diagnosticTask.IsCompleted))
            {
                _diagnosticTask = this.Compilation.BindAndAnalyseTask();

                var diagnostics = await _diagnosticTask;
                _resultHandler(diagnostics);

                // If the compilation was changed during the analysis, attempt to run it again (with respect to the delay),
                // as it was blocked by the previous condition
                if (this.Compilation != updatedCompilation)
                {
                    await AnalyseLazily(this.Compilation);
                }
            }
        }
    }
}
