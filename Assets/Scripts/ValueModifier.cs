using UnityEngine;
using UnityEngine.UI;

public enum NumericType
{
    Integer,
    Float
}

public class ValueModifier : MonoBehaviour
{
    [Header("Target Input")]
    [SerializeField] private InputField inputField;

    [Header("Value Type")]
    [SerializeField] private NumericType numericType = NumericType.Integer;

    [Header("Step Settings")]
    [SerializeField] private float stepAmount = 1f;

    [Header("Clamp Settings")]
    [SerializeField] private bool useClamp = true;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue;
    [Header("Invalid Input Fallback")]
    [SerializeField] private float invalidInputFallbackValue;

    public void Increase()
    {
        if (!TryGetCurrentValue(out float current))
            return;

        current += stepAmount;
        SetValue(current);
    }

    public void Decrease()
    {
        if (!TryGetCurrentValue(out float current))
            return;

        current -= stepAmount;
        SetValue(current);
    }

    public void ClampCurrentValue()
    {
        if (!TryGetCurrentValue(out float current))
            return;

        SetValue(current);
    }

    private bool TryGetCurrentValue(out float value)
    {
        // 空字串也視為非法輸入
        if (string.IsNullOrEmpty(inputField.text))
        {
            value = invalidInputFallbackValue;
            return true;
        }

        if (!float.TryParse(inputField.text, out value))
        {
            value = invalidInputFallbackValue;
            return true;
        }

        return true;
    }


    private void SetValue(float value)
    {
        value = NormalizeByNumericType(value);
        value = ClampValue(value);

        if (numericType == NumericType.Integer)
        {
            inputField.text = ((int)value).ToString();
        }
        else
        {
            inputField.text = value.ToString("0.##");
        }
    }

    private float NormalizeByNumericType(float value)
    {
        if (numericType == NumericType.Integer)
        {
            return Mathf.Round(value);
        }

        return value;
    }

    private float ClampValue(float value)
    {
        if (!useClamp)
            return value;

        return Mathf.Clamp(value, minValue, maxValue);
    }
}
