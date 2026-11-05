using Bush.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Put on a Light 2D over the path into the bush, or on a parent of one: it fades on and slowly pulses while you
/// can go foraging, and fades off while you can't (no trips left today, or someone wants a word first).
/// </summary>
public class ForageGlow : MonoBehaviour
{
    [Tooltip("How fast it pulses. Higher is quicker.")]
    [SerializeField] private float pulseSpeed = 1.2f;
    [Tooltip("How much of its brightness each pulse takes away. 0 is a steady glow.")]
    [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.35f;

    private Light2D _light;
    private float _fullIntensity;
    private float _strength;

    private void Awake()
    {
        _light = GetComponentInChildren<Light2D>(true);
        if (!_light)
        {
            Debug.LogWarning($"[ForageGlow] No Light 2D on {name} or its children.", this);
            enabled = false;
            return;
        }
        _fullIntensity = _light.intensity;
        _light.intensity = 0f;
    }

    private void Update()
    {
        GameSession session = GameSession.Current;
        bool available = session != null
                         && ForagingService.TripsLeftToday(session.Design, session.State) != 0
                         && session.Quests.ForagingWaitsFor() == null;

        _strength = Mathf.MoveTowards(_strength, available ? 1f : 0f, Time.unscaledDeltaTime / 1.5f);
        float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
        _light.intensity = _fullIntensity * _strength * pulse;
        _light.enabled = _strength > 0.001f;
    }
}
