using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LetterTapper : MonoBehaviour
{
    [SerializeField] GameObject letterObject;
    [SerializeField] Color tappedColor = Color.green;

    private void OnMouseDown()
    {
        Debug.Log("Letter tapped: " + gameObject.name);

        if (letterObject == null) return;

        // TextMeshPro (UGUI or 3D)
        var tmp = letterObject.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.color = tappedColor;
            return;
        }
    }
}
