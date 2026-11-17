using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.SkillTree
{
    public static class SkillTreeLayout
    {
        private const string DefaultFormat = "skill-tree-layout";
        private const int Version = 1;
        private const int MaxJsonLength = 1_000_000;
        internal const float MaxCoordinate = 20_000f;
        private const string Number = @"(-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)";

        private static readonly Regex PositionsStart = new Regex("\"positions\"\\s*:\\s*\\{");
        private static readonly Regex XCoordinate = new Regex("\"x\"\\s*:\\s*" + Number + @"(?=\s*[,}])");
        private static readonly Regex YCoordinate = new Regex("\"y\"\\s*:\\s*" + Number + @"(?=\s*[,}])");

        public static string Export(IReadOnlyList<SkillTreeNode> nodes, string format = DefaultFormat)
        {
            var json = new StringBuilder("{\"format\":");
            AppendString(json, format);
            json.Append(",\"version\":").Append(Version).Append(",\"positions\":{");

            for (var i = 0; i < nodes.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                var node = nodes[i];
                AppendString(json, node.Id.ToString(CultureInfo.InvariantCulture));
                json.Append(':').Append(JsonUtility.ToJson(new Point(node.Position)));
            }

            return json.Append("}}").ToString();
        }

        public static Dictionary<int, Vector2> Import(
            string json, IReadOnlyList<SkillTreeNode> knownNodes, string format = DefaultFormat)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
            {
                throw new ArgumentException("Invalid skill tree layout.", nameof(json));
            }

            var header = JsonUtility.FromJson<Header>(json);
            var positionsStart = PositionsStart.Match(json);
            if (header == null || header.format != format || header.version != Version || !positionsStart.Success)
            {
                throw new ArgumentException("Invalid skill tree layout format or version.", nameof(json));
            }

            var positions = new Dictionary<int, Vector2>();
            var start = positionsStart.Index + positionsStart.Length;
            foreach (var node in knownNodes)
            {
                var quotedId = AppendString(new StringBuilder(), node.Id.ToString(CultureInfo.InvariantCulture)).ToString();
                var pointPattern = new Regex(Regex.Escape(quotedId) + @"\s*:\s*(\{[^{}]*\})");
                var match = pointPattern.Match(json, start);
                if (!match.Success)
                {
                    continue;
                }

                var point = match.Groups[1].Value;
                if (TryReadCoordinate(XCoordinate, point, out var x) &&
                    TryReadCoordinate(YCoordinate, point, out var y))
                {
                    positions[node.Id] = new Vector2(x, y);
                }
            }

            return positions;
        }

        private static bool TryReadCoordinate(Regex pattern, string point, out float coordinate)
        {
            coordinate = default;
            var match = pattern.Match(point);
            return match.Success &&
                   float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) &&
                   float.IsFinite(coordinate) &&
                   Mathf.Abs(coordinate) <= MaxCoordinate;
        }

        private static StringBuilder AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                if (character == '"' || character == '\\')
                {
                    json.Append('\\').Append(character);
                }
                else if (character < ' ')
                {
                    json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    json.Append(character);
                }
            }

            return json.Append('"');
        }

        [Serializable]
        private sealed class Header
        {
            public string format = string.Empty;
            public int version = 0;
        }

        [Serializable]
        private struct Point
        {
            public float x;
            public float y;

            public Point(Vector2 position)
            {
                x = position.x;
                y = position.y;
            }
        }
    }
}
