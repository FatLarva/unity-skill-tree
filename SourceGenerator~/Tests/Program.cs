using FatLarva.SkillTree.Generator;
using Game.SkillTree.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

int checks = 0;
var regular = Manifest("Gameplay", "Game.Skills", "SkillId", "00000000000000000000000000000001", (4, "Long Stride"), (17, "class"), (23, "7 Lives"), (42, "Сила"));
var valid = Run("Gameplay", "public class Probe { public int Id => (int)Game.Skills.SkillId.Long_Stride; }", File(regular));
Check(!valid.Errors.Any(), "A valid manifest and consumer compile: " + string.Join("\n", valid.Errors.Select(error => error.ToString())));
Check(valid.Source.Contains("None = 0") && valid.Source.Contains("Long_Stride = 4") && valid.Source.Contains("@class = 17") && valid.Source.Contains("_7_Lives = 23") && valid.Source.Contains("Сила = 42"), "Stable IDs and deterministic identifier conversion");
Check(Run("Unrelated", "", File(regular)).Source.Length == 0, "Explicit assembly scope excludes unrelated asmdefs");

var changed = Manifest("Gameplay", "Game.Skills", "SkillId", "00000000000000000000000000000001", (42, "Might"), (4, "Long Stride"));
var changedResult = Run("Gameplay", "", File(changed));
Check(changedResult.Source.Contains("Might = 42") && !changedResult.Source.Contains("Сила"), "Rename keeps the numeric identity");
var reordered = Manifest("Gameplay", "Game.Skills", "SkillId", "00000000000000000000000000000001", (4, "Long Stride"), (42, "Might"));
Check(changedResult.Source == Run("Gameplay", "", File(reordered)).Source, "Reordering nodes does not change generated source");
Check(SkillTreeEnumManifestFormat.Serialize(changed) == SkillTreeEnumManifestFormat.Serialize(reordered), "Reordering nodes does not rewrite the manifest");

ExpectError("STENUM001", Manifest("Gameplay", "", "Skills", Guid.NewGuid().ToString("N"), (0, "NoId")), "ID zero is reserved");
ExpectError("STENUM001", Manifest("Gameplay", "", "Skills", Guid.NewGuid().ToString("N"), (7, "One"), (7, "Two")), "Duplicate IDs rejected");
ExpectError("STENUM001", Manifest("Gameplay", "", "Skills", Guid.NewGuid().ToString("N"), (1, "Long Stride"), (2, "Long-Stride")), "Colliding names rejected");
ExpectError("STENUM001", Manifest("Gameplay", "", "Skills", Guid.NewGuid().ToString("N"), (1, "None")), "None reserved");
ExpectError("STENUM001", Manifest("Gameplay", "", "Skills", Guid.NewGuid().ToString("N"), (1, "value__")), "Enum backing field name reserved");
ExpectError("STENUM001", Manifest("Gameplay", "bad namespace", "Skills", Guid.NewGuid().ToString("N"), (1, "One")), "Invalid namespace rejected");
ExpectError("STENUM001", Manifest("Gameplay", "", "7Skills", Guid.NewGuid().ToString("N"), (1, "One")), "Invalid enum name rejected");
var second = Manifest("Gameplay", "Game.Skills", "SkillId", "00000000000000000000000000000002", (9, "Other"));
Check(Run("Gameplay", "", File(regular), File(second)).Errors.Any(e => e.Id == "STENUM002"), "Duplicate enum types across trees diagnosed");
Check(Run("Gameplay", "namespace Game.Skills { public enum SkillId { Existing } }", File(regular)).Errors.Any(e => e.Id == "STENUM003"), "Existing consumer type collision diagnosed");
Check(Run("Gameplay", "", new MemoryFile("bad" + SkillTreeEnumManifestFormat.FileSuffix, "<broken")).Errors.Any(e => e.Id == "STENUM001"), "Malformed manifest diagnosed without generator crash");
Check(Run("Gameplay", "", new MemoryFile("unrelated.additionalfile", "<broken")).Errors.Length == 0, "Other analyzers' additional files are ignored");
var keyword = Manifest("Gameplay", "namespace.event", "enum", Guid.NewGuid().ToString("N"), (1, "public"));
Check(Run("Gameplay", "", File(keyword)).Errors.Length == 0, "C# keywords escaped in namespace, type and members");
var large = Manifest("Gameplay", "Game.Skills", "LargeSkills", Guid.NewGuid().ToString("N"), Enumerable.Range(1, 600).Select(i => (i, "Skill " + i)).ToArray());
Check(Run("Gameplay", "", File(large)).Errors.Length == 0, "600-node manifest compiles");
Console.WriteLine($"PASS: {checks} source-generator checks");

void ExpectError(string id, SkillTreeEnumManifest manifest, string message) => Check(Run("Gameplay", "", File(manifest)).Errors.Any(error => error.Id == id), message);
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }

static SkillTreeEnumManifest Manifest(string assembly, string ns, string name, string guid, params (int Id, string Name)[] entries)
{
    var manifest = new SkillTreeEnumManifest { AssemblyName = assembly, Namespace = ns, TypeName = name, TreeGuid = guid };
    foreach (var entry in entries) manifest.Nodes.Add(new SkillTreeEnumEntry { Id = entry.Id, FileName = entry.Name, AssetGuid = entry.Id.ToString("x32") });
    return manifest;
}

static MemoryFile File(SkillTreeEnumManifest manifest) => new MemoryFile(manifest.TreeGuid + SkillTreeEnumManifestFormat.FileSuffix, SkillTreeEnumManifestFormat.Serialize(manifest));

static (string Source, Diagnostic[] Errors) Run(string assemblyName, string consumer, params AdditionalText[] files)
{
    var input = CSharpCompilation.Create(assemblyName,
        new[] { CSharpSyntaxTree.ParseText(consumer) },
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new SkillTreeEnumGenerator() }, files);
    driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
    var errors = diagnostics.Concat(output.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    var source = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
    return (source, errors);
}

sealed class MemoryFile : AdditionalText
{
    private readonly SourceText _text;
    public MemoryFile(string path, string text) { Path = path; _text = SourceText.From(text); }
    public override string Path { get; }
    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}
