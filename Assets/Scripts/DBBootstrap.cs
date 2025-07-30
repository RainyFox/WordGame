using UnityEngine;

public class DBBootstrap : MonoBehaviour
{
    private SimpleDB db;
    async void Awake()
    {
        db = new SimpleDB();
        //TODO: Ensure the database is created and ready to use
        //string path = await EnsureDbInWritablePath();
    }


}

