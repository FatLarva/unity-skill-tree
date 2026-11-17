using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.LargeSample.Editor
{
    // The tree's effect list uses this drawer for each inline parameter instance.
    [CustomPropertyDrawer(typeof(StatModifierParameters))]
    public sealed class StatModifierParametersDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(StatModifierParameters.Stat)), "Stat"));
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var operation = new PropertyField(property.FindPropertyRelative(nameof(StatModifierParameters.Operation)), "Operation");
            var value = new PropertyField(property.FindPropertyRelative(nameof(StatModifierParameters.Value)), "Value");
            operation.style.flexGrow = 1f;
            value.style.flexGrow = 1f;
            row.Add(operation);
            row.Add(value);
            root.Add(row);
            return root;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => 2f * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(position, property.FindPropertyRelative(nameof(StatModifierParameters.Stat)));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            var left = position;
            left.width = position.width * .6f;
            var right = position;
            right.x += left.width + 4f;
            right.width -= left.width + 4f;
            EditorGUI.PropertyField(left, property.FindPropertyRelative(nameof(StatModifierParameters.Operation)));
            EditorGUI.PropertyField(right, property.FindPropertyRelative(nameof(StatModifierParameters.Value)), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }
}

