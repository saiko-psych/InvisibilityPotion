// Parses every C# file of the mod project with Roslyn, once with DEBUG and once without (Release), and fails on a syntax error.
// The game DLLs are not needed, so this runs in CI where `make build` cannot: it catches broken syntax and #if mistakes,
// not missing members or type errors (those still need `make build` against the game).
// Usage: dotnet run --project tools/syntax-check -- InvisibilityPotion
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("usage: SyntaxCheck <project directory>");
    return 2;
}
var files = Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();
var errors = 0;
foreach (var symbols in new[] { new[] { "DEBUG" }, Array.Empty<string>() })
{
    var options = new CSharpParseOptions(LanguageVersion.CSharp10, preprocessorSymbols: symbols);   // LangVersion 10, as in the csproj
    foreach (var file in files)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options, file);
        foreach (var d in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
        {
            Console.WriteLine($"[{(symbols.Length == 0 ? "Release" : "Debug")}] {d}");
            errors++;
        }
    }
}
Console.WriteLine($"{files.Count} files, Debug and Release: {errors} syntax error(s)");
return errors == 0 ? 0 : 1;
