﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media;
using NLog;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using Roslyn.Services;
using Roslyn.Services.Editor;
using warnings.analyzer;
using warnings.analyzer.comparators;
using warnings.components;
using warnings.refactoring;
using warnings.refactoring.detection;
using warnings.resources;
using warnings.retriever;
using warnings.util;

namespace warnings.conditions
{
    internal partial class ExtractMethodConditionsList
    {
        /// <summary>
        /// This checker is checking whether the extracted method has taken enough or more than enough 
        /// parameters than actual need.
        /// </summary>
        private class ParametersChecker : ExtractMethodConditionChecker
        {
            private Logger logger = NLoggerUtil.GetNLogger(typeof (ParametersChecker));

            public override Predicate<SyntaxNode> GetIssuedNodeFilter()
            {
                return n => n.Kind == SyntaxKind.ParameterList;
            }

            protected override IConditionCheckingResult CheckCondition(
                IManualExtractMethodRefactoring refactoring)
            {
                var before = refactoring.BeforeDocument;

                // Calculate the needed typeNameTuples, depending on what to extract.
                IEnumerable<ISymbol> needed;
                if (refactoring.ExtractedStatements != null)
                    needed = ConditionCheckersUtils.GetUsedButNotDeclaredData(refactoring.ExtractedStatements, 
                        before);
                else
                    needed = ConditionCheckersUtils.GetFlowInData(refactoring.ExtractedExpression, before);

                // Logging the needed typeNameTuples.
                logger.Info("Needed typeNameTuples: " + StringUtil.ConcatenateAll(",", needed.Select(s => 
                    s.Name)));

                // Calculate the missing symbols and the extra symbols, also, trivial to show 'this' so 
                // remove.
                needed = ConditionCheckersUtils.RemoveThisSymbol(needed);

                // Among the missing parameters, some of them are already by a parameter of the newly 
                // extracted method.
                var parameterNames = GetParameterNames(refactoring.ExtractedMethodDeclaration);
                var missing = needed.Where(s => !parameterNames.Contains(s.Name));

                // if missing is not empty, then some typeNameTuples are needed. 
                if (missing.Any())
                {
                    logger.Info("Missing Parameters Issue Found.");
                    return new ParameterCheckingCodeIssueComputer(refactoring.ExtractedMethodDeclaration,
                        ConditionCheckersUtils.GetTypeNameTuples(missing), refactoring.MetaData);
                }
             
                // Otherwise, return no problem.
                return new SingleDocumentCorrectRefactoringResult(refactoring, this.RefactoringConditionType);
            }

            /// <summary>
            /// Get the names of the parameters for a given method declaration.
            /// </summary>
            /// <param name="method">the given method declaration</param>
            /// <returns></returns>
            private IEnumerable<string> GetParameterNames(SyntaxNode method)
            {
                var names = new List<string>();
                var analyzer = AnalyzerFactory.GetMethodDeclarationAnalyzer();
                analyzer.SetMethodDeclaration(method);
                var parameters = analyzer.GetParameters();
                var parameterAnalyzer = AnalyzerFactory.GetParameterAnalyzer();
                foreach (var parameter in parameters)
                {
                    parameterAnalyzer.SetParameter(parameter);
                    names.Add(parameterAnalyzer.GetIdentifier().GetText());
                }
                return names;
            }



            public override RefactoringConditionType RefactoringConditionType
            {
                get { return RefactoringConditionType.EXTRACT_METHOD_PARAMETER; }
            }

