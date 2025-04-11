using System.Data;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region SerializeField
    [SerializeField] GameObject gamePanel;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] TextMeshProUGUI spell;
    [SerializeField] TextMeshProUGUI translate;
    [SerializeField] TextMeshProUGUI example;
    [SerializeField] TMP_InputField input;
    #endregion
    SimpleDB db = new();
    bool readyToNext = false;
    bool waitingForRelease = false;
    int next = 0;
    DataTable table;
    void Start()
    {
        int from = 1;
        int to = 43;
        table = ReadWordsInRange(from, to, 43);
        ShowAnswer(false);
        LoadNextWord();
    }

    void Update()
    {
        DetectEnter();
    }

    public void OnTextSubmit(string text)
    {
        if (input.text == spell.text)
        {
            ShowAnswer(true);
            readyToNext = true;
            waitingForRelease = true;
        }
        input.text = "";
        input.ActivateInputField();
    }
    DataTable ReadWordsInRange(int from, int to, int quantity)
    {
        string command = $@"
            SELECT * 
            FROM Vocabulary
            WHERE 番号 between {from} and {to}
            ORDER by random()
            limit {quantity}";

        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }

    void LoadNextWord()
    {
        DataRow row = table.Rows[next];
        questionText.text = row["単語"].ToString();
        spell.text = row["綴り"].ToString();
        translate.text = row["中国語"].ToString();
        example.text = RemoveParentheses(row["例"].ToString());
        next += 1;
    }
    void ShowAnswer(bool show)
    {
        spell.enabled = show;
        translate.enabled = show;
        example.enabled = show;
    }

    void DetectEnter()
    {
        // 如果還在等你鬆開按鍵，就不處理下一題的行為
        if (waitingForRelease)
        {
            // 檢查是否已鬆開 Enter
            if (Input.GetKeyUp(KeyCode.Return))
            {
                // 一旦鬆開後，才允許偵測下一次按 Enter
                waitingForRelease = false;
            }
            return;  // 跳出 Update，不做後面
        }

        // 真正偵測第二次按 Enter
        if (readyToNext && Input.GetKeyDown(KeyCode.Return))
        {
            ShowAnswer(false);
            LoadNextWord();
            readyToNext = false;
        }
    }

    string RemoveParentheses(string text)
    {
        return Regex.Replace(text, "[()]", "");
    }
}
