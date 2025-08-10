using UnityEngine;

[CreateAssetMenu(fileName = "AppConfig", menuName = "Scriptable Objects/AppConfig")]
public class AppConfig : ScriptableObject
{
    public bool TestMode;                    // ���A�b Inspector ��
    static AppConfig _i;
    public static AppConfig I => _i ??= Resources.Load<AppConfig>("AppConfig");
}
