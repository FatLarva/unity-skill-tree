using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Game.SkillTree.CodeGeneration
{
    // Shared with the source generator by a linked Compile item. No Unity or Roslyn dependency.
    internal sealed class SkillTreeEnumManifest
    {
        public string AssemblyName;
        public string Namespace;
        public string TypeName;
        public string TreeGuid;
        public readonly List<SkillTreeEnumEntry> Nodes = new List<SkillTreeEnumEntry>();
    }

    internal sealed class SkillTreeEnumEntry
    {
        public int Id;
        public string FileName;
        public string AssetGuid;
    }

    internal static class SkillTreeEnumManifestFormat
    {
        public const string AnalyzerName = "FatLarva.SkillTree.Generator";
        public const string FileSuffix = "." + AnalyzerName + ".additionalfile";
        private static readonly HashSet<string> Keywords = new HashSet<string>(
            ("abstract as base bool break byte case catch char checked class const continue decimal " +
             "default delegate do double else enum event explicit extern false finally fixed float for " +
             "foreach goto if implicit in int interface internal is lock long namespace new null object " +
             "operator out override params private protected public readonly ref return sbyte sealed short " +
             "sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked " +
             "unsafe ushort using virtual void volatile while").Split(' '), StringComparer.Ordinal);

        public static string Serialize(SkillTreeEnumManifest manifest)
        {
            var root = new XElement("skillTreeEnum",
                new XAttribute("version", "1"),
                new XAttribute("assembly", manifest.AssemblyName ?? string.Empty),
                new XAttribute("namespace", manifest.Namespace ?? string.Empty),
                new XAttribute("name", manifest.TypeName ?? string.Empty),
                new XAttribute("tree", manifest.TreeGuid ?? string.Empty));
            var nodes = new List<SkillTreeEnumEntry>(manifest.Nodes);
            nodes.Sort((a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : string.CompareOrdinal(a.FileName, b.FileName));
            foreach (var node in nodes)
            {
                root.Add(new XElement("node", new XAttribute("id", node.Id),
                    new XAttribute("file", node.FileName ?? string.Empty),
                    new XAttribute("asset", node.AssetGuid ?? string.Empty)));
            }
            return root.ToString(SaveOptions.None).Replace("\r\n", "\n") + "\n";
        }

        public static bool TryParse(string text, out SkillTreeEnumManifest manifest, out string error)
        {
            manifest = null;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var source = new StringReader(text))
                using (var reader = XmlReader.Create(source, settings))
                {
                    var root = XElement.Load(reader);
                    if (root.Name != "skillTreeEnum" || (string)root.Attribute("version") != "1")
                    {
                        error = "Expected a skillTreeEnum manifest with version 1.";
                        return false;
                    }
                    var result = new SkillTreeEnumManifest
                    {
                        AssemblyName = (string)root.Attribute("assembly"),
                        Namespace = (string)root.Attribute("namespace") ?? string.Empty,
                        TypeName = (string)root.Attribute("name"),
                        TreeGuid = (string)root.Attribute("tree")
                    };
                    foreach (var element in root.Elements())
                    {
                        int id;
                        if (element.Name != "node" || !int.TryParse((string)element.Attribute("id"),
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                        {
                            error = "Every manifest element must be a node with an integer id.";
                            return false;
                        }
                        result.Nodes.Add(new SkillTreeEnumEntry
                        {
                            Id = id,
                            FileName = (string)element.Attribute("file"),
                            AssetGuid = (string)element.Attribute("asset")
                        });
                    }
                    manifest = result;
                    error = null;
                    return true;
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is ArgumentException)
            {
                error = "Cannot read enum manifest: " + exception.Message;
                return false;
            }
        }

        public static bool Validate(SkillTreeEnumManifest manifest, out string error)
        {
            if (string.IsNullOrWhiteSpace(manifest.AssemblyName))
                return Fail("Target Assembly Name is required (the asmdef name, or Assembly-CSharp).", out error);
            if (!IsIdentifier(manifest.TypeName) || manifest.TypeName == "None" || manifest.TypeName == "value__")
                return Fail("Enum Type Name must be a C# identifier other than None or value__.", out error);
            if (!string.IsNullOrEmpty(manifest.Namespace))
            {
                foreach (string part in manifest.Namespace.Split('.'))
                    if (!IsIdentifier(part)) return Fail("Enum Namespace must contain dot-separated C# identifiers.", out error);
            }
            Guid treeGuid;
            if (!Guid.TryParseExact(manifest.TreeGuid, "N", out treeGuid))
                return Fail("The tree must be a saved Unity asset with a valid GUID.", out error);

            var ids = new HashSet<int>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in manifest.Nodes)
            {
                if (node.Id <= 0 || !ids.Add(node.Id))
                    return Fail("Node '" + node.FileName + "' has a non-positive or duplicate ID: " + node.Id + ".", out error);
                if (string.IsNullOrEmpty(node.FileName)) return Fail("A node has no asset file name.", out error);
                Guid assetGuid;
                if (!Guid.TryParseExact(node.AssetGuid, "N", out assetGuid))
                    return Fail("Node '" + node.FileName + "' must be a saved Unity asset with a valid GUID.", out error);
                string name = ToIdentifier(node.FileName);
                if (name == "None" || name == "value__" || name == manifest.TypeName || !names.Add(name))
                    return Fail("Node file '" + node.FileName + "' produces duplicate or reserved enum member '" + name + "'. Rename the node asset.", out error);
            }
            error = null;
            return true;
        }

        public static string ToIdentifier(string fileName)
        {
            var result = new StringBuilder();
            foreach (char value in fileName.Normalize(NormalizationForm.FormC))
                result.Append(IsPart(value) ? value : '_');
            if (result.Length == 0 || !IsStart(result[0])) result.Insert(0, '_');
            return result.ToString();
        }

        public static string EscapeIdentifier(string identifier) => Keywords.Contains(identifier) ? "@" + identifier : identifier;

        public static string EscapeNamespace(string value)
        {
            string[] parts = value.Split('.');
            for (int i = 0; i < parts.Length; i++) parts[i] = EscapeIdentifier(parts[i]);
            return string.Join(".", parts);
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsStart(value[0])) return false;
            for (int i = 1; i < value.Length; i++) if (!IsPart(value[i])) return false;
            return true;
        }

        private static bool IsStart(char value)
        {
            if (value == '_') return true;
            switch (char.GetUnicodeCategory(value))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber: return true;
                default: return false;
            }
        }

        private static bool IsPart(char value)
        {
            if (IsStart(value)) return true;
            switch (char.GetUnicodeCategory(value))
            {
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark: return true;
                default: return false;
            }
        }

        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
