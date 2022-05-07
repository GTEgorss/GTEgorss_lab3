using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AnalyzerTemplate
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AnalyzerTemplateCodeFixProvider)), Shared]
    public class AnalyzerTemplateCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(AnalyzerTemplateAnalyzer.BoolDiagnosticId, AnalyzerTemplateAnalyzer.ToStringDiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();

            if (diagnostic.Id == "BoolAnalyzer")
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().First();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: BoolCodeFixResources.CodeFixTitle,
                        createChangedDocument: c => MakeWithoutNotAsync(context.Document, declaration, c),
                        equivalenceKey: nameof(BoolCodeFixResources.CodeFixTitle)),
                    diagnostic);
            } 
            
            if (diagnostic.Id == "ToStringAnalyzer")
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                var toStringInvocation = root.FindToken(diagnosticSpan.Start).Parent.Ancestors().Where(a => a.DescendantNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault() != null).FirstOrDefault()
                    .DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: ToStringCodeFixResources.CodeFixTitle,
                        createChangedDocument: c => OverrideToString(context.Document, toStringInvocation, c),
                        equivalenceKey: nameof(ToStringCodeFixResources.CodeFixTitle)),
                    diagnostic);
            }
        }

        private async Task<Document> MakeWithoutNotAsync(Document document, VariableDeclarationSyntax declarationExpr, CancellationToken cancellationToken)
        {
            string variableName = declarationExpr.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault().Identifier.ValueText;

            var newDeclarationExpr = SyntaxFactory.VariableDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
                SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(variableName.Substring(3)))
                .WithInitializer(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.LiteralExpression(
                        declarationExpr.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                        .Initializer.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault().Kind())))));

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(declarationExpr, newDeclarationExpr);

            int usagesSize = newRoot.DescendantTokens().Where(t => t.ValueText == variableName).ToList().Count;

            for (int i = usagesSize - 1; i >= 0; --i)
            {
                var usage = newRoot.DescendantTokens().FirstOrDefault(t => t.ValueText == variableName);
                newRoot = newRoot.ReplaceToken(usage, SyntaxFactory.Identifier(new System.Text.StringBuilder().Append("!").Append(variableName.Substring(3)).ToString()));
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> OverrideToString(Document document, MemberAccessExpressionSyntax toStringInvocation, CancellationToken cancellationToken)
        {
            var invocationIdentifier = toStringInvocation.DescendantNodes().OfType<IdentifierNameSyntax>().First().Identifier.ValueText;

            var invocationType = toStringInvocation.Ancestors().SelectMany(a => a.DescendantNodes().OfType<VariableDeclarationSyntax>())
                .FirstOrDefault(d => d.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault().Identifier.ValueText == invocationIdentifier)
                .DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                .Initializer.Value.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault().Identifier.ValueText;

            var invocationTypeNode = toStringInvocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault(a => a.Identifier.ValueText == invocationType);

            var fields = invocationTypeNode.DescendantNodes().OfType<FieldDeclarationSyntax>().Select(f => f.Declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>().First().Identifier.ValueText).ToList();

            var buildLiteral = new StringBuilder();
            buildLiteral.Append("\"");
            int count = 0;
            fields.ForEach(f =>
            {
                buildLiteral.Append(f).Append(": {").Append(f).Append("}");
                ++count;
                if (count < fields.Count) buildLiteral.Append("\\n");
            });
            buildLiteral.Append("\"");

            var toStringMethod = SyntaxFactory.MethodDeclaration(
                    attributeLists: default(SyntaxList<AttributeListSyntax>),
                    modifiers: SyntaxFactory.TokenList(new[]{
                                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                                            SyntaxFactory.Token(SyntaxKind.OverrideKeyword)}),
                    returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                    explicitInterfaceSpecifier: null,
                    identifier: SyntaxFactory.Identifier("ToString"),
                    typeParameterList: null,
                    parameterList: SyntaxFactory.ParameterList(),
                    constraintClauses: default(SyntaxList<TypeParameterConstraintClauseSyntax>),
                    body: SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("$" + buildLiteral.ToString())))),
                    expressionBody: null);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(invocationTypeNode, invocationTypeNode.AddMembers(toStringMethod));

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
