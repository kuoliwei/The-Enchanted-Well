using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class PointerHoldInvoker : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Hold Event")]
    [SerializeField] private UnityEvent onHold;

    [Header("Hold Settings")]
    [SerializeField] private float repeatInterval = 0.1f;

    [Header("Visual Feedback")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Color holdColor = Color.gray;
    [SerializeField] private Color normalColor = Color.white;

    private Coroutine holdRoutine;

    public void OnPointerDown(PointerEventData eventData)
    {
        SetImageColor(holdColor);
        holdRoutine = StartCoroutine(HoldLoop());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopHold();
    }

    private IEnumerator HoldLoop()
    {
        while (true)
        {
            onHold?.Invoke();
            yield return new WaitForSeconds(repeatInterval);
        }
    }

    private void StopHold()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        SetImageColor(normalColor);
    }

    private void SetImageColor(Color color)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }
}
