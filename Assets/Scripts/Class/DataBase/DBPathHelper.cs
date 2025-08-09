using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest

public class DBPathHelper
{
    const string DB_NAME = "WordGame.db";

    /// <summary>
    /// �T�w�i�g��Ƨ����w����Ʈw�F�Y�S���N�q StreamingAssets �ƻs�L�h�C
    /// �Ǧ^�i�����Ω� SQLite �s�u��������|�C
    /// </summary>
    public static async Task<string> EnsureDbInWritablePath()
    {
        string dstPath = Path.Combine(Application.persistentDataPath, DB_NAME);

        if (File.Exists(dstPath))
            return dstPath;                       // �w�s�b�A������

        string srcPath = Path.Combine(Application.streamingAssetsPath, DB_NAME);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android: StreamingAssets �b .apk �̡A�u��� UnityWebRequest Ū��
        using UnityWebRequest uwr = UnityWebRequest.Get(srcPath);
        var op = uwr.SendWebRequest();
        while (!op.isDone) await Task.Yield();    // async ���ݡA������D�����

        if (uwr.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Ū����Ʈw����: {uwr.error}\n�ӷ����|: {srcPath}");
            throw new IOException("Unable to copy database from StreamingAssets.");
        }
        File.WriteAllBytes(dstPath, uwr.downloadHandler.data);
#else
        // PC / macOS / iOS / Editor�G�i�����ƻs�ɮ�
        File.Copy(srcPath, dstPath, overwrite: true);
#endif

        Debug.Log($"��Ʈw�w�ƻs��i�g���|: {dstPath}");
        return dstPath;
    }
}
