using UnityEngine;

/// <summary>
/// Describes the area this scene is (the cafe, the garden...). One per area scene, on Systems.
/// Its name is shown when the player arrives, so it's always clear where they are.
/// </summary>
public class AreaInfo : MonoBehaviour
{
    [SerializeField] private string displayName = "The Cafe";
    [Tooltip("The hub that 'Return to the cafe' and getting unstuck take you back to.")]
    [SerializeField] private bool isHub;

    public static AreaInfo Current { get; private set; }

    public string DisplayName => displayName;
    public bool IsHub => isHub;
    public string SceneName => gameObject.scene.name;

    private void Awake()
    {
        Current = this;
        if (isHub) AreaTravel.HubSceneName = SceneName;
    }

    private void Start()
    {
        // On a fresh start show where we are; after travelling, AreaTravel shows it once the fade is done.
        if (!AreaTravel.Instance.IsTravelling) AreaTravel.Instance.ShowAreaName();
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
