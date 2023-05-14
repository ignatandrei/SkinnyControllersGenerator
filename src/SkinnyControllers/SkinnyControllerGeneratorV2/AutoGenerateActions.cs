using System.Collections.Generic;

namespace SkinnyControllerGeneratorV2;

[Generator]
public class AutoGenerateActions : IIncrementalGenerator
{
    string autoActions = "AutoActionsAttribute";//typeof(AutoActionsAttribute).Name;
    string autoActionsFullName = "SkinnyControllersCommon.AutoActionsAttribute";//typeof(AutoActionsAttribute).FullName;
    static string endFileName = "controller.txt";
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classTypes = context.SyntaxProvider
                             .ForAttributeWithMetadataName(autoActionsFullName,
                                                           ItIsOnClass,
                                                           GetClassInfo)
                             .Where(it=>it.IsValid())
                             .Collect()
                             //.SelectMany((enumInfos, _) => enumInfos.Distinct())
                               ; 

        var templates = context.AdditionalTextsProvider
                                .Where(text => text.Path.EndsWith(endFileName, StringComparison.OrdinalIgnoreCase))
                                .Select((text, token) =>new AdditionalFilesText (  text.Path, text.GetText(token)?.ToString()) )
                                .Where(text => text.IsValid())
                                .Collect();

