using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    #region SerializeField
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] ToggleGroup GameModeGroup;
    [SerializeField] TMP_InputField rangeMin;
    [SerializeField] TMP_InputField rangeMax;
    [SerializeField] GameObject gamePanel;
    [SerializeField] TextMeshProUGUI roundText;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] TextMeshProUGUI spell;
    [SerializeField] TextMeshProUGUI translate;
    [SerializeField] TextMeshProUGUI example;
    [SerializeField] TMP_InputField textInput;
    [SerializeField] Button dontKnowButton;
    #endregion
    SimpleDB db = new();
    bool readyToNext = false;
    private bool waitingForRelease;
 
    int next = 0;
    int round = 1;
    int rangeMinNumber, rangeMaxNumber;
    DataTable table;
    string tableName;
    int wordType;
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
    private void Start()
    {
        textInput.onSubmit.AddListener(OnTextSubmit);
    }
    void Update()
    {
        DetectEnter();
    }

    public void GameStart(int modeNumber)
    {
        if (!TryParseAndValidateRange(out rangeMinNumber, out rangeMaxNumber))
            return;
        wordType = modeNumber;
        tableName = GetTableType(modeNumber);
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
        //Ignore compostioning and empty input
        if (!string.IsNullOrEmpty(Input.compositionString) || textInput.text == "")
        {
            textInput.ActivateInputField();
            return;
        }
        if (textInput.text == spell.text)
        {
            HandleCorrectAnswer();
        }
        else
        {
            HandleWrongAnswer();
        }

        textInput.text = "";
        textInput.ActivateInputField();
    }
    DataTable LoadWordsInRange(string type, int from, int to)
    {
        int limit = to - from + 1;
        int offset = from - 1;
        string command = $@"
            WITH subset AS (
            SELECT *
            FROM Vocabulary
            WHERE タイプ = '{type}'
            ORDER BY 番号
            LIMIT {limit}    
            OFFSET {offset}       
            )
            SELECT *
            FROM subset
            ORDER BY RANDOM()
            ";

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
        SetExampleText(row);
        next += 1;
    }
    void SetExampleText(DataRow row)
    {
        example.text = RemoveParentheses(row["例"].ToString());
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

    string GetTableType(int number)
    {
        string[] tableNames = { "通常", "テキスト", "口語/ネット/方言" };
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

    public void OnDontKnowButtonClick()
    {
        ShowAnswer(true);
        readyToNext = true;
        textInput.text = "";
    }

    void HandleCorrectAnswer()
    {
        Debug.Log("Correct answer!");
        ShowAnswer(true);
        readyToNext = true;
        waitingForRelease = true;
    }
    void HandleWrongAnswer()
    {
        Debug.Log("Wrong answer!");
    }
}
