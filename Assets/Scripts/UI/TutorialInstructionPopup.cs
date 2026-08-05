using UnityEngine;
using UnityEngine.UI;

public class TutorialInstructionPopup : MonoBehaviour
{
    [SerializeField] private string tutorialId;
    [SerializeField] private int sequenceOrder;
    [SerializeField] private Button closeButton;

    // The ScrollableText child (Content text inside the nested scroll view) already carries
    // its own TypewriterEffect. Its Start() auto-captures whatever text is baked into the TMP
    // field and blanks the display, waiting for someone to call StartWriting() — nothing did,
    // so the scroll view rendered empty regardless of authored content. Found via
    // GetComponentInChildren rather than a serialized field so every existing instance picks
    // this up without needing a per-prefab-instance wiring pass.
    private TypewriterEffect scrollTextTypewriter;

    public string TutorialId => string.IsNullOrWhiteSpace(tutorialId)
        ? TutorialInstructionsManager.BuildId(gameObject)
        : tutorialId;

    public int SequenceOrder => sequenceOrder;

    private void Awake()
    {
        if (closeButton == null)
        {
            Transform closeTransform = transform.Find("Content/Image/Close");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }

        if (closeButton == null)
        {
            Debug.LogError($"{name} has no tutorial instruction close button.", this);
            return;
        }

        closeButton.onClick.AddListener(Close);

        scrollTextTypewriter = GetComponentInChildren<TypewriterEffect>(true);
    }

    // Runs every time the popup is shown (unlike Start, which only fires once on first
    // activation), so the scroll text re-types on every OpenNext(), not just the first. Reads
    // the TMP text itself rather than relying on TypewriterEffect's own Start() to have already
    // captured it — OnEnable can race ahead of a sibling component's Start() on first
    // activation, which would otherwise let TypewriterEffect wipe the text a frame after this
    // already started typing it.
    private void OnEnable()
    {
        if (scrollTextTypewriter == null || scrollTextTypewriter.textMeshPro == null) return;

        string text = string.IsNullOrEmpty(scrollTextTypewriter.fullText)
            ? scrollTextTypewriter.textMeshPro.text
            : scrollTextTypewriter.fullText;
        scrollTextTypewriter.fullText = text;
        scrollTextTypewriter.StartWriting(text);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    public void Close()
    {
        Close(null);
    }

    public void Close(GameObject activateAfterClose)
    {
        TutorialInstructionsManager.Instance.Close(this, activateAfterClose);
    }
}
