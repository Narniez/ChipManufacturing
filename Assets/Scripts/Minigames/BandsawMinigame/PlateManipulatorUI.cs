using UnityEngine;
using UnityEngine.UI;

public class PlateManipulatorUI : MonoBehaviour
{
    [Header("Targets")]
    public PlateManipulator target;

    [Header("Sliders")]
    public Slider translateSpeedSlider;
    public Slider rotationSpeedSlider;

    [Header("Ranges")]
    public float translateMin = 0f;
    public float translateMax = 0.2f;
    public float rotationMin = 0f;
    public float rotationMax = 2f;

    void Awake()
    {
     
       // Configure ranges
        if (translateSpeedSlider)
        {
            translateSpeedSlider.minValue = translateMin;
            translateSpeedSlider.maxValue = translateMax;
            translateSpeedSlider.value = target ? target.translateSpeed : translateMin;
            translateSpeedSlider.onValueChanged.AddListener(OnTranslateChanged);
        }

        if (rotationSpeedSlider)
        {
            rotationSpeedSlider.minValue = rotationMin;
            rotationSpeedSlider.maxValue = rotationMax;
            rotationSpeedSlider.value = target ? target.rotationSpeed : rotationMin;
            rotationSpeedSlider.onValueChanged.AddListener(OnRotationChanged);
        }
    }

    void OnDestroy()
    {
        if (translateSpeedSlider) translateSpeedSlider.onValueChanged.RemoveListener(OnTranslateChanged);
        if (rotationSpeedSlider) rotationSpeedSlider.onValueChanged.RemoveListener(OnRotationChanged);
    }

    public void OnTranslateChanged(float v)
    {
        if (!target) return;
        target.SetTranslateSpeed(v);
    }

    public void OnRotationChanged(float v)
    {
        if (!target) return;
        target.SetRotationSpeed(v);
    }
}
