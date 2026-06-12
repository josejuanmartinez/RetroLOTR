using System.Collections;
using UnityEngine;

// Plays a character's animator controller on a character sprite renderer (hex icon or board mover).
// The animation clips bind m_Sprite with an empty path, so the Animator must live
// on the same GameObject as the SpriteRenderer it drives.
public class CharacterAnimationController : MonoBehaviour
{
    private static readonly int ActionParam = Animator.StringToHash("action");
    private static readonly int ForwardParam = Animator.StringToHash("forward");
    private static readonly int LeftParam = Animator.StringToHash("left");
    private static readonly int RightParam = Animator.StringToHash("right");
    private static readonly int BackParam = Animator.StringToHash("back");
    private static readonly int FidgetParam = Animator.StringToHash("fidget");
    private const float VerticalMovementEpsilon = 0.01f;
    private const float FidgetMinDelay = 5f;
    private const float FidgetMaxDelay = 10f;

    private Animator animator;
    private Coroutine fidgetCoroutine;

    private void Awake()
    {
        EnsureAnimator();
    }

    private void Update()
    {
        if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null) return;

        // Clear each bool once its state starts playing so the controller returns
        // to Idle afterwards instead of retriggering the same animation.
        ClearParamWhenStateReached(ActionParam, "Action");
        ClearParamWhenStateReached(ForwardParam, "Forward");
        ClearParamWhenStateReached(LeftParam, "Left");
        ClearParamWhenStateReached(RightParam, "Right");
        ClearParamWhenStateReached(BackParam, "Back");
        ClearParamWhenStateReached(FidgetParam, "Fidget");
    }

    private void ClearParamWhenStateReached(int param, string stateName)
    {
        if (!animator.GetBool(param)) return;
        bool reached = animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName));
        if (reached) animator.SetBool(param, false);
    }

    private void EnsureAnimator()
    {
        if (animator != null) return;
        animator = GetComponent<Animator>();
        if (animator == null) animator = gameObject.AddComponent<Animator>();
        animator.enabled = false;
    }

    public bool Show(Character character)
    {
        RuntimeAnimatorController controller = character != null ? character.GetAnimatorController() : null;
        if (controller == null)
        {
            Clear();
            return false;
        }

        EnsureAnimator();
        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
        }
        animator.enabled = true;
        StartFidgetLoop();
        return true;
    }

    private void StartFidgetLoop()
    {
        if (fidgetCoroutine != null) StopCoroutine(fidgetCoroutine);
        fidgetCoroutine = StartCoroutine(FidgetLoop());
    }

    private IEnumerator FidgetLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(FidgetMinDelay, FidgetMaxDelay));
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null) continue;

            bool anyActive = animator.GetBool(ActionParam) || animator.GetBool(ForwardParam)
                || animator.GetBool(LeftParam) || animator.GetBool(RightParam) || animator.GetBool(BackParam);
            bool inIdle = animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0);

            if (inIdle && !anyActive)
                animator.SetBool(FidgetParam, true);
        }
    }

    // worldDelta is the move in world space: horizontal moves play the side walk,
    // upward moves show the character's back, downward moves walk toward the viewer.
    public bool PlayMovement(Character character, Vector3 worldDelta)
    {
        if (!gameObject.activeInHierarchy) return false;
        if (!Show(character)) return false;
        ResetParams();
        animator.SetBool(ResolveDirectionParam(worldDelta), true);
        return true;
    }

    public bool PlayAction(Character character)
    {
        if (!gameObject.activeInHierarchy) return false;
        if (!Show(character)) return false;
        ResetParams();
        animator.SetBool(ActionParam, true);
        return true;
    }

    private static int ResolveDirectionParam(Vector3 worldDelta)
    {
        if (worldDelta.y > VerticalMovementEpsilon) return BackParam;
        if (worldDelta.y < -VerticalMovementEpsilon) return ForwardParam;
        return worldDelta.x < 0f ? LeftParam : RightParam;
    }

    private void ResetParams()
    {
        animator.SetBool(ActionParam, false);
        animator.SetBool(ForwardParam, false);
        animator.SetBool(LeftParam, false);
        animator.SetBool(RightParam, false);
        animator.SetBool(BackParam, false);
        animator.SetBool(FidgetParam, false);
    }

    public void Clear()
    {
        if (fidgetCoroutine != null)
        {
            StopCoroutine(fidgetCoroutine);
            fidgetCoroutine = null;
        }
        if (animator == null) return;
        if (animator.enabled && animator.runtimeAnimatorController != null) ResetParams();
        animator.enabled = false;
        animator.runtimeAnimatorController = null;
    }
}
