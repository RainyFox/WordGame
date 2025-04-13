using System.Data;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region SerializeField
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] TMP_InputField rangeMin;
    [SerializeField] TMP_InputField rangeMax;
    [SerializeField] GameObject gamePanel;
    [SerializeField] TextMeshProUGUI roundText;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] TextMeshProUGUI spell;
    [SerializeField] TextMeshProUGUI translate;
    [SerializeField] TextMeshProUGUI example;
    [SerializeField] TMP_InputField textInput;
    #endregion
    SimpleDB db = new();
    bool readyToNext = false;
    bool waitingForRelease = false;
    int next = 0;
    int round = 1;
    int rangeMinNumber, rangeMaxNumber;
    DataTable table;
    string tableName;
    #region Properties
    public int Round
    {
        get => round;
        set 
        {
            round = value;
            roundText.text = $" {round} 回";
        }
    }

    #endregion
    void Start()
    {
        
     
    }

    void Update()
    {
        DetectEnter();
    }

    public void GameStart(int type)
    {
        if (!TryParseAndValidateRange(out rangeMinNumber, out rangeMaxNumber))
            return;
        tableName = GetTableName(type);
        table = LoadWordsInRange(tableName, rangeMinNumber, rangeMaxNumber);
        if (table.Rows.Count == 0)
            return;
        ShowAnswer(false);
        LoadNextWord();
        Round = 1;
        levelSelectPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
    public void OnTextSubmit(string text)
    {
        if (textInput.text == spell.text)
        {
            ShowAnswer(true);
            readyToNext = true;
            waitingForRelease = true;
        }
        textInput.text = "";
        textInput.ActivateInputField();
    }
    DataTable LoadWordsInRange(string table, int from, int to)
    {
        string command = $@"
            SELECT * 
            FROM {table}
            WHERE 番号 between {from} and {to}
            ORDER by random()";

        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }

    void LoadNextWord()
    {
        CheckRoundEnd();
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

    void CheckRoundEnd()
    {
        if (next >= table.Rows.Count)
        {
            next = 0;
            Round += 1;
            table = LoadWordsInRange(tableName, rangeMinNumber, rangeMaxNumber);
        }
    }

    string GetTableName(int number)
    {
        string[] tableNames = { "Vocabulary", "Textbook", "NonTextbook" };
        return tableNames[number];
    }
    private bool TryParseAndValidateRange(out int min, out int max)
    {
        // 嘗試解析
        bool okMin = int.TryParse(rangeMin.text, out min);
        bool okMax = int.TryParse(rangeMax.text, out max);

        // 如果任一解析失敗，給予預設值然後返回 false
        if (!okMin || !okMax)
        {
            if (!okMin)
                min = 0;  // 可根據需求設定預設值
            if (!okMax)
                max = 0;
            return false;
        }

        // 若最小值大於最大值也返回 false
        if (min > max)
            return false;

        return true;
    }
}
