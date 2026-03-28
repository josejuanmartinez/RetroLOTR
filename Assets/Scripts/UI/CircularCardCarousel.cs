using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CircularCardCarousel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private List<GameObject> items = new();
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RectTransform wheelArea;

    [Header("Layout")]
    [SerializeField] private Vector2 centerPosition = Vector2.zero;
    [SerializeField] private Vector2 previousPosition = new(-250f, 0f);
    [SerializeField] private Vector2 nextPosition = new(250f, 0f);
    [SerializeField] private Vector3 selectedScale = Vector3.one;
    [SerializeField] private Vector3 sideScale = new(0.8f, 0.8f, 0.8f);

    [Header("Input")]
    [SerializeField] private bool useMouseWheel = true;
    [SerializeField] private bool requirePointerOverAreaForWheel = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onSelectionChanged;

    private int currentIndex;
    private Canvas parentCanvas;

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

            items[i].SetActive(false);
        }

        GameObject currentItem = items[currentIndex];
        SetupItem(currentItem, centerPosition, selectedScale, 2);

        if (validCount == 2)
        {
            GameObject sideItem = items[WrapIndex(currentIndex + 1)];
            SetupItem(sideItem, nextPosition, sideScale, 1);
        }
        else if (validCount > 2)
        {
            GameObject previousItem = items[WrapIndex(currentIndex - 1)];
            SetupItem(previousItem, previousPosition, sideScale, 0);

            GameObject nextItem = items[WrapIndex(currentIndex + 1)];
            SetupItem(nextItem, nextPosition, sideScale, 1);
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

    private void SetupItem(GameObject item, Vector2 anchoredPosition, Vector3 scale, int siblingIndex)
    {
        if (item == null)
        {
            return;
        }

        item.SetActive(true);

        RectTransform rectTransform = item.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localScale = scale;
        }
        else
        {
            item.transform.localPosition = anchoredPosition;
            item.transform.localScale = scale;
        }

        if (item.transform.parent != null)
        {
            item.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, item.transform.parent.childCount - 1));
        }
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
