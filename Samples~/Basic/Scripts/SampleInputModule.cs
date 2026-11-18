using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && FATLARVA_SKILLTREE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Game.SkillTree.Samples
{
    [RequireComponent(typeof(EventSystem))]
    public sealed class SampleInputModule : MonoBehaviour
    {
        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM && FATLARVA_SKILLTREE_INPUT_SYSTEM
            gameObject.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
            gameObject.AddComponent<StandaloneInputModule>();
#else
            Debug.LogError("Enable an input backend in Active Input Handling to use this sample.", this);
#endif
        }
    }
}
