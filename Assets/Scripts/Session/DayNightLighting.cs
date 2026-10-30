using Bush.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lights the scene for the time of day: the scene's Global Light 2D as you set it by day, warmer at
/// dusk (the evening, once the day's foraging is done) and a deep blue at night. Eases between them,
/// and holds while a day ends (<see cref="DayTransition"/>) so the change happens behind the fade.
/// <see cref="NightLight"/>s (lanterns, windows) switch on at dusk and night. Created by the HUD,
/// so no setup is needed beyond the Global Light 2D every URP 2D scene has.
/// </summary>
public class DayNightLighting : MonoBehaviour
{
    public enum Phase
    {
        Day,
        Dusk,
        Night
    }

    private static readonly Color DuskTint = new(1f, 0.82f, 0.68f);
    private const float DuskIntensity = 0.9f;
    private static readonly Color NightTint = new(0.5f, 0.56f, 0.9f);
    private const float NightIntensity = 0.5f;
    private const float EaseSeconds = 1.5f;

    private static DayNightLighting _instance;

    private Light2D _global;
    private Color _dayColor = Color.white;
    private float _dayIntensity = 1f;
    private bool _warned;

    /// <summary>The time of day the world currently shows (lags the rules while a day is ending).</summary>
    public static Phase CurrentPhase { get; private set; } = Phase.Day;

    public static void Ensure()
    {
        if (_instance) return;
        _instance = FindAnyObjectByType<DayNightLighting>();
        if (!_instance) _instance = new GameObject("[DayNightLighting]").AddComponent<DayNightLighting>();
    }

    /// <summary>Jumps straight to the current time of day (while the screen is black).</summary>
    public static void SnapNow()
    {
        if (!_instance) return;
        CurrentPhase = PhaseNow();
        _instance.Apply(1f);
    }

    private void Start()
    {
        FindGlobalLight();
        CurrentPhase = PhaseNow();
        Apply(1f);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!DayTransition.Holding) CurrentPhase = PhaseNow();
        Apply(Time.unscaledDeltaTime / EaseSeconds);
    }

    /// <summary>What the rules say it is: night, dusk (evening, the day's trips done) or day.</summary>
    public static Phase PhaseNow()
    {
        GameSession session = GameSession.Current;
        if (session == null) return Phase.Day;
        if (session.State.isNight) return Phase.Night;
        DayRules rules = session.Design.rules;
        return rules.TripsPerDay(session.State) > 0 && rules.IsEvening(session.State) ? Phase.Dusk : Phase.Day;
    }

    private void Apply(float step)
    {
        if (!_global && !FindGlobalLight()) return;
        (Color color, float intensity) = CurrentPhase switch
        {
            Phase.Night => (_dayColor * NightTint, _dayIntensity * NightIntensity),
            Phase.Dusk => (_dayColor * DuskTint, _dayIntensity * DuskIntensity),
            _ => (_dayColor, _dayIntensity)
        };
        _global.color = Color.Lerp(_global.color, color, Mathf.Clamp01(step));
        _global.intensity = Mathf.Lerp(_global.intensity, intensity, Mathf.Clamp01(step));
    }

    private bool FindGlobalLight()
    {
        foreach (Light2D light in FindObjectsByType<Light2D>())
        {
            if (light.lightType != Light2D.LightType.Global) continue;
            _global = light;
            _dayColor = light.color; // what you set in the scene is the daytime look
            _dayIntensity = light.intensity;
            return true;
        }
        if (!_warned) Debug.LogWarning("[DayNightLighting] No Global Light 2D in the scene, so there's no day and night lighting.");
        _warned = true;
        return false;
    }
}
