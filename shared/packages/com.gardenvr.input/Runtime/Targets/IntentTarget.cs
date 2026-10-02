using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>
    /// Stable id for a lookable or pokeable object (<c>jar</c>, <c>seed.walk</c>, <c>pebble.settings</c>).
    /// Inactive or disabled targets unregister, so raycasts miss them and Tab skips them.
    /// </summary>
    public sealed class IntentTarget : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] Collider hitCollider;
        [SerializeField] bool pokeOnly;

        public string Id
        {
            get => id;
            set
            {
                if (id == value) return;
                if (isActiveAndEnabled) IntentTargetRegistry.Unregister(this);
                id = value;
                if (isActiveAndEnabled) IntentTargetRegistry.Register(this);
            }
        }

        /// <summary>A short click or Enter on this target is a Poke instead of a Pinch.</summary>
        public bool PokeOnly
        {
            get => pokeOnly;
            set => pokeOnly = value;
        }

        /// <summary>Collider used for rays. The serialized collider wins; otherwise the collider on this object.</summary>
        public Collider HitCollider => hitCollider != null ? hitCollider : GetComponent<Collider>();

        void OnEnable()
        {
            IntentTargetRegistry.Register(this);
        }

        void OnDisable()
        {
            IntentTargetRegistry.Unregister(this);
        }
    }
}
