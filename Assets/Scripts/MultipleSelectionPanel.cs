using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultipleSelectionPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] texts;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void SetText(int index, string text)
    {
        texts[index].text = text;
    }

    public string GetText(int index)
    {
        return texts[index].text;
    }
}
