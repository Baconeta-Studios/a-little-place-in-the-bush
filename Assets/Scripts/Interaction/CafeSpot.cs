/// <summary>
/// The cafe door (or counter): click it, the gecko walks over, and the menu board opens
/// (<see cref="CafePopup"/>). Set it up like any interactable: a Collider2D on the Interactable
/// layer and a StandPoint child.
/// </summary>
public class CafeSpot : Interactable
{
    public override string Prompt => "Run the cafe";

    public override void Interact(in InteractionContext context)
    {
        base.Interact(context);
        CafePopup.Show();
    }
}
