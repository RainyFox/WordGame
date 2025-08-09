using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest

public class DBPathHelper
{
    const string DB_NAME = "WordGame.db";

    /// <summary>
    /// 確定可寫資料夾內已有資料庫；若沒有就從 StreamingAssets 複製過去。
    /// 傳回可直接用於 SQLite 連線的絕對路徑。
    /// </summary>
    public static async Task<string> EnsureDbInWritablePath()
    {
        string dstPath = Path.Combine(Application.persistentDataPath, DB_NAME);

        if (File.Exists(dstPath))
            return dstPath;                       // 已存在，直接用

        string srcPath = Path.Combine(Application.streamingAssetsPath, DB_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: StreamingAssets 在 .apk 裡，只能用 UnityWebRequest 讀取
        using UnityWebRequest uwr = UnityWebRequest.Get(srcPath);
        var op = uwr.SendWebRequest();
        while (!op.isDone) await Task.Yield();    // async 等待，不阻塞主執行緒

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"讀取資料庫失敗: {uwr.error}\n來源路徑: {srcPath}");
            throw new IOException("Unable to copy database from StreamingAssets.");
        }
        File.WriteAllBytes(dstPath, uwr.downloadHandler.data);
#else
        // PC / macOS / iOS / Editor：可直接複製檔案
        File.Copy(srcPath, dstPath, overwrite: true);
#endif

        Debug.Log($"資料庫已複製到可寫路徑: {dstPath}");
        return dstPath;
    }
}
