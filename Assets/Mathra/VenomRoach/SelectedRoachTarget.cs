using UnityEngine;

namespace Mathra.Enemies
{
    // Enable this component on the controlled hero when the party leader changes.
    public sealed class SelectedRoachTarget : MonoBehaviour
    {
        public static Transform Current { get; private set; }
        public static void Select(Transform hero) => Current = hero;
        void OnEnable() => Select(transform);
        void OnDisable() { if (Current == transform) Select(null); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSelection() => Select(null);
    }
}
