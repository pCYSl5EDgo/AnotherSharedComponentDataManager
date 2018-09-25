using UnityEditor;

public sealed class TestHelper : EditorWindow
{
    [MenuItem("Window/Helper/RunTestRunner &#f")]
    public static void RunTestRunner()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
    }
    [MenuItem("Window/Helper/ShowEntityDebugger &#g")]
    public static void ShowEntityDebugger()
    {
        EditorApplication.ExecuteMenuItem("Window/Analysis/Entity Debugger");
    }
    [MenuItem("Window/Helper/EditSettings &#d")]
    public static void EditSettings()
    {
        EditorApplication.ExecuteMenuItem("Edit/Settings");
    }
}
