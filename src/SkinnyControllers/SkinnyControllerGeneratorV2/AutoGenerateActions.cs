using System.Xml.Linq;

namespace SkinnyControllerGeneratorV2;
[Generator]
public class AutoGenerateActions : IIncrementalGenerator
{
    string autoActions = typeof(AutoActionsAttribute).Name;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classTypes = context.SyntaxProvider
                             .ForAttributeWithMetadataName("SkinnyControllersCommon.AutoActionsAttribute",
                                                           ItIsOnClass,
                                                           GetClassInfo)
                             .Where(it=>it.IsValid())
                             .Collect();
        var templates = context.AdditionalTextsProvider
                                .Select((text, token) =>new { name = text.Path, text = text.GetText(token)?.ToString() })
                                .Where(text => text?.text is not null)!
                                .Collect();
        context.RegisterSourceOutput(classTypes.Combine(templates).SelectMany(), GenerateCode);

    }

    private void GenerateCode(SourceProductionContext arg1, (ImmutableArray<(INamedTypeSymbol, ClassDeclarationSyntax)>, ImmutableArray<object>) arg2)
    {
        throw new NotImplementedException();
    }

    private void GenerateCode1(SourceProductionContext context, ImmutableArray<(INamedTypeSymbol, ClassDeclarationSyntax)> arg2)
    {
        var executing = Assembly.GetExecutingAssembly();
        foreach (var item in arg2)
        {
            var cds = item.Item2;
            if (!IsPartial(cds))
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, "please add partial declaration"));
                continue;
            }
            var myController = item.Item1;
            string? Namespace = myController.ContainingNamespace.IsGlobalNamespace ? null : myController.ContainingNamespace.ToString();
            var att = myController.GetAttributes().FirstOrDefault(it => it.AttributeClass.Name == autoActions);
            if(att == null)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, "cannot find attribute on "+myController.Name));
                continue;
            }
            var template = att.NamedArguments.First(it => it.Key == "template")
                    .Value
                    .Value
                    .ToString();
            var templateId = (TemplateIndicator)long.Parse(template);
            var fields = att.NamedArguments.First(it => it.Key == "FieldsName")
                .Value
                .Values
                .Select(it => it.Value?.ToString())
                .ToArray()
                ;
            string[] excludeFields = null;
            try
            {
                excludeFields = att.NamedArguments.FirstOrDefault(it => it.Key == "ExcludeFields")
                    .Value
                    .Values
                    .Select(it => it.Value?.ToString())
                    .ToArray()
                    ;
            }
            catch (Exception)
            {
                //it is not mandatory to define ExcludeFields
                //do nothing, 
            }
            string templateCustom = "";
            if (att.NamedArguments.Any(it => it.Key == "CustomTemplateFileName"))
            {

                templateCustom = att.NamedArguments.First(it => it.Key == "CustomTemplateFileName")
                .Value
                .Value
                .ToString()
                ;
            }
            bool All = fields.Contains("*");

            var memberFields = myController
                    .GetMembers()
                    .Where(it => All || fields.Contains(it.Name))
                    .Select(it => it as IFieldSymbol)
                    .Where(it => it != null)
                    .ToArray();

            if (excludeFields?.Length > 0)
            {
                var q = memberFields.ToList();
                q.RemoveAll(it => excludeFields.Contains(it.Name));
                memberFields = q.ToArray();

            }
            if (memberFields.Length < fields.Length)
            {
                //report also the mismatched names
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning,
                            $"controller {myController.Name} have some fields to generate that were not found"));
            }
            if (memberFields.Length == 0)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error,
                        $"controller {myController.Name} do not have fields to generate"));
                continue;
            }
            string post = "";
            try
            {
                switch (templateId)
                {

                    case TemplateIndicator.None:
                        context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Info, $"class {myController.Name} has no template "));
                        continue;
                    case TemplateIndicator.CustomTemplateFile:

                        var file = context.AdditionalFiles.FirstOrDefault(it => it.Path.EndsWith(templateCustom));
                        if (file == null)
                        {
                            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, $"cannot find {templateCustom} for  {myController.Name} . Did you put in AdditionalFiles in csproj ?"));
                            continue;
                        }
                        post = file.GetText().ToString();
                        break;

                    default:
                        using (var stream = executing.GetManifestResourceStream($"SkinnyControllersGenerator.templates.{templateId}.txt"))
                        {
                            using var reader = new StreamReader(stream);
                            post = reader.ReadToEnd();

                        }
                        break;
                }

                string classSource = ProcessClass(myController, memberFields, post);
                if (string.IsNullOrWhiteSpace(classSource))
                    continue;


                context.AddSource($"{myController.Name}.autogenerate.cs", SourceText.From(classSource, Encoding.UTF8));

            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, $"{myController.Name} error {ex.Message}"));
            }
        }
    }

    static Diagnostic DoDiagnostic(DiagnosticSeverity ds, string message)
    {
        //info  could be seen only with 
        // dotnet build -v diag
        var dd = new DiagnosticDescriptor("SkinnyControllersGenerator", $"StartExecution", $"{message}", "SkinnyControllers", ds, true);
        var d = Diagnostic.Create(dd, Location.None, "andrei.txt");
        return d;
    }
    private static bool ItIsOnClass(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        return syntaxNode is ClassDeclarationSyntax classDeclaration;
    }
    
    public static bool IsPartial(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }
    private static DataGenerator GetClassInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var type = context.TargetSymbol as INamedTypeSymbol;
        var cds = context.TargetNode as ClassDeclarationSyntax;
        return new DataGenerator(type,cds);
    }

   
}
class DataGenerator : IEquatable<DataGenerator>
{
    public DataGenerator(INamedTypeSymbol nts, ClassDeclarationSyntax cds)
    {
        Nts = nts;
        Cds = cds;
    }

    public INamedTypeSymbol Nts { get; }
    public ClassDeclarationSyntax Cds { get; }

    public bool Equals(DataGenerator other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return Nts?.Name == other.Nts?.Name;
    }
    public bool IsValid()
    {
        return Nts != null && Cds != null;
    }
}

