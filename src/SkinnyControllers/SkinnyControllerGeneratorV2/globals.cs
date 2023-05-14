global using Microsoft.CodeAnalysis;
global using System.Linq;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.CSharp.Syntax;
global using System;
global using System.Collections.Immutable;
global using System.Threading;
global using Microsoft.CodeAnalysis.Text;
global using System.IO;
global using System.Reflection;
global using System.Text;
global using System.Xml.Linq;

enum TemplateIndicator : long
{

    None = 0,
    AllPost = 1,
    NoArgs_Is_Get_Else_Post = 2,
    Rest = 3,
    AllPostWithRecord = 4,
    TryCatchLogging = 5,
    CustomTemplateFile = 10000,
}
