using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades renderers near the gameplay camera so close foliage does not block the player view.
/// Attach this to the Main Camera that has the Cinemachine Brain.
/// </summary>
public class Mb_CameraProximityFade : MonoBehaviour
{
    private const int DEFAULT_COLLIDER_BUFFER_SIZE = 64;
    private const string COLOR_PROPERTY = "_Color";
    private const string BASE_COLOR_PROPERTY = "_BaseColor";
    private const string TINT_COLOR_PROPERTY = "_TintColor";
    private const string MODE_PROPERTY = "_Mode";
    private const string SRC_BLEND_PROPERTY = "_SrcBlend";
    private const string DST_BLEND_PROPERTY = "_DstBlend";
    private const string Z_WRITE_PROPERTY = "_ZWrite";
    private const string RENDER_TYPE_TAG = "RenderType";
    private const string TRANSPARENT_RENDER_TYPE = "Transparent";

    [Header("Detection")]
    [Tooltip("Broad physics query layers. Keep this as Default if environment objects stay on Default.")]
    [SerializeField] private LayerMask FadeLayers = 0;

    [Tooltip("Only objects with this tag, or with a parent using this tag, are faded. Leave empty to fade every object in Fade Layers.")]
    [SerializeField] private string FadeTag = "CameraFade";

    [Tooltip("Radius around the camera that fades matching objects.")]
    [SerializeField] private float FadeRadius = 1.75f;

    [Tooltip("How often the overlap check runs. Lower values react faster but check more often.")]
    [SerializeField] private float CheckInterval = 0.05f;

    [Tooltip("Maximum colliders the fade query can process per check.")]
    [SerializeField] private int ColliderBufferSize = DEFAULT_COLLIDER_BUFFER_SIZE;

    [Header("Fade")]
    [Range(0f, 1f)]
    [Tooltip("Lowest opacity applied to objects inside the fade radius.")]
    [SerializeField] private float MinimumOpacity = 0.25f;

    [Tooltip("How quickly objects fade in and out.")]
    [SerializeField] private float FadeSpeed = 8f;

    private readonly Dictionary<Renderer, FadedRenderer> _fadedRenderers = new Dictionary<Renderer, FadedRenderer>();
    private readonly HashSet<Renderer> _detectedRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> _renderersToRelease = new List<Renderer>();

    private Collider[] _colliderBuffer = new Collider[DEFAULT_COLLIDER_BUFFER_SIZE];
    private float _nextCheckTime;

    private void Awake()
    {
        EnsureColliderBuffer();
    }

    private void OnValidate()
    {
        FadeRadius = Mathf.Max(0.1f, FadeRadius);
        CheckInterval = Mathf.Max(0.01f, CheckInterval);
        ColliderBufferSize = Mathf.Max(1, ColliderBufferSize);
        MinimumOpacity = Mathf.Clamp01(MinimumOpacity);
        FadeSpeed = Mathf.Max(0.1f, FadeSpeed);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextCheckTime)
        {
            RefreshDetectedRenderers();
            _nextCheckTime = Time.unscaledTime + CheckInterval;
        }

