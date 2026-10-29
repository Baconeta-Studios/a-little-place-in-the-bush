/// <summary>
/// A spot in the world to go foraging from (a path into the bush at the side of the cafe, later a
/// garden gate). Click it, the gecko walks over, and the forage screen opens.
/// Set it up like any interactable: a Collider2D on the Interactable layer and a StandPoint child.
/// </summary>
public class ForageSpot : Interactable
{
    public override string Prompt => "Go foraging";

    public override void Interact(in InteractionContext context)
    {
        base.Interact(context);
        ForagePopup.Show();
    }
}
