using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Pulses the alpha of an authored border Image to signal the active environmental card. Which
// image and where it sits is entirely up to what you assign/position in the Inspector — this
// script only drives the alpha pulse, it never builds or moves anything.
public class CardEnvironmentalPulseEffect : MonoBehaviour
{
    [Tooltip("Border Image to pulse. Position, size and sprite are whatever this Image is authored as.")]
    [SerializeField] private Image pulseImage;
    [SerializeField] private Color glowColor = new Color(0.4f, 0.85f, 1f, 1f);
    [SerializeField] private float pulseSpeed = 1.1f;
    [SerializeField] private float alphaMin = 0.12f;
    [SerializeField] private float alphaMax = 0.60f;

    private Coroutine pulseCoroutine;

    private void Awake()
    {
        if (pulseImage != null) pulseImage.raycastTarget = false;
    }

    private void OnEnable()
    {
        if (pulseCoroutine == null)            pulseCoroutine = StartCoroutine(PulseLoop());
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        if (pulseImage != null)
            pulseImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
    }

    private IEnumerator PulseLoop()
    {
        while (true)
        {
            float alpha = Mathf.Lerp(alphaMin, alphaMax, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
            if (pulseImage != null)
                pulseImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
            yield return null;
        }
    }
}
