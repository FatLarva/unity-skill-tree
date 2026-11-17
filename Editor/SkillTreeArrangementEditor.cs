using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomEditor(typeof(SkillTreeArrangementContext))]
    public sealed class SkillTreeArrangementEditor : SkillTreeInspectorBase
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = BeginInspector();
            var context = (SkillTreeArrangementContext)target;
            var source = new SerializedObject(context);
            Bindings.Add(source);
            root.Add(new HelpBox("Changes accumulate. Each drag or text edit is one Undo step; the tree previews it live. Scope only chooses what the next edit affects.", HelpBoxMessageType.Info));
            var selected = new Label { name = "arrangementSelection" };
            selected.AddToClassList("skill-tree-help");
            root.Add(selected);
            var preset = new EnumField("Shape", context.Preset) { name = "preset" };
            var scope = new EnumField("Scope", context.Scope) { name = "scope" };
            root.Add(preset);
            root.Add(scope);
            FloatField horizontal = null;
            FloatField vertical = null;
            FloatField angle = null;
            FloatField length = null;
            FloatField radius = null;
            HelpBox error = null;
            FloatField editingField = null;
            float editingStartValue = 0f;
            var numbers = new List<(string Property, FloatField Field)>();

            void FinishEdit()
            {
                if (context != null)
                {
                    if (editingField != null && editingField.value.Equals(editingStartValue)) context.CancelEdit();
                    else context.CommitEdit();
                }
                editingField = null;
            }

            FloatField Number(string property, string label, Func<float, float> normalize = null)
            {
                var field = new FloatField(label) { name = property.TrimStart('_') };
                field.SetValueWithoutNotify(source.FindProperty(property).floatValue);
                numbers.Add((property, field));
                root.Add(field);
                bool dragging = false;
                bool editingText = false;
                bool cancelling = false;
                float startingValue = field.value;

                void BeginEdit(float value)
                {
                    if (editingField == field && context.IsPreviewing) return;
                    FinishEdit();
                    startingValue = value;
                    editingStartValue = value;
                    editingField = field;
                    context.BeginEdit();
                }
                void WriteValue(float value)
                {
                    source.Update();
                    source.FindProperty(property).floatValue = value;
                    source.ApplyModifiedPropertiesWithoutUndo();
                }
                // The native label dragger stops PointerDown at the label. Start on its
                // ancestor. Captured PointerUp only reaches the label, where our
                // callback runs after the native dragger finishes.
                field.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || !field.labelElement.Contains(evt.target as VisualElement) &&
                        evt.target != field.labelElement) return;
                    dragging = true;
                    BeginEdit(field.value);
                }, TrickleDown.TrickleDown);
                field.labelElement.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!dragging || evt.button != 0) return;
                    dragging = false;
                    if (!cancelling) FinishEdit();
                    UpdateFields();
                });
                field.labelElement.RegisterCallback<PointerCaptureOutEvent>(_ =>
                {
                    field.schedule.Execute(() =>
                    {
                        if (!dragging || cancelling || field.labelElement.HasMouseCapture()) return;
                        dragging = false;
                        FinishEdit();
                        UpdateFields();
                    });
                });
                field.RegisterCallback<FocusInEvent>(evt =>
                {
                    if (evt.target == field) return;
                    editingText = true;
                    BeginEdit(field.value);
                });
                field.RegisterCallback<FocusOutEvent>(_ =>
                {
                    editingText = false;
                    if (!dragging && !cancelling && editingField == field) FinishEdit();
                });
                void CancelOnEscape(KeyDownEvent evt)
                {
                    if (editingField != field || cancelling || evt.keyCode != KeyCode.Escape) return;
                    // TextElement consumes Escape. Let it restore its text first, while
                    // suppressing the value/focus events that would otherwise commit it.
                    cancelling = true;
                    field.schedule.Execute(() =>
                    {
                        dragging = false;
                        if (context != null)
                        {
                            context.CancelEdit();
                            field.SetValueWithoutNotify(startingValue);
                            WriteValue(startingValue);
                        }
                        editingField = null;
                        cancelling = false;
                        UpdateFields();
                    });
                }
                field.RegisterCallback<KeyDownEvent>(CancelOnEscape, TrickleDown.TrickleDown);
                field.labelElement.RegisterCallback<KeyDownEvent>(CancelOnEscape, TrickleDown.TrickleDown);
                field.RegisterValueChangedCallback(change =>
                {
                    if (context == null || cancelling) return;
                    float value = normalize != null ? normalize(change.newValue) : change.newValue;
                    if (!value.Equals(change.newValue)) field.SetValueWithoutNotify(value);
                    source.Update();
                    if (value.Equals(source.FindProperty(property).floatValue)) return;
                    if (dragging || editingText)
                    {
                        BeginEdit(change.previousValue);
                        WriteValue(value);
                        context.Preview();
                        UpdateFields();
                    }
                    else
                    {
                        FinishEdit();
                        context.BeginEdit();
                        WriteValue(value);
                        context.Preview();
                        context.CommitEdit();
                        UpdateFields();
                    }
                });
                return field;
            }
            horizontal = Number("_horizontalSpacing", "Horizontal spacing");
            vertical = Number("_verticalSpacing", "Vertical spacing");
            angle = Number("_branchAngle", "Branch angle (0–360)", SkillTreeArrangementContext.ClampBranchAngle);
            length = Number("_lengthScale", "Branch length multiplier");
            radius = Number("_radius", "Radius");
            Number("_rotation", "Rotation");
            error = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            error.AddToClassList("skill-tree-hidden");
            root.Add(error);

            void UpdateFields()
            {
                if (context == null) return;
                if (!context.IsPreviewing)
                {
                    source.Update();
                    foreach (var number in numbers)
                        number.Field.SetValueWithoutNotify(source.FindProperty(number.Property).floatValue);
                    preset.SetValueWithoutNotify(context.Preset);
                    scope.SetValueWithoutNotify(context.Scope);
                }
                bool tree = context.Preset == SkillTreeLayoutPreset.TreeDown || context.Preset == SkillTreeLayoutPreset.TreeUp;
                bool fractal = context.Preset == SkillTreeLayoutPreset.Fractal;
                bool singleNode = context.Scope == SkillTreeArrangementScope.SelectedNodeOnly;
                horizontal.SetEnabled(context.UsesHorizontalSpacing);
                vertical.SetEnabled(tree || fractal);
                angle.SetEnabled(fractal);
                length.SetEnabled(fractal && !singleNode);
                radius.SetEnabled(context.Preset == SkillTreeLayoutPreset.Radial ||
                    context.Preset == SkillTreeLayoutPreset.Circle || context.Preset == SkillTreeLayoutPreset.CenteredCircle);
                error.text = context.Error ?? string.Empty;
                error.EnableInClassList("skill-tree-hidden", string.IsNullOrEmpty(context.Error));
            }
            var graph = context.Graph;
            void UpdateSelection(SkillTreeNode _)
            {
                if (context == null) return;
                selected.text = context.HasSelection
                    ? $"Selected: {graph.SelectedId}"
                    : "Select a node to arrange its branches.";
            }
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (graph != null) graph.Selected += UpdateSelection;
                UpdateSelection(null);
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (graph != null) graph.Selected -= UpdateSelection;
            });
            UpdateSelection(null);
            void Apply()
            {
                context.Apply();
                UpdateFields();
            }
            preset.RegisterValueChangedCallback(change =>
            {
                if (context == null) return;
                FinishEdit();
                context.BeginEdit();
                source.Update();
                source.FindProperty("_preset").enumValueIndex = (int)(SkillTreeLayoutPreset)change.newValue;
                source.ApplyModifiedPropertiesWithoutUndo();
                context.Preview();
                context.CommitEdit();
                UpdateFields();
            });
            scope.RegisterValueChangedCallback(change =>
            {
                if (context == null) return;
                FinishEdit();
                source.Update();
                source.FindProperty("_scope").enumValueIndex = (int)(SkillTreeArrangementScope)change.newValue;
                source.ApplyModifiedPropertiesWithoutUndo();
                UpdateFields();
            });
            UpdateFields();
            root.schedule.Execute(UpdateFields).Every(150);
            var actions = new VisualElement();
            actions.AddToClassList("skill-tree-actions");
            actions.Add(new Button(Apply) { text = "Arrange", name = "applyArrangement" });
            actions.Add(new Button(() =>
            {
                FinishEdit();
                SkillTreeAssetEditing.CloseContext(context.Graph);
            }) { text = "Done", name = "closeArrangement" });
            actions.Add(new Button(() => context.Graph?.Fit()) { text = "Frame tree", name = "frameTree" });
            root.Add(actions);
            root.RegisterCallback<DetachFromPanelEvent>(_ => FinishEdit());
            return root;
        }
    }
}
