/// <summary>
/// The little sign on the tree by the cafe door: click it to read what kind of cafe you've made and
/// how cosy it is (<see cref="CafeSignPopup"/>). Set it up like any interactable: a Collider2D on the
/// Interactable layer and a StandPoint child.
/// </summary>
public class CafeSign : Interactable
{
    public override string Prompt => "Read the sign";

    public override void Interact(in InteractionContext context)
    {
        base.Interact(context);
        CafeSignPopup.Show();
    }
}
