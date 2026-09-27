using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

// Run from the repository root. Existing copy keys/translations are never overwritten.
var root=Directory.GetCurrentDirectory();
var jsonOptions=new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
var translations=new[]{"default","zh"}.ToDictionary(x=>x,x=>JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText($"BetterBeads/i18n/{x}.json"))!);
bool Chinese(string text)=>text.Any(c=>c is >= '\u4e00' and <= '\u9fff');
int added=0;
foreach(var path in Directory.EnumerateFiles("BetterBeads","*.cs",SearchOption.AllDirectories)
    .Where(p=>!p.Split(Path.DirectorySeparatorChar).Any(x=>x is "bin" or "obj") && !p.EndsWith(".g.cs") && !p.EndsWith("ContentText.cs")))
{
    string source=File.ReadAllText(path);var tree=CSharpSyntaxTree.ParseText(source);var syntax=tree.GetRoot();
    var edits=new List<(int Start,int Length,string Text)>();
    foreach(var node in syntax.DescendantNodes().Where(n=>n is InterpolatedStringExpressionSyntax || n is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.StringLiteralExpression)))
    {
        // Outer interpolated strings own all their expression children; no overlapping edits.
        if(node.Ancestors().Any(n=>n is InterpolatedStringExpressionSyntax || n is AttributeSyntax || n is ConstantPatternSyntax || n is CaseSwitchLabelSyntax
            || n is FieldDeclarationSyntax f && f.Modifiers.Any(SyntaxKind.ConstKeyword)
            || n is LocalDeclarationStatementSyntax d && d.Modifiers.Any(SyntaxKind.ConstKeyword)
            || n is InvocationExpressionSyntax call && call.Expression.ToString().StartsWith("ContentText.")))continue;
        string value;string method;
        if(node is LiteralExpressionSyntax literal){value=literal.Token.ValueText;method="Get";}
        else
        {
            var interpolated=(InterpolatedStringExpressionSyntax)node;
            if(!interpolated.Contents.OfType<InterpolatedStringTextSyntax>().Any(t=>Chinese(t.TextToken.ValueText)))continue;
            var sb=new StringBuilder();int index=0;
            foreach(var part in interpolated.Contents)
                if(part is InterpolatedStringTextSyntax t)sb.Append(t.TextToken.ValueText);
                else if(part is InterpolationSyntax i)sb.Append('{').Append(index++).Append(i.AlignmentClause?.ToString()).Append(i.FormatClause?.ToString()).Append('}');
            value=sb.ToString();method="Format";
        }
        if(!Chinese(value))continue;
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..10].ToLowerInvariant();
        string key="copy."+Path.GetFileNameWithoutExtension(path)+"."+hash;
        foreach(var catalog in translations.Values)catalog.TryAdd(key,value);
        edits.Add((node.SpanStart,node.Span.Length,$"ContentText.{method}(\"{key}\",{node})"));added++;
    }
    foreach(var edit in edits.OrderByDescending(e=>e.Start))source=source.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Text);
    if(edits.Count>0)File.WriteAllText(path,source,new UTF8Encoding(false));
}
foreach(var catalog in translations)File.WriteAllText($"BetterBeads/i18n/{catalog.Key}.json",JsonSerializer.Serialize(catalog.Value,jsonOptions)+"\n");
var rows=new List<object>();
foreach(var path in Directory.EnumerateFiles("BetterBeads","*.cs",SearchOption.AllDirectories).Where(p=>!p.Split(Path.DirectorySeparatorChar).Any(x=>x is "bin" or "obj")))
{
    var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(path));
    foreach(var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n=>n.Expression.ToString().StartsWith("ContentText.")))
    {
        if(call.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax key)continue;
        rows.Add(new {Key=key.Token.ValueText,Text=translations["default"].GetValueOrDefault(key.Token.ValueText),File=path.Replace('\\','/'),Line=tree.GetLineSpan(call.Span).StartLinePosition.Line+1});
    }
}
Directory.CreateDirectory("art/text");
File.WriteAllText("art/text/copy-index.json",JsonSerializer.Serialize(rows,jsonOptions)+"\n");
Console.WriteLine($"Extracted {added} text expressions; indexed {rows.Count} uses. Existing translations retained.");
