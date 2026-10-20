using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Benchmark
{
    // 경로계획 알고리즘 정량 비교 하네스.
    // 알고리즘 코드는 수정하지 않고, 각 씬의 컴포넌트를 그대로 불러 같은 시작/목표 쌍으로 반복 실행한다.
    // 실행: Tools > Path Planning Benchmark > Run (또는 BenchmarkLauncher.RunBatch를 -executeMethod로)
    public class PathPlanningBenchmark : MonoBehaviour
    {
        public const string RunFlagKey = "PathPlanningBenchmark.Run";

        // ---- 실험 설정 ----
        public int pairSeed = 2024;          // 시작/목표 쌍 생성 시드
        public int randomPairCount = 30;     // 원본 쌍 외 무작위 쌍 개수
        public float minPairDistance = 50f;  // 시작-목표 최소 직선거리
        public float minEndpointClearance = 2.5f; // 시작/목표의 장애물 최소 이격 (RRT 격자 스냅 오차 포함)
        public float mapHalfExtent = 45f;    // 시작/목표 샘플링 범위 (격자는 ±50)
        public int gridTimingRepeats = 5;    // A*/Dijkstra 계획 시간 반복 측정 횟수 (중앙값 사용)
        public int rrtSeeds = 20;            // RRT/RRT* 쌍당 시드 수
        public float bugSpeed = 20f;         // Bug 계열 이동 속도 (씬 기본값: Bug0/1 = 20, Bug2 = 10)
        public float bugDeltaTime = 1f / 60f;
        public float bugTimeout = 90f;       // 시뮬레이션 시간 기준 제한 (s)
        public float bugGoalTolerance = 1f;
        public float sampleStep = 0.1f;      // 경로 충돌/이격 검사 간격

        const string CommonMapScene = "Assets/Scenes/Astar.unity";
        static readonly Vector3 OriginalStart = new Vector3(25.7f, 0f, -42.3f);  // Astar.unity의 Seeker
        static readonly Vector3 OriginalGoal = new Vector3(-24.2f, 0f, 41.2f);   // 모든 씬의 Target
        const int UnwalkableMask = 1 << 6;

        readonly List<Collider> obstacles = new List<Collider>();
        readonly List<(Vector3 start, Vector3 goal)> pairs = new List<(Vector3, Vector3)>();
        readonly StringBuilder plannerCsv = new StringBuilder();
        readonly StringBuilder bugCsv = new StringBuilder();
        readonly StringBuilder pathCsv = new StringBuilder();
        string outDir;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!SessionState.GetBool(RunFlagKey, false)) return;
            SessionState.SetBool(RunFlagKey, false);
            var go = new GameObject("PathPlanningBenchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<PathPlanningBenchmark>();
        }
#endif

        void Start()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BenchmarkResults"));
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var total = Stopwatch.StartNew();
            plannerCsv.AppendLine("algo,pair,seed,success,plan_ms,nodes,path_len,waypoints,turn_deg,min_clear,mean_clear,collision_len,retrace_aborted");
            bugCsv.AppendLine("algo,pair,success,timeout,sim_time,frames,travel_len,turn_deg,min_clear,mean_clear,collision_len");
            pathCsv.AppendLine("algo,pair,seed,idx,x,z");

            yield return LoadScene(CommonMapScene);
            ExportOccupancy();
            GeneratePairs();

            // 알고리즘 내부 Debug.Log/print가 시간 측정을 왜곡하지 않도록 일반 로그만 거른다 (경고·예외는 남김)
            Debug.unityLogger.filterLogType = LogType.Warning;

            yield return RunGridPlanner<Astar.Grid, Astar.Pathfinding>("Astar", CommonMapScene,
                (s, g, cb) => Astar.PathRequestManager.RequestPath(s, g, cb), CountTouchedAstar);
            yield return RunGridPlanner<Dijkstra.Grid, Dijkstra.Pathfinding>("Dijkstra", "Assets/Scenes/Dijkstra.unity",
                (s, g, cb) => Dijkstra.PathRequestManager.RequestPath(s, g, cb), CountTouchedDijkstra);
            yield return RunRrt<RRT.Grid, RRT.Pathfinding>("RRT", "Assets/Scenes/RRT.unity",
                (s, g, cb) => RRT.PathRequestManager.RequestPath(s, g, cb), pf => pf.lastRetraceAborted);
            yield return RunRrt<RRTstar.Grid, RRTstar.Pathfinding>("RRTstar", "Assets/Scenes/RRTstar.unity",
                (s, g, cb) => RRTstar.PathRequestManager.RequestPath(s, g, cb), pf => pf.lastRetraceAborted);
            yield return RunBugs();

            Debug.unityLogger.filterLogType = LogType.Log;
            File.WriteAllText(Path.Combine(outDir, "planner_trials.csv"), plannerCsv.ToString());
            File.WriteAllText(Path.Combine(outDir, "bug_trials.csv"), bugCsv.ToString());
            File.WriteAllText(Path.Combine(outDir, "paths.csv"), pathCsv.ToString());
            WriteMeta(total.Elapsed.TotalSeconds);
            Debug.LogWarning($"[Benchmark] done in {total.Elapsed.TotalSeconds:F1}s -> {outDir}");
            Finish();
        }

        // ---------------- 씬 준비 ----------------

        IEnumerator LoadScene(string path)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Physics.SyncTransforms();
            CollectObstacles(); // 씬을 다시 불러오면 이전 충돌체 참조는 파괴된다
            Debug.LogWarning($"[Benchmark] loaded {path} ({obstacles.Count} obstacles)");
        }

        // Start() 전에 호출되므로 Unit이 자동으로 경로를 요청하거나 Seeker가 움직이지 않도록 막는다.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Seeker" || root.name == "Target") root.SetActive(false);
                foreach (var cam in root.GetComponentsInChildren<CameraController>(true)) cam.enabled = false;
            }
        }

        void CollectObstacles()
        {
            obstacles.Clear();
            foreach (var c in FindObjectsOfType<Collider>())
                if (((1 << c.gameObject.layer) & UnwalkableMask) != 0) obstacles.Add(c);
        }

        // 장애물에서 가장 가까운 거리 (내부면 0). 높이 y=0 평면 기준.
        float Clearance(Vector3 p)
        {
            p.y = 0f;
            float best = float.MaxValue;
            foreach (var c in obstacles)
            {
                Vector3 q = c.ClosestPoint(p);
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                if (d < best) best = d;
            }
            return best;
        }

        void ExportOccupancy()
        {
            // 그림용 점유 격자 (0.25 해상도, 물리 충돌체 기준)
            const float res = 0.25f;
            int n = Mathf.RoundToInt(100f / res);
            var sb = new StringBuilder();
            sb.AppendLine($"# res={res} origin=-50,-50 rows=z cols=x");
            for (int zi = 0; zi < n; zi++)
            {
                var row = new char[n];
                for (int xi = 0; xi < n; xi++)
                {
                    var p = new Vector3(-50f + (xi + 0.5f) * res, 0f, -50f + (zi + 0.5f) * res);
                    row[xi] = Physics.CheckSphere(p, 0.01f, UnwalkableMask) ? '1' : '0';
                }
                sb.AppendLine(new string(row));
            }
            File.WriteAllText(Path.Combine(outDir, "occupancy.txt"), sb.ToString());
        }

        void GeneratePairs()
        {
            pairs.Clear();
            pairs.Add((OriginalStart, OriginalGoal));
            var rng = new System.Random(pairSeed);
            Func<Vector3> sample = () => new Vector3(
                (float)(rng.NextDouble() * 2 - 1) * mapHalfExtent, 0f,
                (float)(rng.NextDouble() * 2 - 1) * mapHalfExtent);
            int guard = 0;
            while (pairs.Count < randomPairCount + 1 && guard++ < 100000)
            {
                Vector3 s = sample(), g = sample();
                if (Vector3.Distance(s, g) < minPairDistance) continue;
                if (Clearance(s) < minEndpointClearance || Clearance(g) < minEndpointClearance) continue;
                if (!SegmentBlocked(s, g)) continue; // 직선으로 갈 수 있는 쉬운 쌍은 제외
                pairs.Add((s, g));
            }
            var sb = new StringBuilder("pair,sx,sz,gx,gz,euclid,line_blocked\n");
            for (int i = 0; i < pairs.Count; i++)
            {
                var (s, g) = pairs[i];
                sb.AppendLine(Csv(i, s.x, s.z, g.x, g.z, Vector3.Distance(s, g), SegmentBlocked(s, g) ? 1 : 0));
            }
            File.WriteAllText(Path.Combine(outDir, "pairs.csv"), sb.ToString());
        }

        bool SegmentBlocked(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            for (float t = 0; t <= len; t += sampleStep)
                if (Clearance(Vector3.Lerp(a, b, t / len)) <= 0f) return true;
            return false;
        }

        // ---------------- 경로 지표 ----------------

        struct PathStats { public float length, turnDeg, minClear, meanClear, collisionLen; }

        PathStats Measure(List<Vector3> pts)
        {
            var st = new PathStats { minClear = float.MaxValue };
            double clearSum = 0; int samples = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                a.y = b.y = 0f;
                float seg = Vector3.Distance(a, b);
                st.length += seg;
                int n = Mathf.Max(1, Mathf.CeilToInt(seg / sampleStep));
                for (int k = (i == 0 ? 0 : 1); k <= n; k++)
                {
                    float c = Clearance(Vector3.Lerp(a, b, (float)k / n));
                    clearSum += c; samples++;
                    if (c < st.minClear) st.minClear = c;
                    if (c <= 0f) st.collisionLen += seg / n;
                }
            }
            st.meanClear = samples > 0 ? (float)(clearSum / samples) : 0f;
            st.turnDeg = TurnDegrees(pts);
            return st;
        }

        static float TurnDegrees(List<Vector3> pts)
        {
            float sum = 0f;
            Vector3? prevDir = null;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 d = pts[i + 1] - pts[i]; d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) continue;
                if (prevDir.HasValue) sum += Vector3.Angle(prevDir.Value, d);
                prevDir = d;
            }
            return sum;
        }

        // 계획기 경로 = 시작점 + 웨이포인트 + 목표점
        static List<Vector3> FullPolyline(Vector3 s, Vector3[] wp, Vector3 g)
        {
            var pts = new List<Vector3> { s };
            if (wp != null) pts.AddRange(wp);
            pts.Add(g);
            return pts;
        }

        // ---------------- A* / Dijkstra ----------------

        static int CountTouchedAstar(Astar.Grid grid)
        {
            var nodes = (Astar.Node[,])typeof(Astar.Grid).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(grid);
            int n = 0; foreach (var node in nodes) if (node.parent != null) n++;
            return n;
        }

        static int CountTouchedDijkstra(Dijkstra.Grid grid)
        {
            var nodes = (Dijkstra.Node[,])typeof(Dijkstra.Grid).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(grid);
            int n = 0; foreach (var node in nodes) if (node.parent != null) n++;
            return n;
        }

        IEnumerator RunGridPlanner<TGrid, TPf>(string algo, string scene,
            Action<Vector3, Vector3, Action<Vector3[], bool>> request, Func<TGrid, int> countNodes)
            where TGrid : MonoBehaviour where TPf : MonoBehaviour
        {
            yield return LoadScene(scene);
            var grid = FindObjectOfType<TGrid>();
            var createGrid = typeof(TGrid).GetMethod("CreateGrid", BindingFlags.NonPublic | BindingFlags.Instance);

            bool done = false; Vector3[] wp = null; bool ok = false;
            Action<Vector3[], bool> cb = (p, s) => { wp = p; ok = s; done = true; };

            // 워밍업 (JIT)
            createGrid.Invoke(grid, null); done = false;
            request(pairs[0].start, pairs[0].goal, cb);
            while (!done) yield return null;

            for (int i = 0; i < pairs.Count; i++)
            {
                var (s, g) = pairs[i];
                var times = new List<double>();
                int nodes = 0;
                for (int r = 0; r < gridTimingRepeats; r++)
                {
                    createGrid.Invoke(grid, null);
                    GC.Collect();
                    done = false;
                    var sw = Stopwatch.StartNew();
                    request(s, g, cb); // 탐색 루프는 첫 yield 전에 동기적으로 끝난다
                    sw.Stop();
                    times.Add(sw.Elapsed.TotalMilliseconds);
                    while (!done) yield return null;
                    nodes = countNodes(grid);
                }
                times.Sort();
                RecordPlanner(algo, i, 0, ok, times[times.Count / 2], nodes, s, wp, g, false);
            }
        }

        // ---------------- RRT / RRT* ----------------

        IEnumerator RunRrt<TGrid, TPf>(string algo, string scene,
            Action<Vector3, Vector3, Action<Vector3[], bool>> request, Func<TPf, bool> aborted)
            where TGrid : MonoBehaviour where TPf : MonoBehaviour
        {
            yield return LoadScene(scene);
            var grid = FindObjectOfType<TGrid>();
            var pf = FindObjectOfType<TPf>();
            var createGrid = typeof(TGrid).GetMethod("CreateGrid", BindingFlags.NonPublic | BindingFlags.Instance);
            var treeField = typeof(TPf).GetField("treeNodes", BindingFlags.NonPublic | BindingFlags.Instance);
            var visitedField = typeof(TPf).GetField("visitedNodes", BindingFlags.NonPublic | BindingFlags.Instance);

            bool done = false; Vector3[] wp = null; bool ok = false;
            Action<Vector3[], bool> cb = (p, s) => { wp = p; ok = s; done = true; };

            void Reset()
            {
                // 원본은 treeNodes/visitedNodes를 비우지 않으므로 시행마다 새 격자와 빈 트리로 초기화
                createGrid.Invoke(grid, null);
                ((IList)treeField.GetValue(pf)).Clear();
                visitedField.FieldType.GetMethod("Clear").Invoke(visitedField.GetValue(pf), null);
            }

            Reset(); done = false; UnityEngine.Random.InitState(-1);
            request(pairs[0].start, pairs[0].goal, cb);
            while (!done) yield return null;

            for (int i = 0; i < pairs.Count; i++)
            {
                var (s, g) = pairs[i];
                for (int seed = 0; seed < rrtSeeds; seed++)
                {
                    Reset();
                    GC.Collect();
                    UnityEngine.Random.InitState(i * 1000 + seed);
                    done = false;
                    var sw = Stopwatch.StartNew();
                    request(s, g, cb);
                    sw.Stop();
                    while (!done) yield return null;
                    int nodes = ((IList)treeField.GetValue(pf)).Count;
                    RecordPlanner(algo, i, seed, ok, sw.Elapsed.TotalMilliseconds, nodes, s, wp, g, aborted(pf));
                }
                yield return null;
            }
        }

        void RecordPlanner(string algo, int pair, int seed, bool ok, double ms, int nodes,
            Vector3 s, Vector3[] wp, Vector3 g, bool retraceAborted)
        {
            bool success = ok && !retraceAborted;
            if (success)
            {
                var pts = FullPolyline(s, wp, g);
                var st = Measure(pts);
                plannerCsv.AppendLine(Csv(algo, pair, seed, 1, ms, nodes, st.length, wp.Length, st.turnDeg, st.minClear, st.meanClear, st.collisionLen, 0));
                if (pair == 0 && seed < 5) DumpPath(algo, pair, seed, pts);
            }
            else
            {
                plannerCsv.AppendLine(Csv(algo, pair, seed, 0, ms, nodes, "", "", "", "", "", "", retraceAborted ? 1 : 0));
            }
        }

        // ---------------- Bug0 / Bug1 / Bug2 ----------------

        IEnumerator RunBugs()
        {
            // Bug 씬은 장애물 배치가 약간 달라서, 공통 맵에 Bug 씬과 같은 설정의 격자/Seeker를 직접 구성한다.
            yield return LoadScene(CommonMapScene);
            var gridGo = new GameObject("BugGrid");
            gridGo.SetActive(false);
            var grid = gridGo.AddComponent<base2.Grid>();
            grid.unwalkableMask = UnwalkableMask;
            grid.gridWorldSize = new Vector2(100f, 100f);
            grid.nodeRadius = 0.1f; // Bug.unity 값
            gridGo.SetActive(true);  // Awake -> CreateGrid
            yield return null;

            Time.captureDeltaTime = bugDeltaTime; // 프레임마다 고정 시뮬레이션 시간
            foreach (var algo in new[] { "Bug0", "Bug1", "Bug2" })
            {
                for (int i = 0; i < pairs.Count; i++)
                    yield return RunBugTrial(algo, i);
                Debug.LogWarning($"[Benchmark] {algo} finished");
            }
            Time.captureDeltaTime = 0f;
        }

        IEnumerator RunBugTrial(string algo, int pairIdx)
        {
            var (s, g) = pairs[pairIdx];
            var target = new GameObject("BugTarget").transform; // 점 목표 (충돌체 없음)
            target.position = g;
            var seeker = new GameObject("BugSeeker");
            seeker.SetActive(false);
            seeker.transform.position = s;
            switch (algo)
            {
                case "Bug0": { var b = seeker.AddComponent<base2.Bug0Seeker>(); b.target = target; b.moveSpeed = bugSpeed; break; }
                case "Bug1": { var b = seeker.AddComponent<base2.Bug1Seeker>(); b.target = target; b.moveSpeed = bugSpeed; break; }
                case "Bug2": { var b = seeker.AddComponent<base2.Bug2Seeker>(); b.target = target; b.moveSpeed = bugSpeed; break; }
            }
            seeker.SetActive(true);

            var traj = new List<Vector3> { s };
            int frames = 0; bool success = false;
            int maxFrames = Mathf.CeilToInt(bugTimeout / bugDeltaTime);
            while (frames < maxFrames)
            {
                yield return null;
                frames++;
                Vector3 p = seeker.transform.position;
                if ((p - traj[traj.Count - 1]).sqrMagnitude > 1e-8f) traj.Add(p);
                if (Vector3.Distance(p, g) <= bugGoalTolerance) { success = true; break; }
            }
            Destroy(seeker);
            Destroy(target.gameObject);

            // 회전량은 프레임 단위 떨림을 피하려고 0.5 간격으로 재표본화한 궤적으로 계산
            var st = Measure(traj);
            st.turnDeg = TurnDegrees(Resample(traj, 0.5f));
            bugCsv.AppendLine(Csv(algo, pairIdx, success ? 1 : 0, success ? 0 : 1, frames * bugDeltaTime, frames,
                st.length, st.turnDeg, st.minClear, st.meanClear, st.collisionLen));
            if (pairIdx == 0) DumpPath(algo, pairIdx, 0, Resample(traj, 0.25f));
        }

        static List<Vector3> Resample(List<Vector3> pts, float step)
        {
            var outPts = new List<Vector3>();
            if (pts.Count == 0) return outPts;
            outPts.Add(pts[0]);
            float carry = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                float seg = Vector3.Distance(a, b), t = step - carry;
                while (t <= seg) { outPts.Add(Vector3.Lerp(a, b, t / seg)); t += step; }
                carry = seg - (t - step);
            }
            if (outPts[outPts.Count - 1] != pts[pts.Count - 1]) outPts.Add(pts[pts.Count - 1]);
            return outPts;
        }

        // ---------------- 출력 ----------------

        void DumpPath(string algo, int pair, int seed, List<Vector3> pts)
        {
            for (int k = 0; k < pts.Count; k++) pathCsv.AppendLine(Csv(algo, pair, seed, k, pts[k].x, pts[k].z));
        }

        void WriteMeta(double seconds)
        {
            var sb = new StringBuilder("{\n");
            sb.AppendLine($"  \"unity\": \"{Application.unityVersion}\",");
            sb.AppendLine($"  \"cpu\": \"{SystemInfo.processorType}\",");
            sb.AppendLine($"  \"timestamp\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            sb.AppendLine($"  \"pairs\": {pairs.Count}, \"pair_seed\": {pairSeed}, \"rrt_seeds\": {rrtSeeds}, \"grid_timing_repeats\": {gridTimingRepeats},");
            sb.AppendLine($"  \"bug_speed\": {Inv(bugSpeed)}, \"bug_dt\": {Inv(bugDeltaTime)}, \"bug_timeout\": {Inv(bugTimeout)}, \"bug_goal_tolerance\": {Inv(bugGoalTolerance)},");
            sb.AppendLine($"  \"min_pair_distance\": {Inv(minPairDistance)}, \"min_endpoint_clearance\": {Inv(minEndpointClearance)},");
            sb.AppendLine($"  \"elapsed_s\": {Inv((float)seconds)}");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "meta.json"), sb.ToString());
        }

        static string Inv(float v) => v.ToString("G6", CultureInfo.InvariantCulture);

        static string Csv(params object[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                switch (values[i])
                {
                    case float f: parts[i] = f.ToString("0.####", CultureInfo.InvariantCulture); break;
                    case double d: parts[i] = d.ToString("0.####", CultureInfo.InvariantCulture); break;
                    default: parts[i] = Convert.ToString(values[i], CultureInfo.InvariantCulture); break;
                }
            }
            return string.Join(",", parts);
        }

        void Finish()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else EditorApplication.isPlaying = false;
#endif
        }
    }
}
