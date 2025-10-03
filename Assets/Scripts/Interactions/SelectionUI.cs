using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class SelectionUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button rotateLeftButton;
    [SerializeField] private Button rotateRightButton;

    private Action _onRotateLeft;
    private Action _onRotateRight;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (rotateLeftButton != null)
            rotateLeftButton.onClick.AddListener(() => _onRotateLeft?.Invoke());

        if (rotateRightButton != null)
            rotateRightButton.onClick.AddListener(() => _onRotateRight?.Invoke());
    }

    public void Show(string title, Action onRotateLeft, Action onRotateRight)
    {
        _onRotateLeft = onRotateLeft;
        _onRotateRight = onRotateRight;

        if (titleText != null) titleText.text = string.IsNullOrEmpty(title) ? "Selected" : title;
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        _onRotateLeft = null;
        _onRotateRight = null;
        if (panel != null) panel.SetActive(false);
    }
}
