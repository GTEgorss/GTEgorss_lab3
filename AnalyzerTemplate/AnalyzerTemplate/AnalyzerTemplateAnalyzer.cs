using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AnalyzerTemplate
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AnalyzerTemplateAnalyzer : DiagnosticAnalyzer
    {
        public const string BoolDiagnosticId = "BoolAnalyzer";
        private static readonly LocalizableString BoolTitle = new LocalizableResourceString(nameof(BoolResources.AnalyzerTitle), BoolResources.ResourceManager, typeof(BoolResources));
        private static readonly LocalizableString BoolMessageFormat = new LocalizableResourceString(nameof(BoolResources.AnalyzerMessageFormat), BoolResources.ResourceManager, typeof(BoolResources));
        private static readonly LocalizableString BoolDescription = new LocalizableResourceString(nameof(BoolResources.AnalyzerDescription), BoolResources.ResourceManager, typeof(BoolResources));
        private const string BoolCategory = "Naming";

        private static readonly DiagnosticDescriptor BoolRule = new DiagnosticDescriptor(BoolDiagnosticId, BoolTitle, BoolMessageFormat, BoolCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: BoolDescription);

        public const string ToStringDiagnosticId = "ToStringAnalyzer";
        private static readonly LocalizableString ToStringTitle = new LocalizableResourceString(nameof(ToStringResources.AnalyzerTitle), ToStringResources.ResourceManager, typeof(ToStringResources));
        private static readonly LocalizableString ToStringMessageFormat = new LocalizableResourceString(nameof(ToStringResources.AnalyzerMessageFormat), ToStringResources.ResourceManager, typeof(ToStringResources));
        private static readonly LocalizableString ToStringDescription = new LocalizableResourceString(nameof(ToStringResources.AnalyzerDescription), ToStringResources.ResourceManager, typeof(ToStringResources));
        private const string ToStringCategory = "CodeStyle";

        private static readonly DiagnosticDescriptor ToStringRule = new DiagnosticDescriptor(ToStringDiagnosticId, ToStringTitle, ToStringMessageFormat, ToStringCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ToStringDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(BoolRule, ToStringRule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeBooleansWithNot, SyntaxKind.VariableDeclaration);

            context.RegisterSyntaxNodeAction(AnalyzeToStringCalls, SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeBooleansWithNot(SyntaxNodeAnalysisContext context)
        {
            var declarationExpr = (VariableDeclarationSyntax)context.Node;

            string variableName = declarationExpr.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault().Identifier.ValueText;

            if (declarationExpr.DescendantNodes().OfType<PredefinedTypeSyntax>().FirstOrDefault() != null && 
                declarationExpr.DescendantNodes().OfType<PredefinedTypeSyntax>().FirstOrDefault().Keyword.Kind() == SyntaxKind.BoolKeyword) {
                if ((variableName[0] == 'n' || variableName[0] == 'N') && (variableName[1] == 'o' || variableName[1] == 'O') && (variableName[2] == 't' || variableName[2] == 'T'))
                {
                    var diagnostic = Diagnostic.Create(BoolRule, declarationExpr.GetLocation(), variableName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void AnalyzeToStringCalls(SyntaxNodeAnalysisContext context)
        {
            var toStringInvocation = (MemberAccessExpressionSyntax)context.Node;

            if (toStringInvocation.Name.Identifier.ValueText == "ToString") {
                var invocationIdentifier = toStringInvocation.DescendantNodes().OfType<IdentifierNameSyntax>().First().Identifier.ValueText;

                var invocationType = toStringInvocation.Ancestors().SelectMany(a => a.DescendantNodes().OfType<VariableDeclarationSyntax>())
                    .FirstOrDefault(d => d.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault().Identifier.ValueText == invocationIdentifier)
                    .DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault()
                    .Initializer.Value.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault().Identifier.ValueText;

                var invocationTypeNode = toStringInvocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault(a => a.Identifier.ValueText == invocationType);

                if (!HasToString(invocationTypeNode)) {
                    var parentClass = GetClassParent(invocationTypeNode);

                    while (parentClass != null)
                    {
                        if (parentClass != null && HasToString(parentClass))
                        {
                            return;
                        }

                        parentClass = GetClassParent(parentClass);
                    }

                    var diagnostic = Diagnostic.Create(ToStringRule, toStringInvocation.GetLocation(), invocationIdentifier);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static ClassDeclarationSyntax GetClassParent(SyntaxNode classNode)
        {
            var baseType = classNode.DescendantNodes().OfType<SimpleBaseTypeSyntax>().FirstOrDefault();

            if (baseType == null)
            {
                return null;
            }

            var parentName = baseType.DescendantNodes().OfType<IdentifierNameSyntax>().First().Identifier.ValueText;

            if (parentName.ToLower() == "object")
            {
                return null;
            }

            var parent = classNode.Ancestors().SelectMany(a => a.DescendantNodes()).OfType<ClassDeclarationSyntax>().FirstOrDefault(d => d.Identifier.ValueText == parentName);

            return parent;
        }

        private static bool HasToString(SyntaxNode classNode)
        {
            return classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == "ToString") != null;
        }
    }
}
