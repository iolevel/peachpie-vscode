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
            // Prevent infinite loop and stop searching after finding the result
            if (x.Tag != _visitedColor && this.Result == null)
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