        context.RegisterSourceOutput(classTypes.Combine(templates), GenerateCode);

    }

    private void GenerateCode(SourceProductionContext arg1, (ImmutableArray<DataGenerator> Left, ImmutableArray<AdditionalFilesText> Right) arg2)
    {
        var dg=arg2.Left.Distinct().ToArray();
        var files = arg2.Right;
        GenerateCode1(arg1, dg, files);
    }

    private void GenerateCode1(SourceProductionContext context, DataGenerator[] arg2, ImmutableArray<AdditionalFilesText> files)
    {
        var executing = Assembly.GetExecutingAssembly();
        foreach (var item in arg2)
        {
            var cds = item.Cds;
            if (!IsPartial(cds))
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, "please add partial declaration"));
                continue;
            }
            var myController = item.Nts;
            string? Namespace = myController.ContainingNamespace.IsGlobalNamespace ? null : myController.ContainingNamespace.ToString();
            var att = myController.GetAttributes().FirstOrDefault(it => it.AttributeClass.Name == autoActions);
            if(att == null)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, "cannot find attribute on "+myController.Name));
                continue;
            }
            var namedArgs = att.NamedArguments;
            var template = namedArgs.First(it => it.Key == "template")
                    .Value
                    .Value
                    .ToString();
            var templateId = (TemplateIndicator)long.Parse(template);
            var fields = namedArgs.First(it => it.Key == "FieldsName")
                .Value
                .Values
                .Select(it => it.Value?.ToString())
                .ToArray()
                ;
            string[] excludeFields = null;
            try
            {
                excludeFields = namedArgs.FirstOrDefault(it => it.Key == "ExcludeFields")
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
                        if(!templateCustom.EndsWith(endFileName))
                        {
                            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, $"template {templateCustom} must end in {endFileName}"));
                        }
                        var file = files.FirstOrDefault(it => it.Name.EndsWith(templateCustom));
                        if (file == null)
                        {
                            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Error, $"cannot find {templateCustom} for  {myController.Name} . Did you put in AdditionalFiles in csproj ?"));
                            continue;
                        }
                        post = file.Contents.ToString();
                        break;

                    default:
                        using (var stream = executing.GetManifestResourceStream($"SkinnyControllersGeneratorV2.templates.{templateId}.txt"))
                        {
                            using var reader = new StreamReader(stream);
                            post = reader.ReadToEnd();

                        }
                        break;
                }

                string classSource = ProcessClass(context,myController, memberFields, post);
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
    private MethodDefinition[] ProcessField(SourceProductionContext context, IFieldSymbol fieldSymbol)
    {

        var ret = new Dictionary<string, MethodDefinition>();
        var code = new StringBuilder();
        string fieldName = fieldSymbol.Name;
        var fieldType = fieldSymbol.Type;
        var members = fieldType.GetMembers().OfType<IMethodSymbol>();


        foreach (var m in members)
        {
            if (m.IsStatic)
                continue;

            if (m.Kind != SymbolKind.Method)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning, $"{m.Name} is not a method ? "));
                continue;

            }

            var ms = m as IMethodSymbol;
            if (ms is null)
            {
                context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning, $"{m.Name} is not a IMethodSymbol"));
                continue;

            }
            if (ms.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (ms.MethodKind != MethodKind.Ordinary)
                continue;

            if ((ms.Name == fieldName || ms.Name == ".ctor") && ms.ReturnsVoid)
                continue;

            var md = new MethodDefinition();
            md.Name = ms.Name;
            md.RegisteredName = md.Name;
            md.FieldName = fieldName;
            md.ReturnsVoid = ms.ReturnsVoid;
            md.Original = ms;
            md.IsAsync = ms.IsAsync;
            if (!md.IsAsync)
            {
                md.IsAsync = (ms.ReturnType?.BaseType?.Name == "Task");
            }
            md.ReturnType = ms.ReturnType.ToString();
            md.Parameters = ms.Parameters.ToDictionary(it => it.Name, it => it.Type);
            //if 2 method have same names, generate different actions
            int i = 0;
            string name = md.RegisteredName;
            //DoDiagnostic(DiagnosticSeverity.Error, "Andrei_" + name);
            if (name.EndsWith("Async"))
            {
                md.Name = name.Substring(0, name.Length - "Async".Length);
                //DoDiasgnostic(DiagnosticSeverity.Error,"Andrei_" + name);
            }

            while (ret.ContainsKey(md.Name))
            {
                i++;
                //same name for the method ... let's modify the name
                md.Name = name + i;
            }
            ret.Add(md.Name, md);

        }
        if (ret.Count == 0)
        {
            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning,
                $"could not find methods on {fieldName} from {fieldSymbol.ContainingType?.Name}"));
        }
        return ret.Values.ToArray();
    }
    private string ProcessClass(SourceProductionContext context, INamedTypeSymbol classSymbol, IFieldSymbol[] fields, string post)
    {


        //if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
        //{
        //    context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning, $"class {classSymbol.Name} is in other namespace; please put directly "));
        //    return null;
        //}

        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
        var cd = new ClassDefinition();
        cd.NamespaceName = namespaceName;
        cd.ClassName = classSymbol.Name;
        cd.Original = classSymbol;
        if (fields.Length == 0)
        {
            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning, $"class {cd.ClassName} has {fields.Length} fields to process"));
        }
        cd.DictNameField_Methods = fields
            .SelectMany(it => ProcessField(context,it))
            .GroupBy(it => it.FieldName)
            .ToDictionary(it => it.Key, it => it.ToArray());


        if (cd.DictNameField_Methods.Count == 0)
        {
            context.ReportDiagnostic(DoDiagnostic(DiagnosticSeverity.Warning, $"class {cd.ClassName} has 0 fields to process"));
        }
        var template = Scriban.Template.Parse(post);
        var output = template.Render(cd, member => member.Name);
        return output;

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
        if (syntaxNode is not ClassDeclarationSyntax classDeclaration)
            return false;
        if (!IsPartial(classDeclaration))
            return false;
        if (classDeclaration.AttributeLists.Count== 0) return false;
       
        return true;
    }
    
    public static bool IsPartial(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }
    private static DataGenerator GetClassInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var type = context.TargetSymbol as INamedTypeSymbol;
        if (type == null)
            return null;
        var cds = context.TargetNode as ClassDeclarationSyntax;
        if (cds == null)
            return null;

        return new DataGenerator(type,cds);
    }

   
}

public class AdditionalFilesText
{
    public AdditionalFilesText(string Name, string Contents)
    {
        this.Name = Name;
        this.Contents = Contents;
    }

    public string Name { get; }
    public string Contents { get; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Contents);
    }
}


