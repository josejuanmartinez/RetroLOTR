using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CircularCardCarousel : MonoBehaviour
{
    private struct CarouselVisualState
    {
        public Vector2 position;
        public Vector3 scale;
        public float alpha;
        public float rotationZ;
        public int siblingIndex;
        public bool visible;
    }

    [Header("References")]
    [SerializeField] private List<GameObject> items = new();
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RectTransform wheelArea;

    [Header("Layout")]
    [SerializeField] private Vector2 centerPosition = Vector2.zero;
    [SerializeField] private Vector2 previousPosition = new(-250f, 0f);
    [SerializeField] private Vector2 nextPosition = new(250f, 0f);
    [SerializeField] private Vector2 previousSecondPosition = new(-500f, 0f);
    [SerializeField] private Vector2 nextSecondPosition = new(500f, 0f);
    [SerializeField] private Vector2 previousThirdPosition = new(-750f, 0f);
    [SerializeField] private Vector2 nextThirdPosition = new(750f, 0f);
    [SerializeField] private Vector3 selectedScale = Vector3.one;
    [SerializeField] private Vector3 sideScale = new(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 secondSideScale = new(0.65f, 0.65f, 0.65f);
    [SerializeField] private Vector3 thirdSideScale = new(0.5f, 0.5f, 0.5f);

    [Header("Presentation")]
    [SerializeField] private float arcHeight = 70f;
    [SerializeField] private float sideTiltAngle = 8f;
    [SerializeField] private float secondTiltAngle = 15f;
    [SerializeField] private float thirdTiltAngle = 22f;
    [SerializeField] private float centerAlpha = 1f;
    [SerializeField] private float sideAlpha = 0.82f;
    [SerializeField] private float secondSideAlpha = 0.58f;
    [SerializeField] private float thirdSideAlpha = 0.35f;
    [SerializeField] private float moveLerpSpeed = 10f;
    [SerializeField] private float scaleLerpSpeed = 10f;
    [SerializeField] private float fadeLerpSpeed = 12f;
    [SerializeField] private float rotateLerpSpeed = 10f;

    [Header("Input")]
    [SerializeField] private bool useMouseWheel = true;
    [SerializeField] private bool requirePointerOverAreaForWheel = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onSelectionChanged;

    private int currentIndex;
    private Canvas parentCanvas;
    private readonly Dictionary<GameObject, CarouselVisualState> targetStates = new();
    private readonly Dictionary<GameObject, CanvasGroup> canvasGroups = new();

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (wheelArea == null)
        {
            wheelArea = transform as RectTransform;
        }
    }

    private void OnEnable()
    {
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(ShowPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(ShowNext);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(ShowPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNext);
        }
    }

    private void Update()
    {
        AnimateItems();

        if (!useMouseWheel || items.Count <= 1)
        {
            return;
        }

        if (requirePointerOverAreaForWheel && !IsPointerOverWheelArea())
        {
            return;
        }

        float wheelDelta = Input.mouseScrollDelta.y;
        if (wheelDelta > 0f)
        {
            ShowPrevious();
        }
        else if (wheelDelta < 0f)
        {
            ShowNext();
        }
    }

    public void Refresh()
    {
        int validCount = items.Count;
        if (validCount == 0)
        {
            return;
        }

        currentIndex = WrapIndex(currentIndex);

        for (int i = 0; i < validCount; i++)
        {
            if (items[i] == null)
            {
                continue;
            }

            EnsureCanvasGroup(items[i]);
            targetStates[items[i]] = new CarouselVisualState
            {
                position = (items[i].transform as RectTransform)?.anchoredPosition ?? Vector2.zero,
                scale = Vector3.one * 0.35f,
                alpha = 0f,
                rotationZ = 0f,
                siblingIndex = 0,
                visible = false
            };
            items[i].SetActive(true);
        }

        List<int> visibleIndices = new() { currentIndex };
        for (int offset = 1; offset <= 3; offset++)
        {
            int previousIndex = WrapIndex(currentIndex - offset);
            if (!visibleIndices.Contains(previousIndex))
            {
                visibleIndices.Add(previousIndex);
            }

            int nextIndex = WrapIndex(currentIndex + offset);
            if (!visibleIndices.Contains(nextIndex))
            {
                visibleIndices.Add(nextIndex);
            }
        }

        GameObject currentItem = items[currentIndex];
        SetupItem(currentItem, GetPositionForOffset(0), selectedScale, 3, GetAlphaForOffset(0), GetRotationForOffset(0));

        for (int offset = 1; offset <= 3; offset++)
        {
            int previousIndex = WrapIndex(currentIndex - offset);
            if (visibleIndices.Contains(previousIndex))
            {
                SetupItem(items[previousIndex], GetPositionForOffset(-offset), GetScaleForOffset(offset), 3 - offset, GetAlphaForOffset(offset), GetRotationForOffset(-offset));
                visibleIndices.Remove(previousIndex);
            }

            int nextIndex = WrapIndex(currentIndex + offset);
            if (visibleIndices.Contains(nextIndex))
            {
                SetupItem(items[nextIndex], GetPositionForOffset(offset), GetScaleForOffset(offset), 3 + offset, GetAlphaForOffset(offset), GetRotationForOffset(offset));
                visibleIndices.Remove(nextIndex);
            }
        }

        onSelectionChanged?.Invoke(currentIndex);
    }

    public void ShowNext()
    {
        if (items.Count == 0)
        {
            return;
        }

        currentIndex = WrapIndex(currentIndex + 1);
        Refresh();
    }

    public void ShowPrevious()
    {
        if (items.Count == 0)
        {
            return;
        }

        currentIndex = WrapIndex(currentIndex - 1);
        Refresh();
    }

    public void SetIndex(int index)
    {
        if (items.Count == 0)
        {
            return;
        }

        currentIndex = WrapIndex(index);
        Refresh();
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public void ClearItems()
    {
        items.Clear();
        currentIndex = 0;
        Refresh();
    }

    public void AddItem(GameObject item)
    {
        if (item == null)
        {
            return;
        }

        items.Add(item);
        Refresh();
    }

    public void RegisterOnSelectionChanged(UnityAction<int> listener)
    {
        if (listener == null)
        {
            return;
        }

        onSelectionChanged.AddListener(listener);
    }

    public void UnregisterOnSelectionChanged(UnityAction<int> listener)
    {
        if (listener == null)
        {
            return;
        }

        onSelectionChanged.RemoveListener(listener);
    }

    private Vector2 GetPositionForOffset(int offset)
    {
        Vector2 basePosition = offset switch
        {
            -3 => previousThirdPosition,
            -2 => previousSecondPosition,
            -1 => previousPosition,
            0 => centerPosition,
            1 => nextPosition,
            2 => nextSecondPosition,
            3 => nextThirdPosition,
            _ => centerPosition
        };

        if (offset == 0)
        {
            return basePosition;
        }

        return new Vector2(basePosition.x, basePosition.y - arcHeight * Mathf.Abs(offset));
    }

    private Vector3 GetScaleForOffset(int absoluteOffset)
    {
        return absoluteOffset switch
        {
            1 => sideScale,
            2 => secondSideScale,
            3 => thirdSideScale,
            _ => selectedScale
        };
    }

    private float GetAlphaForOffset(int absoluteOffset)
    {
        return absoluteOffset switch
        {
            0 => centerAlpha,
            1 => sideAlpha,
            2 => secondSideAlpha,
            3 => thirdSideAlpha,
            _ => 0f
        };
    }

    private float GetRotationForOffset(int offset)
    {
        return offset switch
        {
            -3 => thirdTiltAngle,
            -2 => secondTiltAngle,
            -1 => sideTiltAngle,
            0 => 0f,
            1 => -sideTiltAngle,
            2 => -secondTiltAngle,
            3 => -thirdTiltAngle,
            _ => 0f
        };
    }

    private void SetupItem(GameObject item, Vector2 anchoredPosition, Vector3 scale, int siblingIndex, float alpha, float rotationZ)
    {
        if (item == null)
        {
            return;
        }

        targetStates[item] = new CarouselVisualState
        {
            position = anchoredPosition,
            scale = scale,
            alpha = alpha,
            rotationZ = rotationZ,
            siblingIndex = siblingIndex,
            visible = true
        };
    }

    private void AnimateItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            GameObject item = items[i];
            if (item == null || !targetStates.TryGetValue(item, out CarouselVisualState state))
            {
                continue;
            }

            item.SetActive(true);
            EnsureCanvasGroup(item);

            RectTransform rectTransform = item.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, state.position, Time.deltaTime * moveLerpSpeed);
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, state.scale, Time.deltaTime * scaleLerpSpeed);
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, state.rotationZ);
                rectTransform.localRotation = Quaternion.Lerp(rectTransform.localRotation, targetRotation, Time.deltaTime * rotateLerpSpeed);
            }
            else
            {
                item.transform.localPosition = Vector3.Lerp(item.transform.localPosition, state.position, Time.deltaTime * moveLerpSpeed);
                item.transform.localScale = Vector3.Lerp(item.transform.localScale, state.scale, Time.deltaTime * scaleLerpSpeed);
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, state.rotationZ);
                item.transform.localRotation = Quaternion.Lerp(item.transform.localRotation, targetRotation, Time.deltaTime * rotateLerpSpeed);
            }

            CanvasGroup canvasGroup = canvasGroups[item];
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, state.alpha, Time.deltaTime * fadeLerpSpeed);

            if (item.transform.parent != null)
            {
                item.transform.SetSiblingIndex(Mathf.Clamp(state.siblingIndex, 0, item.transform.parent.childCount - 1));
            }

            if (!state.visible && canvasGroup.alpha <= 0.02f)
            {
                item.SetActive(false);
            }
        }
    }

    private void EnsureCanvasGroup(GameObject item)
    {
        if (item == null || canvasGroups.ContainsKey(item))
        {
            return;
        }

        CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = item.AddComponent<CanvasGroup>();
        }

        canvasGroups[item] = canvasGroup;
    }

    private int WrapIndex(int index)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        int wrappedIndex = index % items.Count;
        return wrappedIndex < 0 ? wrappedIndex + items.Count : wrappedIndex;
    }

    private bool IsPointerOverWheelArea()
    {
        if (wheelArea == null)
        {
            return true;
        }

        Camera eventCamera = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = parentCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(wheelArea, Input.mousePosition, eventCamera);
    }
}
