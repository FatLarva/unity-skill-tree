using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomPropertyDrawer(typeof(SkillEffect))]
    public sealed class SkillEffectDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var source = property.serializedObject;
            var node = source.targetObject as SkillNodeConfig;
            string path = property.propertyPath;
            var root = new VisualElement();
            var dropdown = new DropdownField("Type")
            {
                tooltip = "Changing the effect type creates fresh inline parameters. The shared type asset is not edited."
            };
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            var fields = new VisualElement();
            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            var choices = new List<ScriptableObject>();
            var labels = new List<string>();
            long shownReferenceId = long.MinValue;
            Button reset = null;
            reset = new Button(() => SetType(CurrentAsset())) { text = "Reset parameters" };
            root.Add(dropdown);
            root.Add(fields);
            root.Add(error);
            root.Add(reset);
            ScriptableObject CurrentAsset() => source.targetObject == null ? null :
                source.FindProperty(path)?.FindPropertyRelative("_type")?.FindPropertyRelative("_asset")?.objectReferenceValue as ScriptableObject;

            void RefreshChoices()
            {
                if (source.targetObject == null) return;
                var current = CurrentAsset();
                choices.Clear();
                labels.Clear();
                choices.Add(null);
                labels.Add("Choose a type");
                if (node != null && node.Owner != null)
                {
                    foreach (var reference in node.Owner.EffectTypes)
                    {
                        var asset = reference.Asset;
                        if (asset == null || asset is not ISkillEffectType || choices.Contains(asset)) continue;
                        choices.Add(asset);
                        AddLabel(asset.name);
                    }
                }
                int selected = choices.IndexOf(current);
                if (selected < 0)
                {
                    selected = choices.Count;
                    choices.Add(current);
                    AddLabel($"Unregistered ({current.name})");
                }
                dropdown.choices = new List<string>(labels);
                dropdown.SetValueWithoutNotify(labels[selected]);
                dropdown.SetEnabled(!source.isEditingMultipleObjects && choices.Count > 1);
            }

            void AddLabel(string name)
            {
                string label = name;
                for (int suffix = 2; labels.Contains(label); suffix++) label = $"{name} ({suffix})";
                labels.Add(label);
            }

            void RefreshValidation()
            {
                if (source.targetObject == null) return;
                var current = source.FindProperty(path);
                if (current == null) return;
                var asset = CurrentAsset();
                var parameters = current.FindPropertyRelative("_parameters");
                string message = null;
                bool canReset = false;
                if (node == null || node.Owner == null)
                    message = "Add this node to a tree before choosing an effect type.";
                else if (asset == null)
                    message = "Choose an effect type from the tree's Effect Types catalogue.";
                else
                {
                    bool registered = false;
                    foreach (var reference in node.Owner.EffectTypes)
                        if (reference.Asset == asset) { registered = true; break; }
                    if (!registered) message = $"Register '{asset.name}' in the tree's Effect Types catalogue.";
                    else
                    {
                        try
                        {
                            var reference = new SkillEffectTypeReference(asset);
                            var value = parameters.managedReferenceValue as ISkillEffectParameters;
                            canReset = value == null || !reference.Value.ParametersType.IsInstanceOfType(value);
                            _ = new SkillEffect(reference, value);
                        }
                        catch (ArgumentException exception) { message = exception.Message; }
                    }
                }
                error.text = message ?? string.Empty;
                error.style.display = message == null ? DisplayStyle.None : DisplayStyle.Flex;
                reset.style.display = canReset ? DisplayStyle.Flex : DisplayStyle.None;
                reset.SetEnabled(!source.isEditingMultipleObjects);
            }

            void RefreshFields(bool force = false)
            {
                if (source.targetObject == null) return;
                var parameters = source.FindProperty(path)?.FindPropertyRelative("_parameters");
                if (parameters == null) return;
                if (!force && shownReferenceId == parameters.managedReferenceId) return;
                shownReferenceId = parameters.managedReferenceId;
                fields.Unbind();
                fields.Clear();
                if (parameters.managedReferenceValue == null) return;
                parameters.isExpanded = true;
                // Bind the whole parameter property so Unity can select the user's class PropertyDrawer.
                fields.Add(new PropertyField(parameters.Copy(), "Parameters"));
                fields.Bind(source);
            }

            void SetType(ScriptableObject next)
            {
                if (source.targetObject == null || source.isEditingMultipleObjects) return;
                try
                {
                    ISkillEffectParameters parameters = null;
                    if (next != null)
                    {
                        var reference = new SkillEffectTypeReference(next);
                        parameters = reference.Value.CreateParameters();
                        if (parameters == null || !reference.Value.ParametersType.IsInstanceOfType(parameters))
                            throw new InvalidOperationException($"Effect type '{next.name}' returned missing or incompatible default parameters.");
                    }
                    source.UpdateIfRequiredOrScript();
                    var current = source.FindProperty(path);
                    if (current == null) return;
                    current.FindPropertyRelative("_parameters").managedReferenceValue = parameters;
                    current.FindPropertyRelative("_type").FindPropertyRelative("_asset").objectReferenceValue = next;
                    source.ApplyModifiedProperties();
                    SkillTreeAssetEditor.SaveEditedAsset(node);
                    RefreshChoices();
                    RefreshFields(true);
                    RefreshValidation();
                }
                catch (Exception exception)
                {
                    error.text = exception.Message;
                    error.style.display = DisplayStyle.Flex;
                    Debug.LogException(exception, next);
                    RefreshChoices();
                }
            }

            dropdown.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index >= 0 && choices[index] != CurrentAsset()) SetType(choices[index]);
            });
            dropdown.RegisterCallback<PointerDownEvent>(_ => RefreshChoices(), TrickleDown.TrickleDown);
            dropdown.RegisterCallback<FocusInEvent>(_ => RefreshChoices());
            root.TrackPropertyValue(property.FindPropertyRelative("_type").FindPropertyRelative("_asset"), _ =>
            {
                RefreshChoices();
                RefreshFields(true);
                RefreshValidation();
            });
            root.TrackPropertyValue(property.FindPropertyRelative("_parameters"), _ =>
            {
                RefreshFields();
                RefreshValidation();
            });
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshValidation());
            RefreshChoices();
            RefreshFields(true);
            RefreshValidation();
            return root;
        }
    }
}
