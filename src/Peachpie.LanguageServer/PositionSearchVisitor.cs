using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Semantics.Graph;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer
{
    internal class PositionSearchVisitor : GraphVisitor
    {
        private int _position;

        private int _visitedColor;

        public IPhpOperation Result { get; private set; }

        public PositionSearchVisitor(int position)
        {
            _position = position;
        }

        public override void VisitCFG(ControlFlowGraph x)
        {
            _visitedColor = x.NewColor();

            base.VisitCFG(x);
        }

        protected override void VisitCFGBlockInternal(BoundBlock x)
        {
            if (x.Tag != _visitedColor)
            {
                x.Tag = _visitedColor;
                base.VisitCFGBlockInternal(x); 
            }
        }

        public override void VisitVariableRef(BoundVariableRef x)
        {
            if (x.PhpSyntax.Span.Contains(_position))
            {
                this.Result = x;
            }
        }
    }
}
