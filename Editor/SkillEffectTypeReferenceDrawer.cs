using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomPropertyDrawer(typeof(SkillEffectTypeReference))]
    public sealed class SkillEffectTypeReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var source = property.serializedObject;
            string path = property.propertyPath;
            var root = new VisualElement();
            var field = new ObjectField(property.displayName)
            {
                objectType = typeof(ScriptableObject),
                allowSceneObjects = false,
                tooltip = "A ScriptableObject implementing ISkillEffectType. Its parameters are stored in each node, not in this asset."
            };
            field.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            root.Add(field);
            root.Add(error);

            void Refresh()
            {
                if (source.targetObject == null) return;
                var asset = source.FindProperty(path)?.FindPropertyRelative("_asset");
                if (asset == null) return;
                field.SetValueWithoutNotify(asset.objectReferenceValue);
                string message = ValidationMessage(asset.objectReferenceValue as ScriptableObject);
                error.text = message ?? string.Empty;
                error.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            field.RegisterValueChangedCallback(change =>
            {
                var next = change.newValue as ScriptableObject;
                if (next != null && next is not ISkillEffectType)
                {
                    field.SetValueWithoutNotify(change.previousValue);
                    error.text = $"'{next.name}' must implement {nameof(ISkillEffectType)}.";
                    error.style.display = DisplayStyle.Flex;
                    return;
                }
                if (source.targetObject == null) return;
                source.UpdateIfRequiredOrScript();
                var asset = source.FindProperty(path)?.FindPropertyRelative("_asset");
                if (asset == null) return;
                asset.objectReferenceValue = next;
                source.ApplyModifiedProperties();
                SkillTreeAssetEditor.SaveEditedAsset(source.targetObject as SkillTreeConfigAsset);
                Refresh();
            });
            root.TrackPropertyValue(property.FindPropertyRelative("_asset"), _ => Refresh());
            Refresh();
            return root;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var asset = property.FindPropertyRelative("_asset");
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.ObjectField(position, label, asset.objectReferenceValue, typeof(ScriptableObject), false);
            if (EditorGUI.EndChangeCheck() && (next == null || next is ISkillEffectType))
                asset.objectReferenceValue = next;
            EditorGUI.EndProperty();
        }

        private static string ValidationMessage(ScriptableObject asset)
        {
            if (asset == null) return "Assign an effect type asset.";
            try
            {
                _ = new SkillEffectTypeReference(asset);
                return null;
            }
            catch (System.ArgumentException exception)
            {
                return exception.Message;
            }
        }
    }
}