            /// <summary>
            /// Code issue computer for parameter checking results.
            /// </summary>
            private class ParameterCheckingCodeIssueComputer : SingleDocumentValidCodeIssueComputer,
                IUpdatableCodeIssueComputer
            {
                private readonly SyntaxNode declaration;
                private readonly IEnumerable<Tuple<string, string>> typeNameTuples;
                private readonly IComparer<SyntaxNode> methodNameComparer;
                private readonly Logger logger = NLoggerUtil.GetNLogger(typeof 
                    (ParameterCheckingCodeIssueComputer));

                public ParameterCheckingCodeIssueComputer(SyntaxNode declaration,
                    IEnumerable<Tuple<string, string>> typeNameTuples, 
                        RefactoringMetaData metaData) : base(metaData)
                {
                    this.declaration = declaration;
                    this.typeNameTuples = typeNameTuples;
                    this.methodNameComparer = new MethodNameComparer();
                }

                public override bool IsIssueResolved(ICorrectRefactoringResult correctRefactoringResult)
                {
                    var single = correctRefactoringResult as ISingleDocumentResult;
                    var refactoring = correctRefactoringResult.refactoring as IManualExtractMethodRefactoring;
                    if(single != null && single.GetDocumentId() == GetDocumentId() && refactoring != null)
                    {
                        if (correctRefactoringResult.RefactoringConditionType == RefactoringConditionType.
                            EXTRACT_METHOD_PARAMETER)
                        {
                            return methodNameComparer.Compare(declaration, refactoring.
                                ExtractedMethodDeclaration) == 0;
                        }
                    }
                    return false;
                }

                public override IEnumerable<SyntaxNode> GetPossibleSyntaxNodes(IDocument document)
                {
                    return ((SyntaxNode)document.GetSyntaxRoot()).DescendantNodes(n => n.Kind != 
                        SyntaxKind.MethodDeclaration).OfType<MethodDeclarationSyntax>().Select(m => m.
                            ParameterList);
                }

                public override IEnumerable<CodeIssue> ComputeCodeIssues(IDocument document, SyntaxNode node)
                {
                    if(node.Kind == SyntaxKind.ParameterList)
                    {
                        var method = ConditionCheckersUtils.TryGetOutsideMethod(node);
                        if(method != null && methodNameComparer.Compare(method, declaration) == 0)
                        {
                            return typeNameTuples.Select(t => GetMissingParameterIssue(document, method, node, 
                                t));
                        }
                    }
                    return Enumerable.Empty<CodeIssue>();
                }


                private CodeIssue GetMissingParameterIssue(IDocument document, SyntaxNode method, 
                    SyntaxNode node, Tuple<string, string> typeNameTuple)
                {
                    if (GhostFactorComponents.configurationComponent.SupportQuickFix
                        (RefactoringConditionType.EXTRACT_METHOD_PARAMETER))
                    {
                        return new CodeIssue(CodeIssue.Severity.Error, node.Span, GetErrorDescription
                            (typeNameTuple), new ICodeAction[] {new AddParamterCodeAction(document, method, 
                                typeNameTuple, this)});
                    }
                    return new CodeIssue(CodeIssue.Severity.Error, node.Span, GetErrorDescription
                        (typeNameTuple));
                }

                
                private string GetErrorDescription(Tuple<string, string> typeNameTuple)
                {
                    return "Extracted method needs parameter: " + typeNameTuple.Item1 + " " + typeNameTuple.
                        Item2;
                }


                public override bool Equals(ICodeIssueComputer o)
                {
                    if (IsIssuedToSameDocument(o))
                    {
                        var another = o as ParameterCheckingCodeIssueComputer;

                        // If the other is not in the same RefactoringType, return false
                        if (another != null)
                        {
                            var other = (ParameterCheckingCodeIssueComputer) o;
                            var methodsComparator = new MethodNameComparer();

                            // If the method declarations are equal to each other, compare the missing 
                            // parameters.
                            if(methodsComparator.Compare(declaration, other.declaration) == 0)
                            {
                                return ConditionCheckersUtils.AreStringTuplesSame(typeNameTuples, 
                                    other.typeNameTuples);
                            }
                        }
                    }
                    return false;
                }

                public bool IsUpdatedComputer(IUpdatableCodeIssueComputer o)
                {
                    var other = o as ParameterCheckingCodeIssueComputer;
                    if (other != null && other.GetDocumentId() == GetDocumentId())
                    {
                        if (methodNameComparer.Compare(declaration, other.declaration) == 0)
                        {
                            return !ConditionCheckersUtils.AreStringTuplesSame(typeNameTuples,
                                other.typeNameTuples);
                        }
                    }
                    return false;
                }

                /// <summary>
                /// Code action for adding a single one parameter.
                /// </summary>
                private class AddParamterCodeAction : ICodeAction
                {
                    private readonly Tuple<string, string> typeNameTuple;
                    private readonly SyntaxNode declaration;
                    private readonly IDocument document;
                    private readonly ICodeIssueComputer computer;

                    internal AddParamterCodeAction(IDocument document, SyntaxNode declaration, 
                        Tuple<string, string> typeNameTuple, ICodeIssueComputer computer)
                    {
                        this.document = document;
                        this.typeNameTuple = typeNameTuple;
                        this.declaration = declaration;
                        this.computer = computer;
                    }

                    public CodeActionEdit GetEdit(CancellationToken cancellationToken = new 
                        CancellationToken())
                    {
                        var updatedDocument = updateMethodDeclaration(document);
                        updatedDocument = updateMethodInvocations(updatedDocument);
                        var updatedSolution = document.Project.Solution.UpdateDocument(updatedDocument);
                        var edit = new CodeActionEdit(null, updatedSolution, 
                            ConditionCheckersUtils.GetRemoveCodeIssueComputerOperation(computer));
                        return edit;
                    }

                    public ImageSource Icon
                    {
                        get { return ResourcePool.GetIcon(); }
                    }

                    public string Description
                    {
                        get { return "Add paramters " + typeNameTuple.Item2; }
                    }

                    private IDocument updateMethodDeclaration(IDocument document)
                    {
                        // Get the simplified name of the method
                        var methodName = ((MethodDeclarationSyntax) declaration).Identifier.ValueText;
                        var documentAnalyzer = AnalyzerFactory.GetDocumentAnalyzer();
                        documentAnalyzer.SetDocument(document);
                      
                        // Get the root of the current document.
                        var root = ((SyntaxNode) document.GetSyntaxRoot());

                        // Find the method
                        SyntaxNode method = root.DescendantNodes().Where(
                            // Find all the method declarations.
                            n => n.Kind == SyntaxKind.MethodDeclaration).
                            // Convert all of them to the RefactoringType MethodDeclarationSyntax.
                            Select(n => (MethodDeclarationSyntax) n).
                            // Get the one whose name is same with the given method declaration.
                            First(m => m.Identifier.ValueText.Equals(methodName));

                        // If we can find this method.
                        if (method != null)
                        {
                            // Get the updated method declaration.
                            var methodAnalyzer = AnalyzerFactory.GetMethodDeclarationAnalyzer();
                            methodAnalyzer.SetMethodDeclaration(method);
                            var updatedMethod = methodAnalyzer.AddParameters(new[] {typeNameTuple});

                            // Update the root, document and finally return the code action.
                            var updatedRoot = new MethodDeclarationRewriter(method, updatedMethod).
                                Visit(root);
                            return document.UpdateSyntaxRoot(updatedRoot);
                        }
                        return document;
                    }

                    /// <summary>
                    /// Sytnax writer to change a method to an updated one.
                    /// </summary>
                    private class MethodDeclarationRewriter : SyntaxRewriter
                    {
                        private readonly SyntaxNode originalMethod;
                        private readonly SyntaxNode updatedMethod;
                        private readonly IComparer<SyntaxNode> methodNameComparer; 

                        internal MethodDeclarationRewriter(SyntaxNode originalMethod, SyntaxNode 
                            updatedMethod)
                        {
                            this.originalMethod = originalMethod;
                            this.updatedMethod = updatedMethod;
                            this.methodNameComparer = new MethodNameComparer();
                        }

                        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
                        {
                            // If the visited method has the same name with the target extracted method.
                            if (methodNameComparer.Compare(node, originalMethod) == 0)
                            {
                                return updatedMethod;
                            }
                            return node;
                        }
                    }

                    /// <summary>
                    /// Update all the method invocations in the solution. 
                    /// </summary>
                    /// <param name="document"></param>
                    /// <returns></returns>
                    private IDocument updateMethodInvocations(IDocument document)
                    {
                        // Get the retriever for method invocations.
                        var retriever = RetrieverFactory.GetMethodInvocationRetriever();
                        retriever.SetMethodDeclaration(declaration);
             
                        // Get all the invocations in the document for the given method
                        // declaration.
                        retriever.SetDocument(document);
                        var invocations = retriever.GetInvocations();

                        // If there are invocations in the document.
                        if (invocations.Any())
                        {
                            // Update root
                            var root = (SyntaxNode) document.GetSyntaxRoot();
                            var updatedRoot = new InvocationsAddArgumentsRewriter(invocations, 
                                typeNameTuple.Item2).Visit(root);

                            // Update solution by update the document.
                            document = document.UpdateSyntaxRoot(updatedRoot);
                        }
                        return document;
                    }

                    /// <summary>
                    /// Syntax rewriter for adding arguments to given method invocations.
                    /// </summary>
                    private class InvocationsAddArgumentsRewriter : SyntaxRewriter
                    {
                        private readonly string addedArgument;
                        private readonly IEnumerable<SyntaxNode> invocations;
                        private readonly IMethodInvocationAnalyzer analyzer;

                        internal InvocationsAddArgumentsRewriter(IEnumerable<SyntaxNode> invocations,
                            string addedArgument)
                        {
                            this.invocations = invocations;
                            this.addedArgument = addedArgument;
                            this.analyzer = AnalyzerFactory.GetMethodInvocationAnalyzer();
                        }

                        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
                        {
                            if (invocations.Any(i => ASTUtil.AreSyntaxNodesSame(i, node)))
                            {
                                analyzer.SetMethodInvocation(node);
                                return analyzer.AddArguments(new[] {addedArgument});
                            }
                            return node;
                        }
                    }
                }

                public override RefactoringType RefactoringType
                {
                    get { return RefactoringType.EXTRACT_METHOD; }
                }

                public override RefactoringConditionType RefactoringConditionType
                {
                    get { return RefactoringConditionType.EXTRACT_METHOD_PARAMETER; }
                }
            }
        }
    }
}
