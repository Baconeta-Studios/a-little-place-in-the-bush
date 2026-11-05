using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Add this to any object for a little light (a lantern's glow, a lit window): it brings a Light 2D with it, which
/// is off by day, fades on at dusk and night (<see cref="DayNightLighting"/>), and flickers gently. Works on placeable
/// prefabs too, so placed lanterns light up. Set the brightness, colour and size on the Light 2D.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class NightLight : MonoBehaviour
{
    [Tooltip("Also on at dusk (the evening), not just at night.")]
    [SerializeField] private bool onAtDusk = true;
    [Tooltip("How much of its brightness the flicker can take away. 0 is steady. About 0.2 for a candle, 0.08 for fairy lights.")]
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.15f;
    [Tooltip("How fast it flickers. About 3 for a candle, 1.5 for fairy lights.")]
    [SerializeField, Min(0f)] private float flickerSpeed = 3f;

    private Light2D _light;
    private float _fullIntensity;
    private float _fade;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _fullIntensity = _light.intensity;
        _light.intensity = 0f;
        _seed = Random.value * 100f; // so they don't all flicker together
    }

    private void Update()
    {
        DayNightLighting.Phase phase = DayNightLighting.CurrentPhase;
        bool on = phase == DayNightLighting.Phase.Night || (onAtDusk && phase == DayNightLighting.Phase.Dusk);
        _fade = Mathf.MoveTowards(_fade, on ? 1f : 0f, Time.unscaledDeltaTime / 1.5f);
        float flicker = 1f - flickerAmount * Mathf.PerlinNoise(_seed, Time.unscaledTime * flickerSpeed);
        _light.intensity = _fullIntensity * _fade * flicker;
        _light.enabled = _fade > 0.001f;
    }
}
