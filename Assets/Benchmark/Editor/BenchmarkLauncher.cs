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

        [MenuItem("Tools/Path Planning Benchmark/Record Demo Frames")]
        public static void RecordDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Launch(DemoRecorder.RunFlagKey);
        }

        // 렌더링이 필요하므로 -nographics 없이 실행
        // Unity -batchmode -projectPath <path> -executeMethod Benchmark.BenchmarkLauncher.RecordDemoBatch -logFile <log>
        public static void RecordDemoBatch() => Launch(DemoRecorder.RunFlagKey);

        static void Launch() => Launch(PathPlanningBenchmark.RunFlagKey);

        static void Launch(string flag)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Astar.unity");
            SessionState.SetBool(flag, true);
            EditorApplication.EnterPlaymode();
        }
    }
}
