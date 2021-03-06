﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using warnings.refactoring;

namespace warnings.conditions
{
    partial class InlineMethodConditionCheckersList
    {
        private class VariableNamesCollisionChecker : InlineMethodConditionsChecker
        {
            public override Predicate<SyntaxNode> GetIssuedNodeFilter()
            {
                return n => n is StatementSyntax;
            }

            public override IConditionCheckingResult CheckInlineMethodCondition(IInlineMethodRefactoring 
                refactoring)
            {
                throw new NotImplementedException();
            }

            public override RefactoringConditionType RefactoringConditionType
            {
                get { throw new NotImplementedException(); }
            }
        }
    }
}
