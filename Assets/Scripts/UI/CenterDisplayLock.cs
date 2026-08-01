using System.Collections;
using System.Threading;
using System.Threading.Tasks;

// Global mutual-exclusion gate for exclusive full-screen center displays: the turn banner
// (TurnBanner), the opportunity/situation card tray (SituationCardsUI), and the PC/region
// grant card-preview+flight sequence (Board.TriggerOwnPcGrantIfStandingOnOne /
// TriggerRegionLandGrant). Only one of these may be visible at a time — whichever wants to
// show acquires this and holds it for its whole visible lifetime, not just its own
// synchronous "start showing" call, releasing only once it's fully gone.
public static class CenterDisplayLock
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    // For async/await callers.
    public static Task WaitAsync() => gate.WaitAsync();

    // For Unity coroutines — SemaphoreSlim has no coroutine-friendly await, so poll an
    // atomic zero-timeout acquire attempt once per frame instead.
    public static IEnumerator WaitCoroutine()
    {
        while (!gate.Wait(0)) yield return null;
    }

    public static void Release() => gate.Release();
}
