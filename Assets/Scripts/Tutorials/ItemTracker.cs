using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class ItemTracker : MonoBehaviour
{
    public MaterialData itemToTrack;

    [SerializeField] private TextMeshProUGUI _itemCountText;
    [SerializeField] private int targetCount = 5;

    [Header("Result UI")]
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TextMeshProUGUI _timeText;

    private int _currentCount = 0;
    private float _startTime = 0f;
    private bool _completed = false;

    private void Awake()
    {
        // Start the timer with the game
        _startTime = Time.time;

        // Ensure result panel is hidden at start
        if (_resultPanel != null)
            _resultPanel.SetActive(false);
    }

    private void OnEnable()
    {
        _itemCountText.text = _currentCount + "/" + targetCount;
        Machine.OnMaterialProduced += UpdateCount;
    }

    private void OnDestroy()
    {
        Machine.OnMaterialProduced -= UpdateCount;
    }

    private void UpdateCount(MaterialData material, Vector3 machinePosition)
    {
        if (_completed)
            return;

        if (material == itemToTrack)
        {
            _currentCount++;
            if (_currentCount >= targetCount)
            {
                _currentCount = targetCount;
                _itemCountText.text = _currentCount + "/" + targetCount;

                // Compute elapsed time
                float elapsed = Time.time - _startTime;
                int minutes = Mathf.FloorToInt(elapsed / 60f);
                int seconds = Mathf.FloorToInt(elapsed % 60f);

                if (_timeText != null)
                    _timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

                if (_resultPanel != null)
                    _resultPanel.SetActive(true);

                _completed = true;

                // Stop updating the count
                Machine.OnMaterialProduced -= UpdateCount;
            }
            else
            {
                _itemCountText.text = _currentCount + "/" + targetCount;
            }
        }
    }

    public void ExitButton() {         
        Application.Quit();
    }
}
