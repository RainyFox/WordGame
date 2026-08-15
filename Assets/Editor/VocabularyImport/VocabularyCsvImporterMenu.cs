using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace WordGame.Editor.VocabularyImport
{
    internal static class VocabularyCsvImporterMenu
    {
        const string MenuPath = "Tools/Vocabulary/Import CSV...";

        [MenuItem(MenuPath, priority = 2000)]
        static void ImportCsv()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string csvPath = EditorUtility.OpenFilePanel(
                "選擇 Vocabulary CSV",
                projectRoot,
                "csv");
            if (string.IsNullOrEmpty(csvPath))
                return;

            try
            {
                RunImport(projectRoot, csvPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("單字匯入失敗", exception.Message, "關閉");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(MenuPath, validate = true)]
        static bool ValidateImportCsv()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static void RunImport(string projectRoot, string csvPath)
        {
            EditorUtility.DisplayProgressBar("單字匯入", "讀取並驗證 CSV……", 0.1f);
            IReadOnlyList<VocabularyRecord> csvRecords = VocabularyCsvReader.Read(csvPath);

            EditorUtility.DisplayProgressBar("單字匯入", "比較資料庫內容……", 0.35f);
            var service = new VocabularyImportService(projectRoot);
            VocabularyImportPlan importPlan = service.CreatePlan(csvRecords);

            if (!importPlan.HasChanges)
            {
                EditorUtility.DisplayDialog(
                    "不需要更新",
                    BuildPlanMessage(importPlan),
                    "關閉");
                return;
            }

            bool shouldImport = EditorUtility.DisplayDialog(
                "確認匯入單字",
                BuildPlanMessage(importPlan),
                "建立備份並匯入",
                "取消");
            if (!shouldImport)
                return;

            EditorUtility.DisplayProgressBar("單字匯入", "備份並更新資料庫……", 0.7f);
            VocabularyImportResult result = service.Apply(importPlan);
            RefreshStreamingAssetsDatabase();
            ShowSuccess(importPlan, result);
        }

        static string BuildPlanMessage(VocabularyImportPlan importPlan)
        {
            var message = new StringBuilder();
            message.AppendLine($"CSV 有效資料：{importPlan.CsvRecordCount} 筆");
            message.AppendLine();

            foreach (DatabaseImportPlan databasePlan in importPlan.DatabasePlans)
            {
                AppendDatabasePlan(message, databasePlan);
            }

            message.AppendLine("只會新增或更新 Vocabulary，不會刪除單字或修改 UserProgress。");
            return message.ToString().TrimEnd();
        }

        static void AppendDatabasePlan(
            StringBuilder message,
            DatabaseImportPlan databasePlan)
        {
            message.AppendLine(databasePlan.DisplayName);
            message.AppendLine(
                $"  新增 {databasePlan.AddedCount}／更新 {databasePlan.UpdatedCount}／"
                + $"不變 {databasePlan.UnchangedCount}");

            if (databasePlan.ExtraCount > 0)
            {
                message.AppendLine(
                    $"  額外保留 {databasePlan.ExtraCount} 筆未出現在基準資料中的單字");
            }

            message.AppendLine();
        }

        static void RefreshStreamingAssetsDatabase()
        {
            AssetDatabase.ImportAsset(
                "Assets/StreamingAssets/WordGame.db",
                ImportAssetOptions.ForceUpdate);
        }

        static void ShowSuccess(
            VocabularyImportPlan importPlan,
            VocabularyImportResult result)
        {
            int totalChanges = 0;
            foreach (DatabaseImportPlan databasePlan in importPlan.DatabasePlans)
            {
                totalChanges += databasePlan.ChangeCount;
            }

            string message = $"兩份資料庫合計寫入 {totalChanges} 筆變更。\n\n"
                + $"備份位置：\n{result.BackupDirectory}";
            Debug.Log($"Vocabulary CSV import completed. {message}");
            EditorUtility.DisplayDialog("單字匯入完成", message, "完成");
        }
    }
}
