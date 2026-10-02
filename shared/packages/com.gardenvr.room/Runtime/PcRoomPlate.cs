using UnityEngine;

namespace GardenVR.Room
{
    /// <summary>
    /// Large curved room card parented to the eye camera, drawn with Fidelity/Plate.
    /// <see cref="PcOnly"/> stays on for PC. The Quest phase turns it off so the card never renders there.
    /// Batch captures do not enter play mode, so the saved material keeps the full exposure.
    /// Play mode calls <see cref="Show"/> from Start and fades up from black.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PcRoomPlate : MonoBehaviour, IRoomProvider
    {
        public const float DefaultFadeSeconds = 2f;
        public const float DefaultExposure = 1f;

        [SerializeField] Texture2D _plate;
        [SerializeField] float _exposure = DefaultExposure;
        [SerializeField] float _fadeSeconds = DefaultFadeSeconds;
        [SerializeField] bool _pcOnly = true;
        [SerializeField] bool _playIntro = true;
        [SerializeField] Renderer _renderer;

        float _dim;
        float _fade = 1f;
        float _fadeElapsed;
        bool _fading;

        public Texture2D Plate
        {
            get => _plate;
            set
            {
                _plate = value;
                ApplyExposure();
            }
        }

        public float Exposure
        {
            get => _exposure;
            set
            {
                _exposure = Mathf.Max(0f, value);
                ApplyExposure();
            }
        }

        public float FadeSeconds
        {
            get => _fadeSeconds;
            set => _fadeSeconds = Mathf.Max(0f, value);
        }

        /// <summary>PC renders the plate. Quest turns this off.</summary>
        public bool PcOnly
        {
            get => _pcOnly;
            set
            {
                _pcOnly = value;
                ApplyExposure();
            }
        }

        public float CurrentExposure { get; private set; }

        public void Show(float fadeSeconds)
        {
            if (!_pcOnly)
            {
                _fading = false;
                _fade = 0f;
                ApplyExposure();
                return;
            }

            if (fadeSeconds <= 0.0001f)
            {
                _fading = false;
                _fade = 1f;
                ApplyExposure();
                return;
            }

            _fadeSeconds = fadeSeconds;
            _fadeElapsed = 0f;
            _fade = 0f;
            _fading = true;
            ApplyExposure();
        }

        public void Dim(float amount)
        {
            _dim = Mathf.Clamp01(amount);
            ApplyExposure();
        }

        /// <summary>Advance the fade. EditMode tests call this. Play mode calls it from Update.</summary>
        public void Tick(float dt)
        {
            if (!_fading) return;
            _fadeElapsed += Mathf.Max(0f, dt);
            float duration = _fadeSeconds <= 0f ? 0f : _fadeSeconds;
            _fade = duration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / duration);
            if (_fade >= 1f) _fading = false;
            ApplyExposure();
        }

        void Awake()
        {
            ResolveRenderer();
        }

        void Start()
        {
            if (_playIntro) Show(_fadeSeconds);
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        void ResolveRenderer()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
        }

        void ApplyExposure()
        {
            ResolveRenderer();
            float shown = _pcOnly ? _exposure * _fade * (1f - Mathf.Clamp01(_dim)) : 0f;
            CurrentExposure = shown;
            if (_renderer == null) return;
            _renderer.enabled = _pcOnly;
            Material material = Application.isPlaying ? _renderer.material : _renderer.sharedMaterial;
            if (material == null) return;
            if (_plate != null) material.SetTexture("_MainTex", _plate);
            material.SetFloat("_Exposure", shown);
        }
    }
}
