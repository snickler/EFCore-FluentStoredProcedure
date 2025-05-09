using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading; // Added for CancellationToken
using System.Diagnostics; // Force enable diagnostics
// using System.Diagnostics; // For Debugger - keep commented out unless actively debugging generator

namespace Snickler.EFCore.SourceGenerators
{
    [Generator]
    public class SprocResultsGenerator : IIncrementalGenerator
    {
        private const string SprocResultsTypeName = "Snickler.EFCore.EFExtensions.SprocResults";
        private const string SprocResultsMetadataName = "Snickler.EFCore.EFExtensions+SprocResults";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // #if DEBUG
            // if (!System.Diagnostics.Debugger.IsAttached)
            // {
            //     System.Diagnostics.Debugger.Launch();
            // }
            // #endif 
            // Debugger.Launch(); // Uncomment to debug generator

            IncrementalValuesProvider<InvocationExpressionSyntax> invocationSyntaxes = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)!;

            IncrementalValueProvider<(Compilation, ImmutableArray<InvocationExpressionSyntax>)> compilationAndInvocations
                = context.CompilationProvider.Combine(invocationSyntaxes.Collect());

            context.RegisterSourceOutput(compilationAndInvocations,
                static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        {
            var isTarget = node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } &&
                           (memberAccess.Name.Identifier.ValueText == "ReadToList" || memberAccess.Name.Identifier.ValueText == "ReadToValueTupleList" ||
                            memberAccess.Name.Identifier.ValueText == "ReadToList_Dummy" || memberAccess.Name.Identifier.ValueText == "ReadToValueTupleList_Dummy");
            
            // SGLog: Log node being checked by IsSyntaxTargetForGeneration (Line 51 area)
            // System.Console.WriteLine($"SG_LOG: IsSyntaxTargetForGeneration checking node: {node.GetType().Name} - {node.ToFullString().Replace("\n", " ").Replace("\r", " ").Substring(0, System.Math.Min(100, node.ToFullString().Length))}, IsTarget: {isTarget}");
            return isTarget;
        }


        static InvocationExpressionSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var invocationSyntax = (InvocationExpressionSyntax)context.Node;
            // SGLog: Log node being processed by GetSemanticTargetForGeneration (Line 60 area)
            // System.Console.WriteLine($"SG_LOG: GetSemanticTargetForGeneration processing node: {invocationSyntax.ToFullString().Replace("\n", " ").Replace("\r", " ").Substring(0, System.Math.Min(100, invocationSyntax.ToFullString().Length))}");

