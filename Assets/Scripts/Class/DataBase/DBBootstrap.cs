using System.Threading.Tasks;
using UnityEngine;

public class DBBootstrap : MonoBehaviour
{
    public static SimpleDB Instance { get; private set; }
    public static Task Ready { get; private set; }
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SQLitePCL.Batteries_V2.Init();
        Ready = InitAsync();
    }

    private async Task InitAsync()
    {
        string path = await DBPathHelper.EnsureDbInWritablePath();
        Instance = new SimpleDB(path);   // �o�̤~ new�A�T�O�e���w Init()
    }
}

