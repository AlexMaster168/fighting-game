using UnityEditor;

public static class Launcher
{
    public static void Play()
    {
        EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
    }
}
