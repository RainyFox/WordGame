using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    SimpleDB db;
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
    const double NewCandidateSelectionRate = 0.15;
    const string ReviewCandidateQueryTemplate = @"
        WITH subset AS (
            SELECT *
            FROM Vocabulary
            WHERE 番号 BETWEEN {0} AND {1}
            {2}
            ORDER BY 番号
        )
        SELECT S.*,
            COALESCE(U.Proficiency, 0) AS Proficiency,
            U.LastAnswer AS LastAnswer,
            U.NextReview AS NextReview,
            CASE
                WHEN U.LastAnswer IS NULL OR U.LastAnswer = '' THEN 1.0
                WHEN U.NextReview IS NULL OR julianday(U.NextReview) IS NULL THEN 1.0
                WHEN julianday(U.NextReview) > julianday('now') THEN 0.0
                WHEN julianday(U.NextReview) <= julianday(U.LastAnswer) THEN 1.0
                ELSE 1.0 + log2(max(
                    1.0,
                    (julianday('now') - julianday(U.LastAnswer)) /
                    (julianday(U.NextReview) - julianday(U.LastAnswer))
                ))
            END AS SelectionWeight
        FROM subset AS S
        LEFT JOIN UserProgress AS U
            ON S.番号 = U.番号
            AND U.Mode = '{3}'
        ORDER BY S.番号";

    sealed class ReviewCandidatePools
    {
        public List<int> NewCandidateIndices { get; } = new List<int>();
        public List<int> DueCandidateIndices { get; } = new List<int>();
    }

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
    async void Start()
    {
        textInput.onSubmit.AddListener(OnTextSubmit);
        await InitializeDatabaseAsync();
    }
    private async Task InitializeDatabaseAsync()
    {
        bool isTest = AppConfig.I.TestMode;
        Debug.LogWarning($"Test Mode: {isTest}");

        if (isTest)
        {
            var projRoot = System.IO.Directory.GetParent(Application.dataPath)!.FullName;
            var abs = System.IO.Path.Combine(projRoot, "WordGame.db");
            db = new SimpleDB(abs);  
        }
        else
        {
            await DBBootstrap.Ready;
            db = DBBootstrap.Instance;
        }
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
                LoadNextReviewWord(practiceType);
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
        string[] tableNames = { "通常", "テキスト", "口語/ネット/方言", "文法","ALL" };
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
        string mode = GetCurrentProgressMode();
        string command = $@"
        SELECT *
        FROM UserProgress
        WHERE 番号 = {wordNumber}
        AND Mode = '{mode}'";
        DataTable result = db.GetTableFromSQLcommand(command);
        if (result.Rows.Count > 0)
            currentWordProgress = new UserProgress(result.Rows[0]);
        else
            currentWordProgress = new UserProgress(wordNumber, mode);
    }

    void LoadNextReviewWord(string type)
    {
        DataTable candidates = LoadReviewCandidates(type);
        if (!TrySelectReviewCandidate(candidates, out DataRow selectedRow))
        {
            Debug.LogWarning("No words found in the specified range and type.");
            return;
        }

        RenderReviewCandidate(selectedRow, type);
    }

    DataTable LoadReviewCandidates(string type)
    {
        string command = BuildReviewCandidateQuery(type);
        return db.GetTableFromSQLcommand(command);
    }

    string BuildReviewCandidateQuery(string type)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            ReviewCandidateQueryTemplate,
            rangeMinNumber,
            rangeMaxNumber,
            BuildVocabularyTypeCondition(type),
            GetCurrentProgressMode());
    }

    static string BuildVocabularyTypeCondition(string type)
    {
        return string.IsNullOrEmpty(type) || type == "ALL"
            ? ""
            : $"AND (タイプ = '{type}')";
    }

    string GetCurrentProgressMode()
    {
        return JpToCn ? "JpToCn" : "CnToJp";
    }

    static bool TrySelectReviewCandidate(DataTable candidates, out DataRow selectedRow)
    {
        selectedRow = null;
        if (candidates.Rows.Count == 0)
            return false;

        selectedRow = SelectReviewCandidate(candidates);
        return true;
    }

    void RenderReviewCandidate(DataRow row, string type)
    {
        RenderQuestions(row);
        RenderMultipleChoices(row, type, int.Parse(row["番号"].ToString()));
    }

    static DataRow SelectReviewCandidate(DataTable candidates)
    {
        int selectedIndex = SelectReviewCandidateIndex(
            candidates,
            UnityEngine.Random.value,
            UnityEngine.Random.value);
        return candidates.Rows[selectedIndex];
    }

    internal static int SelectReviewCandidateIndex(
        DataTable candidates,
        double poolRandomValue,
        double candidateRandomValue)
    {
        ReviewCandidatePools pools = GroupReviewCandidates(candidates);
        if (TrySelectCandidateFromAvailablePools(
            candidates,
            pools,
            poolRandomValue,
            candidateRandomValue,
            out int selectedIndex))
        {
            return selectedIndex;
        }

        return SelectFallbackCandidateIndex(candidates, candidateRandomValue);
    }

    static ReviewCandidatePools GroupReviewCandidates(DataTable candidates)
    {
        var pools = new ReviewCandidatePools();

        for (int i = 0; i < candidates.Rows.Count; i++)
        {
            DataRow candidate = candidates.Rows[i];
            if (IsNewCandidate(candidate))
                pools.NewCandidateIndices.Add(i);
            else if (IsDueCandidate(candidate))
                pools.DueCandidateIndices.Add(i);
        }

        return pools;
    }

    static bool TrySelectCandidateFromAvailablePools(
        DataTable candidates,
        ReviewCandidatePools pools,
        double poolRandomValue,
        double candidateRandomValue,
        out int selectedIndex)
    {
        if (ShouldSelectNewCandidate(pools, poolRandomValue))
        {
            selectedIndex = SelectUniformCandidateIndex(
                pools.NewCandidateIndices,
                candidateRandomValue);
            return true;
        }

        if (pools.DueCandidateIndices.Count > 0)
        {
            selectedIndex = SelectWeightedCandidateIndex(
                candidates,
                pools.DueCandidateIndices,
                candidateRandomValue);
            return true;
        }

        selectedIndex = -1;
        return false;
    }

    static bool ShouldSelectNewCandidate(
        ReviewCandidatePools pools,
        double poolRandomValue)
    {
        if (pools.NewCandidateIndices.Count == 0)
            return false;

        return pools.DueCandidateIndices.Count == 0
            || NormalizeRandomValue(poolRandomValue) < NewCandidateSelectionRate;
    }

    static int SelectWeightedCandidateIndex(
        DataTable candidates,
        IReadOnlyList<int> candidateIndices,
        double randomValue)
    {
        double[] weights = ExtractSelectionWeights(candidates, candidateIndices);
        int poolIndex = SelectWeightedIndex(weights, randomValue);
        return poolIndex >= 0
            ? candidateIndices[poolIndex]
            : SelectUniformCandidateIndex(candidateIndices, randomValue);
    }

    static double[] ExtractSelectionWeights(
        DataTable candidates,
        IReadOnlyList<int> candidateIndices)
    {
        var weights = new double[candidateIndices.Count];
        for (int i = 0; i < candidateIndices.Count; i++)
        {
            weights[i] = GetSelectionWeight(candidates.Rows[candidateIndices[i]]);
        }

        return weights;
    }

    static int SelectUniformCandidateIndex(
        IReadOnlyList<int> candidateIndices,
        double randomValue)
    {
        int poolIndex = SelectUniformIndex(candidateIndices.Count, randomValue);
        return candidateIndices[poolIndex];
    }

    static int SelectFallbackCandidateIndex(
        DataTable candidates,
        double randomValue)
    {
        int closestReviewIndex = FindClosestReviewIndex(candidates);
        return closestReviewIndex >= 0
            ? closestReviewIndex
            : SelectUniformIndex(candidates.Rows.Count, randomValue);
    }

    static int SelectUniformIndex(int count, double randomValue)
    {
        double normalizedValue = NormalizeRandomValue(randomValue);
        return Math.Min((int)(normalizedValue * count), count - 1);
    }

    static bool IsNewCandidate(DataRow candidate)
    {
        object lastAnswer = candidate["LastAnswer"];
        return IsMissingDatabaseValue(lastAnswer)
            || string.IsNullOrWhiteSpace(lastAnswer.ToString());
    }

    static bool IsDueCandidate(DataRow candidate)
    {
        return GetSelectionWeight(candidate) > 0;
    }

    static int FindClosestReviewIndex(DataTable candidates)
    {
        int closestIndex = -1;
        DateTimeOffset closestReview = DateTimeOffset.MaxValue;

        for (int i = 0; i < candidates.Rows.Count; i++)
        {
            if (!TryReadNextReview(candidates.Rows[i], out DateTimeOffset nextReview))
                continue;

            if (nextReview < closestReview)
            {
                closestReview = nextReview;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    static bool TryReadNextReview(DataRow row, out DateTimeOffset nextReview)
    {
        nextReview = default;
        object rawValue = row["NextReview"];
        if (IsMissingDatabaseValue(rawValue))
            return false;

        return DateTimeOffset.TryParse(
            rawValue.ToString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out nextReview);
    }

    static double GetSelectionWeight(DataRow row)
    {
        if (!TryReadDouble(row["SelectionWeight"], out double weight))
            return 0;

        return IsValidSelectionWeight(weight) ? weight : 0;
    }

    static bool TryReadDouble(object rawValue, out double value)
    {
        value = 0;
        if (IsMissingDatabaseValue(rawValue))
            return false;

        string text = rawValue.ToString();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    static bool IsMissingDatabaseValue(object value)
    {
        return value == null || value == DBNull.Value;
    }

    static bool IsValidSelectionWeight(double weight)
    {
        return weight > 0 && !double.IsNaN(weight) && !double.IsInfinity(weight);
    }

    internal static int SelectWeightedIndex(IReadOnlyList<double> weights, double normalizedRandomValue)
    {
        if (!TrySummarizeWeights(weights, out double totalWeight, out int lastPositiveIndex))
            return -1;

        double target = NormalizeRandomValue(normalizedRandomValue) * totalWeight;
        return FindCumulativeWeightIndex(weights, target, lastPositiveIndex);
    }

    static bool TrySummarizeWeights(
        IReadOnlyList<double> weights,
        out double totalWeight,
        out int lastPositiveIndex)
    {
        totalWeight = 0;
        lastPositiveIndex = -1;

        for (int i = 0; i < weights.Count; i++)
        {
            if (!IsValidSelectionWeight(weights[i]))
                continue;

            totalWeight += weights[i];
            lastPositiveIndex = i;
        }

        return lastPositiveIndex >= 0 && !double.IsInfinity(totalWeight);
    }

    static double NormalizeRandomValue(double value)
    {
        return double.IsNaN(value)
            ? 0
            : Math.Max(0, Math.Min(1, value));
    }

    static int FindCumulativeWeightIndex(
        IReadOnlyList<double> weights,
        double target,
        int fallbackIndex)
    {
        double cumulativeWeight = 0;

        for (int i = 0; i < weights.Count; i++)
        {
            if (!IsValidSelectionWeight(weights[i]))
                continue;

            cumulativeWeight += weights[i];
            if (target < cumulativeWeight)
                return i;
        }

        return fallbackIndex;
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
            LoadNextReviewWord(practiceType);
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
    void RenderMultipleChoices(DataRow answerRow, string type, int wordNumber)
    {
        string[] choices = new string[4];
        DataTable distractors = GetDistractors(type, wordNumber);


        choices[0] = answerRow[JpToCn ? "綴り" : "単語"].ToString();
        for (int i = 1; i < 4; i++)
        {
            choices[i] = distractors.Rows[i - 1][JpToCn ? "綴り" : "単語"].ToString();
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
            SELECT 単語, 綴り
            FROM Vocabulary
            {whereClause}
            ORDER BY RANDOM()
            LIMIT 3
            ";
        DataTable wordsInRange = db.GetTableFromSQLcommand(command);
        return wordsInRange;
    }

}
