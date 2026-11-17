using System;
using UnityEngine;

namespace Game.SkillTree.Data
{
    public abstract class SkillTreeConfigAsset : ScriptableObject
    {
        [NonSerialized]
        private int _revision;

        public int Revision => _revision;

        public static event Action<SkillTreeConfigAsset> Changed;

        public void NotifyChanged()
        {
            unchecked
            {
                _revision++;
            }
            Changed?.Invoke(this);
        }

        protected virtual void OnValidate()
        {
            NotifyChanged();
        }
    }
}
