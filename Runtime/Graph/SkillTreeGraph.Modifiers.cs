namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private ISkillTreeModifiers _modifiers;

        /// <summary>
        /// Overrides the editing modifiers for this graph. Assign null to use the installed input adapter.
        /// </summary>
        public ISkillTreeModifiers Modifiers
        {
            get => _modifiers ?? SkillTreeModifierDefaults.Current;
            set => _modifiers = value;
        }
    }
}
