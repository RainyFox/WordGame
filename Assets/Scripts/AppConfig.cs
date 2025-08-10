using UnityEngine;

[CreateAssetMenu(fileName = "AppConfig", menuName = "Scriptable Objects/AppConfig")]
public class AppConfig : ScriptableObject
{
    public bool TestMode;                    // µ¹§A¦b Inspector ¤Ä
    static AppConfig _i;
    public static AppConfig I => _i ??= Resources.Load<AppConfig>("AppConfig");
}
