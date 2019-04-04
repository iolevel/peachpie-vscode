using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Semantics.Graph;
using Pchp.CodeAnalysis.Symbols;
using Peachpie.CodeAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Peachpie.LanguageServer
{
    internal class SourceSymbolSearcher : GraphExplorer<VoidStruct>
    {
        public class SymbolStat
        {
            public Span Span { get; set; }
            public IPhpExpression BoundExpression { get; set; }
            public ISymbol Symbol { get; set; }
            public TypeRefContext TypeCtx { get; }

            public SymbolStat(TypeRefContext tctx, Span span, IPhpExpression expr = null, ISymbol symbol = null)
            {
                this.TypeCtx = tctx;
                this.Span = span;
                this.BoundExpression = expr;
                this.Symbol = symbol;
            }
        }

        private int _position;

        private TypeRefContext _tctx;
        private SymbolStat _result;

        PhpCompilation DeclaringCompilation { get; set; }

        private SourceSymbolSearcher(int position)
        {
            _position = position;
        }

        public static SymbolStat SearchCFG(PhpCompilation compilation, ControlFlowGraph cfg, int position)
        {
            var visitor = new SourceSymbolSearcher(position)
            {
                DeclaringCompilation = compilation,
            };
            visitor.VisitCFG(cfg);
            return visitor._result;
        }

        public static SymbolStat SearchParameters(IPhpRoutineSymbol routine, int position)
        {
            var parameter = routine.Parameters.FirstOrDefault(p => p.GetSpan().Contains(position));
            if (parameter != null)
            {
                TypeRefContext typeCtx = routine.ControlFlowGraph?.FlowContext?.TypeRefContext;
                return new SymbolStat(typeCtx, parameter.GetSpan(), null, parameter);
            }
            else
            {
                return null;
            }
        }

        public static SymbolStat SearchMembers(IPhpTypeSymbol type, int position)
        {
            return null;
        }

        protected override void VisitCFGInternal(ControlFlowGraph x)
        {
            _tctx = x.FlowContext?.TypeRefContext;
            base.VisitCFGInternal(x);
        }

        protected override void DefaultVisitUnexploredBlock(BoundBlock x)
        {
            if (_result == null)
            {
                base.DefaultVisitUnexploredBlock(x);
            }
        }

        public override VoidStruct VisitVariableRef(BoundVariableRef x)
        {
            if (x.PhpSyntax?.Span.Contains(_position) == true)
            {
                var symbolOpt = x is ILocalReferenceOperation loc ? loc.Local : null;

                _result = new SymbolStat(_tctx, x.PhpSyntax.Span, x, symbolOpt);
            }

            //
            return base.VisitVariableRef(x);
        }

        protected override VoidStruct DefaultVisitOperation(BoundOperation x)
        {
            if (x is IBoundTypeRef tref && tref.PhpSyntax != null && tref.PhpSyntax.Span.Contains(_position))
            {
                var symbol = tref.ResolveTypeSymbol(DeclaringCompilation);
                if (symbol != null)
                {
                    _result = new SymbolStat(_tctx, tref.PhpSyntax.Span, null, symbol);
                }
            }

            return base.DefaultVisitOperation(x);
        }

        protected override VoidStruct VisitRoutineCall(BoundRoutineCall x)
        {
            if (x.PhpSyntax != null && x.PhpSyntax.Span.Contains(_position) == true)
            {
                var invocation = (IInvocationOperation)x;
                if (invocation.TargetMethod != null)
                {
                    if (!invocation.TargetMethod.IsImplicitlyDeclared || invocation.TargetMethod is IErrorMethodSymbol)
                    {
                        Span span;
                        if (x.PhpSyntax is FunctionCall)
                        {
                            span = ((FunctionCall)x.PhpSyntax).NameSpan;
                            if (span.Contains(_position))
                            {
                                _result = new SymbolStat(_tctx, span, x, invocation.TargetMethod);
                            }
                        }
                    }
                }
            }

            //
            return base.VisitRoutineCall(x);
        }

        public override VoidStruct VisitGlobalConstUse(BoundGlobalConst x)
        {
            if (x.PhpSyntax?.Span.Contains(_position) == true)
            {
                _result = new SymbolStat(_tctx, x.PhpSyntax.Span, x, null);
            }

            //
            return base.VisitGlobalConstUse(x);
        }

        public override VoidStruct VisitPseudoConstUse(BoundPseudoConst x)
        {
            if (x.PhpSyntax?.Span.Contains(_position) == true)
            {
                _result = new SymbolStat(_tctx, x.PhpSyntax.Span, x, null);
            }

            return base.VisitPseudoConstUse(x);
        }

        public override VoidStruct VisitFieldRef(BoundFieldRef x)
        {
            if (x.PhpSyntax?.Span.Contains(_position) == true)
            {
                Span span = x.PhpSyntax.Span;

                //if (x.PhpSyntax is StaticFieldUse)
                //{
                //    span = ((StaticFieldUse)x.PhpSyntax).NameSpan;
                //}
                //else if (x.PhpSyntax is ClassConstUse)
                //{
                //    span = ((ClassConstUse)x.PhpSyntax).NamePosition;
                //}

                if (span.IsValid)
                {
                    _result = new SymbolStat(_tctx, span, x, ((IMemberReferenceOperation)x).Member);
                }
            }

            //
            return base.VisitFieldRef(x);
        }
    }
}
