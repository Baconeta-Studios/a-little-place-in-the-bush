using UnityEngine;

/// <summary>
/// Put on Systems in every area scene, with the GameDatabase assigned. The first one to run creates
/// the <see cref="GameSession"/>; later scenes just find it already there.
/// </summary>
[DefaultExecutionOrder(-300)]
public class GameSessionBootstrap : MonoBehaviour
{
    [SerializeField] private GameDatabase database;

    private void Awake()
    {
        if (GameSession.Current) return;
        if (!database)
        {
            Debug.LogError("[GameSessionBootstrap] No GameDatabase assigned. Run Tools > Bush > Import Design Data, then assign Assets/Data/GameDatabase.", this);
            return;
        }
        GameSession.Create(database);
    }
}
