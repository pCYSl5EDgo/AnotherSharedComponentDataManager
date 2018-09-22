using UnityEditor;

public sealed class TestRunner : EditorWindow
{
    [MenuItem("Window/RunTestRunner &#f")]
    public static void RunTestRunner()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
    }
}
