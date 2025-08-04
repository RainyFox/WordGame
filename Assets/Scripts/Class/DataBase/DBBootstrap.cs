using UnityEngine;

public class DBBootstrap : MonoBehaviour
{
    private SimpleDB db;
    async void Awake()
    {
        string path = await DBPathHelper.EnsureDbInWritablePath();
        db = new SimpleDB(path);
    }
}

