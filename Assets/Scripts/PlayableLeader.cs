public class PlayableLeader : Leader
{

    new public void NewTurn()
    {
        base.NewTurn();
        if(FindFirstObjectByType<Game>().player == this) FindFirstObjectByType<StoresManager>().RefreshStores();
    }
}