        UpdateFades();
    }

    private void OnDisable()
    {
        RestoreAllRenderers();
    }

    private void RefreshDetectedRenderers()
    {
        EnsureColliderBuffer();

        _detectedRenderers.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            FadeRadius,
            _colliderBuffer,
            FadeLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _colliderBuffer[i];
            if (hit == null) continue;

            AddRenderersFromCollider(hit);
        }

        foreach (Renderer renderer in _detectedRenderers)
        {
            if (renderer == null || _fadedRenderers.ContainsKey(renderer)) continue;

            _fadedRenderers.Add(renderer, new FadedRenderer(renderer));
        }

        _renderersToRelease.Clear();

        foreach (KeyValuePair<Renderer, FadedRenderer> pair in _fadedRenderers)
        {
            Renderer renderer = pair.Key;
            if (renderer == null || !_detectedRenderers.Contains(renderer))
                _renderersToRelease.Add(renderer);
        }

        for (int i = 0; i < _renderersToRelease.Count; i++)
        {
            Renderer renderer = _renderersToRelease[i];
            if (renderer == null)
            {
                _fadedRenderers.Remove(renderer);
                continue;
            }

            _fadedRenderers[renderer].SetTargetOpacity(1f);
        }
    }

    private void UpdateFades()
    {
        _renderersToRelease.Clear();

        foreach (KeyValuePair<Renderer, FadedRenderer> pair in _fadedRenderers)
        {
            Renderer renderer = pair.Key;
            FadedRenderer fadedRenderer = pair.Value;

            if (renderer == null)
            {
                _renderersToRelease.Add(renderer);
                continue;
            }

            float targetOpacity = _detectedRenderers.Contains(renderer) ? MinimumOpacity : 1f;
            fadedRenderer.SetTargetOpacity(targetOpacity);
            fadedRenderer.UpdateOpacity(FadeSpeed, Time.unscaledDeltaTime);

            if (!fadedRenderer.IsFading && Mathf.Approximately(fadedRenderer.CurrentOpacity, 1f))
            {
                fadedRenderer.Restore();
                _renderersToRelease.Add(renderer);
            }
        }

        for (int i = 0; i < _renderersToRelease.Count; i++)
        {
            _fadedRenderers.Remove(_renderersToRelease[i]);
        }
    }

    private void AddRenderersFromCollider(Collider hit)
    {
        Transform fadeRoot = ResolveFadeRoot(hit.transform);
        if (fadeRoot == null) return;

        Renderer parentRenderer = fadeRoot.GetComponent<Renderer>();
        if (parentRenderer != null)
            _detectedRenderers.Add(parentRenderer);

        Renderer[] childRenderers = fadeRoot.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (childRenderers[i] != null)
                _detectedRenderers.Add(childRenderers[i]);
        }
    }

    private Transform ResolveFadeRoot(Transform hitTransform)
    {
        if (hitTransform == null) return null;

        if (string.IsNullOrWhiteSpace(FadeTag))
            return hitTransform;

        Transform current = hitTransform;
        Transform taggedRoot = null;

        while (current != null)
        {
            if (current.gameObject.tag == FadeTag)
                taggedRoot = current;

            current = current.parent;
        }

        return taggedRoot;
    }

    private void EnsureColliderBuffer()
    {
        if (_colliderBuffer != null && _colliderBuffer.Length == ColliderBufferSize) return;

        _colliderBuffer = new Collider[ColliderBufferSize];
    }

    private void RestoreAllRenderers()
    {
        foreach (KeyValuePair<Renderer, FadedRenderer> pair in _fadedRenderers)
        {
            pair.Value?.Restore();
        }

        _fadedRenderers.Clear();
        _detectedRenderers.Clear();
        _renderersToRelease.Clear();
    }

    private sealed class FadedRenderer
    {
        private readonly Renderer _renderer;
        private readonly Material[] _originalMaterials;
        private readonly Material[] _runtimeMaterials;
        private readonly MaterialState[] _originalStates;

        private float _targetOpacity;

        public float CurrentOpacity { get; private set; } = 1f;
        public bool IsFading => !Mathf.Approximately(CurrentOpacity, _targetOpacity);

        public FadedRenderer(Renderer renderer)
        {
            _renderer = renderer;
            _originalMaterials = renderer.sharedMaterials;
            _runtimeMaterials = renderer.materials;
            _originalStates = new MaterialState[_runtimeMaterials.Length];
            _targetOpacity = 1f;

            for (int i = 0; i < _runtimeMaterials.Length; i++)
            {
                Material material = _runtimeMaterials[i];
                _originalStates[i] = new MaterialState(material);
                ConfigureMaterialForTransparency(material);
            }
        }

        public void SetTargetOpacity(float targetOpacity)
        {
            _targetOpacity = Mathf.Clamp01(targetOpacity);
        }

        public void UpdateOpacity(float fadeSpeed, float deltaTime)
        {
            CurrentOpacity = Mathf.MoveTowards(CurrentOpacity, _targetOpacity, fadeSpeed * deltaTime);

            for (int i = 0; i < _runtimeMaterials.Length; i++)
            {
                ApplyOpacity(_runtimeMaterials[i], _originalStates[i], CurrentOpacity);
            }
        }

        public void Restore()
        {
            if (_renderer != null)
                _renderer.sharedMaterials = _originalMaterials;

            for (int i = 0; i < _runtimeMaterials.Length; i++)
            {
                if (_runtimeMaterials[i] != null)
                    Object.Destroy(_runtimeMaterials[i]);
            }
        }

        private static void ConfigureMaterialForTransparency(Material material)
        {
            if (material == null) return;

            material.SetOverrideTag(RENDER_TYPE_TAG, TRANSPARENT_RENDER_TYPE);

            if (material.HasProperty(MODE_PROPERTY))
                material.SetFloat(MODE_PROPERTY, 2f);

            if (material.HasProperty(SRC_BLEND_PROPERTY))
                material.SetFloat(SRC_BLEND_PROPERTY, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty(DST_BLEND_PROPERTY))
                material.SetFloat(DST_BLEND_PROPERTY, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty(Z_WRITE_PROPERTY))
                material.SetFloat(Z_WRITE_PROPERTY, 0f);

            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void ApplyOpacity(Material material, MaterialState originalState, float opacity)
        {
            if (material == null) return;

            SetColorOpacity(material, COLOR_PROPERTY, originalState.Color, opacity);
            SetColorOpacity(material, BASE_COLOR_PROPERTY, originalState.BaseColor, opacity);
            SetColorOpacity(material, TINT_COLOR_PROPERTY, originalState.TintColor, opacity);
        }

        private static void SetColorOpacity(Material material, string propertyName, Color originalColor, float opacity)
        {
            if (!material.HasProperty(propertyName)) return;

            Color color = originalColor;
            color.a *= opacity;
            material.SetColor(propertyName, color);
        }
    }

    private readonly struct MaterialState
    {
        public readonly Color Color;
        public readonly Color BaseColor;
        public readonly Color TintColor;

        public MaterialState(Material material)
        {
            Color = GetColor(material, COLOR_PROPERTY);
            BaseColor = GetColor(material, BASE_COLOR_PROPERTY);
            TintColor = GetColor(material, TINT_COLOR_PROPERTY);
        }

        private static Color GetColor(Material material, string propertyName)
        {
            if (material != null && material.HasProperty(propertyName))
                return material.GetColor(propertyName);

            return Color.white;
        }
    }
}
