using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Put on a Light 2D (a lantern's glow, a lit window): it's off by day and fades on at dusk and night
/// (<see cref="DayNightLighting"/>). Works on placeable prefabs too, so placed lanterns light up.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class NightLight : MonoBehaviour
{
    [Tooltip("Also on at dusk (the evening), not just at night.")]
    [SerializeField] private bool onAtDusk = true;

    private Light2D _light;
    private float _fullIntensity;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _fullIntensity = _light.intensity;
        _light.intensity = 0f;
    }

    private void Update()
    {
        DayNightLighting.Phase phase = DayNightLighting.CurrentPhase;
        bool on = phase == DayNightLighting.Phase.Night || (onAtDusk && phase == DayNightLighting.Phase.Dusk);
        _light.intensity = Mathf.MoveTowards(_light.intensity, on ? _fullIntensity : 0f, _fullIntensity * Time.unscaledDeltaTime / 1.5f);
        _light.enabled = _light.intensity > 0.001f;
    }
}
