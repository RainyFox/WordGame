using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    #region SerializeField
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] ToggleGroup RandomModeGroup;
    [SerializeField] TMP_InputField rangeMin;
    [SerializeField] TMP_InputField rangeMax;
    [SerializeField] GameObject gamePanel;
    [SerializeField] TextMeshProUGUI numberText;
    [SerializeField] TextMeshProUGUI roundText;
    [SerializeField] TextMeshProUGUI questionText;
    [SerializeField] TextMeshProUGUI spell;
    [SerializeField] TextMeshProUGUI translate;
    [SerializeField] TextMeshProUGUI example;
    [SerializeField] TMP_InputField textInput;
    [SerializeField] Button multipleSelectionButton;
    [SerializeField] MultipleSelectionPanel multipleSelectionPanel;
    [SerializeField] Button dontKnowButton;
    [SerializeField] TextMeshProUGUI translateDirection;
    [SerializeField] TMP_FontAsset JpFont;
    [SerializeField] TMP_FontAsset CnFont;
    #endregion
    SimpleDB db = new();
    bool readyToNext = false;
    private bool waitingForRelease;

    int next = 0;
    int round = 1;
    int rangeMinNumber, rangeMaxNumber;
    DataTable table;
    string practiceType;
    RandomType randomType;
    UserProgress currentWordProgress;
    bool JpToCn = true;
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
        DetectProceedTrigger();
    }

    public void GameStart(int modeNumber)
    {
        if (!TryParseAndValidateRange(out rangeMinNumber, out rangeMaxNumber))
            return;
        ApplyModeSettings(modeNumber);
        LoadFirstWord();
        ShowAnswer(false);
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
    DataTable GetTableInRange(string type, int from, int to)
    {
        int limit = to - from + 1;
        int offset = from - 1;
        string whereClause = string.IsNullOrEmpty(type) || type == "ALL"
                   ? ""
                   : $"WHERE タイプ = '{type}'";
        string command = $@"
            WITH subset AS (
            SELECT *
            FROM Vocabulary
            {whereClause}
            ORDER BY 番号
            LIMIT {limit}    
            OFFSET {offset}       
            )
            SELECT S.*,
            COALESCE(U.Proficiency, 0) AS Proficiency
            FROM subset AS S
            LEFT JOIN UserProgress AS U
            ON S.番号 = U.番号
            ORDER BY RANDOM()
            ";

        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }

    void LoadNextWordInTable()
    {
        CheckRoundEnd();
        DataRow row = table.Rows[next];
        RenderQuestions(row);
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

    void DetectProceedTrigger()
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
        if (readyToNext && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Mouse0)))
        {
            ShowAnswer(false);
            if (randomType == RandomType.FULLRANDOM)
                LoadNextWordInTable();
            else
                LoadNextWordByWeight(practiceType);
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
            table = GetTableInRange(practiceType, rangeMinNumber, rangeMaxNumber);
        }
    }

    string GetPraticeType(int number)
    {
        string[] tableNames = { "通常", "テキスト", "口語/ネット/方言", "ALL" };
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
        HandleWrongAnswer();
        ShowAnswer(true);
        readyToNext = true;
        textInput.text = "";
    }

    void HandleCorrectAnswer()
    {
        multipleSelectionPanel.gameObject.SetActive(false);
        AdjustDataAndWrite(true);
        ShowAnswer(true);
        readyToNext = true;
        waitingForRelease = true;
    }
    void HandleWrongAnswer()
    {
        AdjustDataAndWrite(false);
    }
    void AdjustDataAndWrite(bool isCorrect)
    {
        currentWordProgress.OnAnswer(isCorrect);
        var data = currentWordProgress.ToDictionary();
        db.InsertIntoDB("UserProgress", data);
    }
    void LoadUserProgress(int wordNumber)
    {
        string mode = JpToCn ? "JpToCn" : "CnToJp";
        string command = $@"
        SELECT *
        FROM UserProgress
        WHERE 番号 = {wordNumber}
        AND Mode = '{mode}'";
        DataTable result = db.GetTableFromSQLcommand(command);
        if (result.Rows.Count > 0)
            currentWordProgress = new UserProgress(result.Rows[0]);
        else
            currentWordProgress = new UserProgress(wordNumber,mode);
    }

    void LoadNextWordByWeight(string type)
    {
        int limit = rangeMaxNumber - rangeMinNumber + 1;
        int offset = rangeMinNumber - 1;
        string whereClause = string.IsNullOrEmpty(type) || type == "ALL"
                       ? ""
                       : $"WHERE タイプ = '{type}'";
        // Note: This SQL command will tend to gather high proficiency words in the middle of the table (when without using LIMIT)
        string command = $@"
            WITH subset AS (
            SELECT *
            FROM Vocabulary
            {whereClause}
            ORDER BY 番号
            LIMIT {limit}    
            OFFSET {offset}       
            )
            SELECT S.*,
            COALESCE(U.Proficiency, 0) AS Proficiency
            FROM subset AS S
            LEFT JOIN UserProgress AS U
            ON S.番号 = U.番号
            AND U.Mode = '{(JpToCn ? "JpToCn" : "CnToJp")}'
            ORDER BY RANDOM()*(0.5      -- 基底
              * pow(0.5, COALESCE(U.Proficiency,0))   -- 熟練度
              * CASE                                  -- 時間因子
                  WHEN U.LastAnswer IS NULL THEN 1
                  ELSE min(1,
                      (julianday('now')-julianday(U.LastAnswer)) /
                      (pow(2,COALESCE(U.Proficiency,0))))
                END
             )
			 LIMIT 1
            ";

        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        DataRow row = wordsInRange.Rows[0];
        RenderQuestions(row);
        if(!JpToCn)
            RenderMultipleChoices(row["単語"].ToString(),type, int.Parse(row["番号"].ToString()));
    }

    RandomType GetRandomType()
    {
        var active = RandomModeGroup.ActiveToggles().FirstOrDefault();
        if (active.name == "FullRandom")
        {
            return RandomType.FULLRANDOM;
        }
        else
        {
            return RandomType.PROFICIENCY;
        }
    }

    void LoadFirstWord()
    {
        if (randomType == RandomType.FULLRANDOM)
        {
            table = GetTableInRange(practiceType, rangeMinNumber, rangeMaxNumber);
            if (table.Rows.Count == 0)
                return;
            LoadNextWordInTable();
            Round = 1;
        }
        else
        {
            LoadNextWordByWeight(practiceType);
            roundText.gameObject.SetActive(false);
        }
    }

    public void TranslateDirectionSwitch()
    {
        JpToCn = !JpToCn;
        if (JpToCn)
            translateDirection.text = "→";
        else
            translateDirection.text = "←";
    }

    void RenderQuestions(DataRow row)
    {
        int wordNumber = int.Parse(row["番号"].ToString());
        numberText.text = wordNumber.ToString();
        LoadUserProgress(wordNumber);
        if (JpToCn)
        {
            questionText.text = row["単語"].ToString();
            spell.text = row["綴り"].ToString();
            translate.text = row["中国語"].ToString();
        }
        else
        {
            questionText.text = row["中国語"].ToString();
            spell.text = row["単語"].ToString();
            translate.text = row["綴り"].ToString();
        }
        SetExampleText(row);
    }

    void AdjustFontSet()
    {
        if (!JpToCn)
        {
            questionText.font = CnFont;
            translate.font = JpFont;
        }
    }

    void ApplyModeSettings(int modeNumber)
    {
        practiceType = GetPraticeType(modeNumber);
        randomType = GetRandomType();
        AdjustFontSet();
        if (!JpToCn)
            multipleSelectionButton.gameObject.SetActive(true);
    }
  
    public void ToggleMultipleSelections()
    {
        multipleSelectionPanel.gameObject.SetActive(!multipleSelectionPanel.gameObject.activeSelf);
    }
    public void OnMultipleSelctionButton(int number)
    {
        string answer = multipleSelectionPanel.GetText(number);
        textInput.text = answer;
        OnTextSubmit(answer);
        waitingForRelease = false; // Cancel the waiting for release since not enter the answer by keyboard
    }
    void RenderMultipleChoices(string answer, string type, int wordNumber)
    {
        string[] choices = new string[4];
        DataTable distractors = GetDistractors(type, wordNumber);

        choices[0] = answer;
        for (int i = 1; i < 4; i++)
        {
            choices[i] = distractors.Rows[i - 1]["単語"].ToString();
        }
        // Shuffle the choices
        var shuffled = choices.OrderBy(_ => UnityEngine.Random.value).ToArray();
        for (int i = 0; i < 4; i++)
        {
            multipleSelectionPanel.SetText(i, shuffled[i]);
        }
    }
    DataTable GetDistractors(string type, int wordNumber)
    {
        string whereClause = string.IsNullOrEmpty(type) || type == "ALL"
                     ? $"WHERE 番号 <> {wordNumber}"
                     : $"WHERE タイプ = '{type}'  AND 番号 <> {wordNumber}";
        string command = $@"
            SELECT 単語
            FROM Vocabulary
            {whereClause}
            ORDER BY RANDOM()
            LIMIT 3
            ";
        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }
}
