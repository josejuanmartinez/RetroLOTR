using UnityEngine;
using UnityEngine.UI;

/// <summary>Prevents MaterialManager from replacing this Image's authored material.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ImageUnaffectedBySkin : MonoBehaviour
{
}