            if (invocationSyntax.Expression is MemberAccessExpressionSyntax memberAccessSyntax) 
            {
                SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(invocationSyntax);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
                {
                    var sprocResultsTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(SprocResultsMetadataName);
                    bool isCorrectType = sprocResultsTypeSymbol != null && 
                                         methodSymbol.ContainingType != null &&
                                         SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType.OriginalDefinition, sprocResultsTypeSymbol.OriginalDefinition);
                    
                    // SGLog: Log type comparison result (Line 73 area)
                    // System.Console.WriteLine($"SG_LOG: GetSemanticTargetForGeneration - Method: {methodSymbol.Name}, ContainingType: {methodSymbol.ContainingType?.ToDisplayString()}, TargetType: {sprocResultsTypeSymbol?.ToDisplayString()}, IsCorrectType: {isCorrectType}");

                    if (isCorrectType)
                    {
                        return invocationSyntax;
                    }
                }
            }
            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<InvocationExpressionSyntax> invocations, SourceProductionContext context)
        {
            try
            {
                if (invocations.IsDefaultOrEmpty)
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG002", "Generator Info", "No relevant invocation syntaxes found.", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));
                    return;
                }

                var distinctInvocationSyntaxes = invocations.Distinct(SyntaxNodeComparer.Instance);
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG003", "Generator Info", $"Found {distinctInvocationSyntaxes.Count()} distinct relevant invocation syntax nodes.", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));

                var typesForReadToList = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var typesForReadToValueTupleList = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

                var sprocResultsSymbol = compilation.GetTypeByMetadataName(SprocResultsMetadataName);
                if (sprocResultsSymbol == null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG004", "Generator Error", $"Could not find SprocResults type using metadata name: {SprocResultsMetadataName}", "SourceGenerator", DiagnosticSeverity.Error, true), Location.None));
                    return;
                }
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG005", "Generator Info", $"Found SprocResults symbol: {sprocResultsSymbol.ToDisplayString()}", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));

                foreach (var invocationSyntax in distinctInvocationSyntaxes)
                {
                    var semanticModel = compilation.GetSemanticModel(invocationSyntax.SyntaxTree);
                    SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocationSyntax);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
                        methodSymbol.IsGenericMethod &&
                        SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, sprocResultsSymbol))
                    {
                        var typeArg = methodSymbol.TypeArguments.FirstOrDefault();
                        if (typeArg == null || typeArg.TypeKind == TypeKind.Error)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG011", "Generator Warning", $"Invocation {invocationSyntax.ToFullString()} has null or error type argument.", "SourceGenerator", DiagnosticSeverity.Warning, true), invocationSyntax.GetLocation()));
                            continue;
                        }
                        
                        string methodNameToCategorize = methodSymbol.Name;

                        if (methodNameToCategorize == "ReadToList" || methodNameToCategorize == "ReadToList_Dummy")
                        {
                            if (typesForReadToList.Add(typeArg))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG006", "Generator Info", $"Detected {methodNameToCategorize} usage with POCO type: {typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}", "SourceGenerator", DiagnosticSeverity.Info, true), invocationSyntax.GetLocation()));
                            }
                        }
                        else if (methodNameToCategorize == "ReadToValueTupleList" || methodNameToCategorize == "ReadToValueTupleList_Dummy")
                        {
                            if (typeArg is INamedTypeSymbol ntSymbol)
                            {
                                bool isCompilerTuple = ntSymbol.IsTupleType; // True for (int, string)
                                bool isGenericValueTuple = ntSymbol.ContainingNamespace?.ToDisplayString() == "System" &&
                                                           ntSymbol.Name.StartsWith("ValueTuple") &&
                                                           !ntSymbol.Name.Equals("ValueTuple") && // Exclude non-generic System.ValueTuple itself
                                                           ntSymbol.TypeArguments.Any(); // Must be generic

                                if (isCompilerTuple || isGenericValueTuple)
                                {
                                    if (typesForReadToValueTupleList.Add(ntSymbol))
                                    {
                                        context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG007", "Generator Info", $"Detected {methodNameToCategorize} usage with valid ValueTuple type: {ntSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}", "SourceGenerator", DiagnosticSeverity.Info, true), invocationSyntax.GetLocation()));
                                    }
                                }
                                else
                                {
                                    // Log skipped System.Tuple or non-generic System.ValueTuple, or other unsuitable types
                                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG018", "Generator Info", $"Skipped adding unsuitable type to ValueTuple list: {ntSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} for method {methodNameToCategorize}", "SourceGenerator", DiagnosticSeverity.Info, true), invocationSyntax.GetLocation()));
                                }
                            }
                        }
                    }
                }

                if (!typesForReadToList.Any() && !typesForReadToValueTupleList.Any())
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG008", "Generator Info", "No unique types found for generation. No code generated.", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG009", "Generator Info", $"Generating for {typesForReadToList.Count} POCOs and {typesForReadToValueTupleList.Count} ValueTuples.", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));

                StringBuilder sourceBuilder = new StringBuilder();
                GenerateSourceFileHeader(sourceBuilder);

                if (typesForReadToList.Any())
                {
                    GenerateDispatcherMethod(sourceBuilder, typesForReadToList, "ReadToList", "T", "new()");
                    foreach (var typeSymbol in typesForReadToList) { GeneratePocoMapperMethod(sourceBuilder, typeSymbol, context); }
                }

                if (typesForReadToValueTupleList.Any())
                {
                    GenerateDispatcherMethod(sourceBuilder, typesForReadToValueTupleList, "ReadToValueTupleList", "TValueTuple", "struct");
                    foreach (var typeSymbol in typesForReadToValueTupleList) 
                    {
                        // The list should now be pre-filtered, but an extra check in GenerateValueTupleMapperMethod is good.
                        // No need for the complex 'if' here anymore if the list is correctly populated.
                        GenerateValueTupleMapperMethod(sourceBuilder, typeSymbol, context); 
                    }
                }

                GenerateSourceFileFooter(sourceBuilder);
                context.AddSource("SprocResults.Generated.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG010", "Generator Info", "SprocResults.Generated.cs successfully added to compilation.", "SourceGenerator", DiagnosticSeverity.Info, true), Location.None));
            }
            catch (System.Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG998", "Generator Exception", $"SprocResultsGenerator encountered an exception: {ex.GetType().Name} - {ex.Message} {ex.StackTrace}", "SourceGenerator", DiagnosticSeverity.Error, true), Location.None));
                // Optionally, rethrow if you want to halt the build, but for diagnostics, just reporting might be enough.
                // throw; 
            }
        }

        private static void GenerateSourceFileHeader(StringBuilder sb)
        {
            sb.AppendLine("/// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data.Common;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            sb.AppendLine();
            sb.AppendLine("namespace Snickler.EFCore");
            sb.AppendLine("{");
            sb.AppendLine("    public static partial class EFExtensions");
            sb.AppendLine("    {");
            sb.AppendLine("        public partial class SprocResults");
            sb.AppendLine("        {");
        }

        private static void GenerateDispatcherMethod(StringBuilder sb, IEnumerable<ITypeSymbol> types, string methodName, string typeParamName, string typeParamConstraint)
        {
            sb.AppendLine($"            private IList<{typeParamName}> Generated{methodName}Dispatch<{typeParamName}>() where {typeParamName} : {typeParamConstraint}");
            sb.AppendLine("            {");
            sb.AppendLine($"                var typeOf{typeParamName} = typeof({typeParamName});");
            foreach (var typeSymbol in types)
            {
                // For ValueTuples, ensure it's a valid tuple type before generating dispatch entry
                if (methodName == "ReadToValueTupleList" && !(typeSymbol is INamedTypeSymbol ntSymbol && (ntSymbol.IsTupleType || (ntSymbol.ContainingNamespace?.ToDisplayString() == "System" && ntSymbol.Name.StartsWith("ValueTuple")))))
                {
                    continue; 
                }

                var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var safeMethodNamePart = GetSafeMethodNamePart(typeSymbol);
                var mapMethodName = methodName == "ReadToList" ? $"GeneratedMap_{safeMethodNamePart}" : $"GeneratedMapValueTuple_{safeMethodNamePart}";
                sb.AppendLine($"                if (typeOf{typeParamName} == typeof({fullTypeName})) return (IList<{typeParamName}>){mapMethodName}(_reader);");
            }
            sb.AppendLine($"                throw new InvalidOperationException($\"No source-generated SprocResults mapper for {{typeOf{typeParamName}.FullName}}. Ensure it is invoked via a ReadToList/ReadToValueTupleList call (or their _Dummy counterparts) in the main library, the type is accessible, and (for tuples) it is a valid ValueTuple type.\");");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        private static void GenerateSourceFileFooter(StringBuilder sb)
        {
            sb.AppendLine("        } // End partial class SprocResults");
            sb.AppendLine("    } // End partial class EFExtensions");
            sb.AppendLine("}");
        }

        private static string GetSafeIdentifier(string name)
        {
             // Simplified and made more robust
            StringBuilder safeName = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    safeName.Append(c);
                }
                else
                {
                    safeName.Append('_'); // Replace non-alphanumeric with underscore
                }
            }
            // Prepend with underscore if it starts with a digit or is a keyword
            if (safeName.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(safeName[0]))
            {
                safeName.Insert(0, '_');
            }
            // return SyntaxFacts.IsReservedKeyword(safeName.ToString()) ? "@" + safeName : safeName.ToString();
            return SyntaxFacts.GetKeywordKind(safeName.ToString()) != SyntaxKind.None ? "_" + safeName : safeName.ToString();
        }

        private static string GetSafeMethodNamePart(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType && (namedType.IsTupleType || (namedType.ContainingNamespace?.ToDisplayString() == "System" && namedType.Name.StartsWith("ValueTuple"))))
            {
                var elementTypesForName = namedType.IsTupleType ? namedType.TupleElements.Select(te => te.Type) : namedType.TypeArguments;
                var elementSafeNames = elementTypesForName.Select(et => GetSafeIdentifier(et.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))).ToList();

                if (!elementSafeNames.Any() || elementSafeNames.All(string.IsNullOrEmpty))
                {
                    return "UnnamedTupleStructure"; // Provide a non-empty placeholder
                }
                // Filter out any potentially empty strings from elementSafeNames before joining
                var validElementSafeNames = elementSafeNames.Where(s => !string.IsNullOrEmpty(s));
                return validElementSafeNames.Any() ? string.Join("_", validElementSafeNames) : "UnnamedTupleStructure";
            }
            return GetSafeIdentifier(typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        private static void GeneratePocoMapperMethod(StringBuilder sb, ITypeSymbol pocoType, SourceProductionContext context)
        {
            var fullTypeName = pocoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var safeMethodNamePart = GetSafeMethodNamePart(pocoType);
            sb.AppendLine($"            private static List<{fullTypeName}> GeneratedMap_{safeMethodNamePart}(DbDataReader dr)");
            sb.AppendLine("            {");
            sb.AppendLine($"                var objList = new List<{fullTypeName}>();");
            sb.AppendLine("                if (!dr.HasRows) return objList;");
            sb.AppendLine();
            var properties = pocoType.GetMembers().OfType<IPropertySymbol>()
                                     .Where(p => p.SetMethod != null && p.DeclaredAccessibility == Accessibility.Public).ToList();

            sb.AppendLine("                var columnSchema = dr.GetColumnSchema();");
            sb.AppendLine("                var colNameOrdinalMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);");
            sb.AppendLine("                for(int i = 0; i < columnSchema.Count; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                     int ordinal = columnSchema[i].ColumnOrdinal ?? i;");
            sb.AppendLine("                     if (!string.IsNullOrEmpty(columnSchema[i].ColumnName)) colNameOrdinalMap[columnSchema[i].ColumnName] = ordinal;");
            sb.AppendLine("                }");
            sb.AppendLine();

            foreach (var prop in properties)
            {
                sb.AppendLine($"                string {prop.Name}_columnNameForLookup = \"{prop.Name}\";");
                var columnAttribute = prop.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "ColumnAttribute" && ad.AttributeClass.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema");
                if (columnAttribute != null && columnAttribute.ConstructorArguments.Any())
                {
                    var arg = columnAttribute.ConstructorArguments[0];
                    if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string attrColName && !string.IsNullOrEmpty(attrColName))
                    {
                        sb.AppendLine($"                {prop.Name}_columnNameForLookup = \"{attrColName}\";");
                    }
                }
                sb.AppendLine($"                if (!colNameOrdinalMap.TryGetValue({prop.Name}_columnNameForLookup, out int {prop.Name}_ordinal)) {prop.Name}_ordinal = -1;");
            }
            sb.AppendLine();
            sb.AppendLine("                while (dr.Read())");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var obj = new {fullTypeName}();");
            foreach (var prop in properties)
            {
                var propTypeFullName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var underlyingTypeSymbol = prop.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                                        ? ((INamedTypeSymbol)prop.Type).TypeArguments[0]
                                        : prop.Type;
                var underlyingTypeName = underlyingTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine($"                    if ({prop.Name}_ordinal != -1 && !dr.IsDBNull({prop.Name}_ordinal))");
                sb.AppendLine("                    {");
                bool isDateOnly = underlyingTypeName == "global::System.DateOnly" || underlyingTypeName == "System.DateOnly";
                bool isTimeOnly = underlyingTypeName == "global::System.TimeOnly" || underlyingTypeName == "System.TimeOnly";

                if (isDateOnly)
                {
                    sb.AppendLine($"                        var val_{prop.Name} = dr.GetValue({prop.Name}_ordinal);");
                    sb.AppendLine($"                        if (val_{prop.Name} is System.DateTime dt_{prop.Name}) obj.{prop.Name} = System.DateOnly.FromDateTime(dt_{prop.Name});");
                    sb.AppendLine($"                        else if (val_{prop.Name} is System.DateOnly d_{prop.Name}) obj.{prop.Name} = d_{prop.Name};");
                }
                else if (isTimeOnly)
                {
                    sb.AppendLine($"                        var val_{prop.Name} = dr.GetValue({prop.Name}_ordinal);");
                    sb.AppendLine($"                        if (val_{prop.Name} is System.DateTime dt_{prop.Name}) obj.{prop.Name} = System.TimeOnly.FromDateTime(dt_{prop.Name});");
                    sb.AppendLine($"                        else if (val_{prop.Name} is System.TimeSpan ts_{prop.Name}) obj.{prop.Name} = System.TimeOnly.FromTimeSpan(ts_{prop.Name});");
                    sb.AppendLine($"                        else if (val_{prop.Name} is System.TimeOnly t_{prop.Name}) obj.{prop.Name} = t_{prop.Name};");
                }
                else
                {
                    sb.AppendLine($"                        obj.{prop.Name} = dr.GetFieldValue<{propTypeFullName}>({prop.Name}_ordinal);");
                }
                sb.AppendLine("                    }");
                sb.AppendLine($"                    else if ({prop.Name}_ordinal != -1) // DBNull case");
                sb.AppendLine("                    {");
                if (prop.Type.IsReferenceType || prop.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    // Use the null-forgiving operator (!) for reference types to handle nullability warnings
                    sb.AppendLine($"                        obj.{prop.Name} = default!; // default for nullable types is null, use ! to handle nullability warnings");
                }
                sb.AppendLine("                    }");
            }
            sb.AppendLine("                    objList.Add(obj);");
            sb.AppendLine("                }");
            sb.AppendLine("                return objList;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        private static void GenerateValueTupleMapperMethod(StringBuilder sb, ITypeSymbol tupleTypeSymbol, SourceProductionContext context)
        {
            // Ensure it's a tuple-like INamedTypeSymbol we can work with
            if (tupleTypeSymbol is not INamedTypeSymbol namedTupleType)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("SG017A", "Generator Error", $"GenerateValueTupleMapperMethod called with non-INamedTypeSymbol: {tupleTypeSymbol.ToDisplayString()}", "SourceGenerator", DiagnosticSeverity.Error, true), Location.None));
                return;
            }

            bool isCompilerTuple = namedTupleType.IsTupleType;
            bool isGenericValueTupleItself = namedTupleType.ContainingNamespace?.ToDisplayString() == "System" &&
                                          namedTupleType.Name.StartsWith("ValueTuple") &&
                                          !namedTupleType.Name.Equals("ValueTuple") && 
                                          namedTupleType.TypeArguments.Any();

            if (!isCompilerTuple && !isGenericValueTupleItself)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("SG017B", "Generator Warning", $"GenerateValueTupleMapperMethod called with unsuitable type (not a valid ValueTuple or compiler tuple): {namedTupleType.ToDisplayString()}. Skipping.", "SourceGenerator", DiagnosticSeverity.Warning, true),
                    Location.None));
                return;
            }
            
            var fullTupleTypeName = namedTupleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var safeMethodNamePart = GetSafeMethodNamePart(namedTupleType);

            if (string.IsNullOrEmpty(fullTupleTypeName) || fullTupleTypeName == "global::System.ValueTuple" || 
                safeMethodNamePart == "UnnamedTupleStructure" || string.IsNullOrEmpty(safeMethodNamePart))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("SG020", "Generator Error", $"Invalid type or name for ValueTuple mapping: FullName='{fullTupleTypeName}', SafePart='{safeMethodNamePart}'. Skipping for type {namedTupleType.ToDisplayString()}.", "SourceGenerator", DiagnosticSeverity.Error, true),
                    Location.None)); 
                return; 
            }

            // At this point, namedTupleType should be a valid type like (int, string) or System.ValueTuple<T1, T2>
            // Get element types, either from TupleElements (if IsTupleType) or TypeArguments (for ValueTuple<T...>)
            ImmutableArray<ITypeSymbol> elementTypes = namedTupleType.IsTupleType 
                ? namedTupleType.TupleElements.Select(f => f.Type).ToImmutableArray() 
                : namedTupleType.TypeArguments;

            if (!elementTypes.Any()) return;

            string methodSignature = $"            private static List<{fullTupleTypeName}> GeneratedMapValueTuple_{safeMethodNamePart}(DbDataReader dr)";
            sb.AppendLine(methodSignature);
            sb.AppendLine("            {");
            sb.AppendLine($"                var resultList = new List<{fullTupleTypeName}>();");
            sb.AppendLine("                if (!dr.HasRows) return resultList;");
            sb.AppendLine();
            sb.AppendLine("                int fieldCount = dr.FieldCount;");
            int tupleArity = elementTypes.Length; // Use length of elementTypes

            sb.AppendLine($"                if ({tupleArity} > fieldCount) {{ return resultList; }}");
            sb.AppendLine();

            sb.AppendLine("                while (dr.Read())");
            sb.AppendLine("                {");

            var constructorArgs = new List<string>();
            for (int i = 0; i < tupleArity; i++)
            {
                var elementType = elementTypes[i]; // Directly use the type from the list
                var elementFullTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var varName = $"item{i}";

                if (elementType.TypeKind == TypeKind.Error || string.IsNullOrEmpty(elementFullTypeName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("SG013", "Generator Error", $"Invalid element type for ValueTuple mapping: Element {i} of {namedTupleType.ToDisplayString()} resulted in empty/error typeName '{elementFullTypeName}'. Skipping item.", "SourceGenerator", DiagnosticSeverity.Error, true),
                        Location.None));
                    elementFullTypeName = "/* ERROR_INVALID_ELEMENT_TYPE */"; 
                }

                var underlyingElementTypeSymbol = elementType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                                        ? ((INamedTypeSymbol)elementType).TypeArguments[0]
                                        : elementType;
                var underlyingElementTypeName = underlyingElementTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine($"                    {elementFullTypeName} {varName};");
                sb.AppendLine($"                    if (dr.IsDBNull({i}))");
                sb.AppendLine("                    {");
                if (elementType.IsReferenceType && !elementType.IsValueType)
                {
                    // Use null-forgiving operator (!) for reference type elements to handle nullability warnings
                    sb.AppendLine($"                        {varName} = default!; // Use ! to handle nullability warnings");
                }
                else
                {
                    sb.AppendLine($"                        {varName} = default;");
                }
                sb.AppendLine("                    }");
                sb.AppendLine("                    else");
                sb.AppendLine("                    {");
                bool isElementDateOnly = underlyingElementTypeName == "global::System.DateOnly" || underlyingElementTypeName == "System.DateOnly";
                bool isElementTimeOnly = underlyingElementTypeName == "global::System.TimeOnly" || underlyingElementTypeName == "System.TimeOnly";

                if (isElementDateOnly)
                {
                    sb.AppendLine($"                        var val_{varName} = dr.GetValue({i});");
                    sb.AppendLine($"                        if (val_{varName} is System.DateTime dt_{varName}) {varName} = System.DateOnly.FromDateTime(dt_{varName});");
                    sb.AppendLine($"                        else if (val_{varName} is System.DateOnly d_{varName}) {varName} = d_{varName};");
                    sb.AppendLine($"                        else {varName} = default;");
                }
                else if (isElementTimeOnly)
                {
                    sb.AppendLine($"                        var val_{varName} = dr.GetValue({i});");
                    sb.AppendLine($"                        if (val_{varName} is System.DateTime dt_{varName}) {varName} = System.TimeOnly.FromDateTime(dt_{varName});");
                    sb.AppendLine($"                        else if (val_{varName} is System.TimeSpan ts_{varName}) {varName} = System.TimeOnly.FromTimeSpan(ts_{varName});");
                    sb.AppendLine($"                        else if (val_{varName} is System.TimeOnly t_{varName}) {varName} = t_{varName};");
                    sb.AppendLine($"                        else {varName} = default;");
                }
                else
                {
                    sb.AppendLine($"                        {varName} = dr.GetFieldValue<{elementFullTypeName}>({i});");
                }
                sb.AppendLine("                    }");
                constructorArgs.Add(varName);
            }

            if (tupleArity > 0) // Only attempt to add if there are elements
            {
                if (elementTypes.Length == 1 && namedTupleType.Name.StartsWith("ValueTuple")) // Specifically for System.ValueTuple<T1>
                {
                    if (constructorArgs.Count == 0) 
                    {
                         context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("SG014", "Generator Error", $"ValueTuple mapping: constructorArgs is empty for tuple {fullTupleTypeName} with 1 element. Skipping Add.", "SourceGenerator", DiagnosticSeverity.Error, true),
                            Location.None));
                    }
                    else
                    {
                        sb.AppendLine($"                    resultList.Add(new {fullTupleTypeName}({constructorArgs[0]}));");
                    }
                }
                else // For compiler tuples or ValueTuples with arity > 1 or 0 (arity 0 is now skipped by outer if)
                {
                    string tupleCreationExpression = string.Join(", ", constructorArgs);
                    sb.AppendLine($"                    resultList.Add(({tupleCreationExpression}));");
                }
            }
            else // tupleArity is 0, likely from a non-generic ValueTuple that slipped through filters
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("SG019", "Generator Warning", $"ValueTuple mapping: tuple {fullTupleTypeName} has 0 elements. No item added to list for current row.", "SourceGenerator", DiagnosticSeverity.Warning, true),
                    Location.None));
                // Do not add anything to the list: sb.AppendLine("                    // resultList.Add(()); // This would be a syntax error");
            }

            sb.AppendLine("                }");
            sb.AppendLine("                return resultList;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        private class SyntaxNodeComparer : IEqualityComparer<SyntaxNode>
        {
            public static readonly SyntaxNodeComparer Instance = new SyntaxNodeComparer();
            public bool Equals(SyntaxNode? x, SyntaxNode? y) => x?.ToString() == y?.ToString();
            public int GetHashCode(SyntaxNode obj) => obj.ToString().GetHashCode();
        }
    }
} 