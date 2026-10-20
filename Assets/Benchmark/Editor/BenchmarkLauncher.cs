using UnityEditor;
using UnityEditor.SceneManagement;

namespace Benchmark
{
    public static class BenchmarkLauncher
    {
        [MenuItem("Tools/Path Planning Benchmark/Run")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Launch();
        }

        // Unity -batchmode -projectPath <path> -executeMethod Benchmark.BenchmarkLauncher.RunBatch -logFile <log>
        public static void RunBatch() => Launch();

        static void Launch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Astar.unity");
            SessionState.SetBool(PathPlanningBenchmark.RunFlagKey, true);
            EditorApplication.EnterPlaymode();
        }
    }
}
