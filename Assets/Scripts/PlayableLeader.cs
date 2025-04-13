public class PlayableLeader : Leader
{
    override public void Killed(Leader killedBy, bool onlyMask = false)
    {
        if (killed) return;

        FindFirstObjectByType<PlayableLeaderIcons>().AddDeadIcon(this);

        health = 0;
        killed = true;

        if (FindFirstObjectByType<Game>().player == this)
        {
            FindFirstObjectByType<Game>().EndGame(false);
            return;
        }

        FindFirstObjectByType<Game>().competitors.Remove(this);

        base.Killed(killedBy);
    }
    new public void NewTurn()
    {

        FindFirstObjectByType<PlayableLeaderIcons>().HighlightCurrentlyPlaying(this);

        base.NewTurn();
    }
}