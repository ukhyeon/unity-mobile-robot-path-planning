using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Benchmark
{
    // README용 데모 프레임 녹화기.
    // 원본 시각화는 OnDrawGizmos라 카메라 렌더에 찍히지 않으므로, 알고리즘 상태(탐색 노드, 트리, 경로)를 읽어 다시 그린다.
    // 각 씬의 Unit이 원래대로 경로를 요청하고 따라가며, 녹화기는 탐색 과정을 재생한 뒤 주행을 기록한다.
    // 출력: <프로젝트>/Recordings/<이름>/f_0000.png  (GIF 변환은 Tools/make_gifs.sh)
    public class DemoRecorder : MonoBehaviour
    {
        public const string RunFlagKey = "PathPlanningDemo.Run";

        // BenchmarkResults/pairs.csv의 pair 29 (세 Bug가 모두 성공하고 동작 차이가 잘 보이는 쌍)
        static readonly Vector3 DemoStart = new Vector3(-36.3634f, 0f, 8.5267f);
        static readonly Vector3 DemoGoal = new Vector3(43.2906f, 0f, -28.541f);
        const int DemoPair = 29;
        const int UnwalkableMask = 1 << 6;

        public int resolution = 640;
        public int captureEvery = 3;          // 60Hz 시뮬레이션 중 3프레임마다 저장 -> 20fps
        public float simDeltaTime = 1f / 60f;
        public int exploreFrames = 50;        // 탐색 재생 길이 (프레임)
        public int holdFrames = 16;

        static readonly Color ExploredColor = new Color(0.53f, 0.71f, 0.94f, 0.55f); // #86b6ef
        static readonly Color TreeColor = new Color(0.32f, 0.32f, 0.31f, 0.9f);
        static readonly Dictionary<string, Color> AlgoColor = new Dictionary<string, Color>
        {
            { "Astar", Hex("#2a78d6") }, { "Dijkstra", Hex("#eb6834") }, { "RRT", Hex("#1baf7a") }, { "RRTstar", Hex("#eda100") },
            { "Bug0", Hex("#e87ba4") }, { "Bug1", Hex("#008300") }, { "Bug2", Hex("#6250d6") },
        };

        Camera cam;
        RenderTexture rt;
        Texture2D readback;
        string outRoot;
        string currentDir;
        int frameIndex;
        int pendingRandomSeed = -1;
        Material lineMat;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!SessionState.GetBool(RunFlagKey, false)) return;
            SessionState.SetBool(RunFlagKey, false);
            var go = new GameObject("DemoRecorder");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoRecorder>();
        }
#endif

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        void Start()
        {
            outRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
            lineMat = new Material(Shader.Find("Sprites/Default"));
            rt = new RenderTexture(resolution, resolution, 24) { antiAliasing = 8 };
            readback = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            var camGo = new GameObject("DemoCamera");
            DontDestroyOnLoad(camGo);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 51f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 80f, 0f), Quaternion.Euler(90f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#fcfcfb");
            cam.targetTexture = rt;
            cam.enabled = false; // 수동 렌더
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Debug.unityLogger.filterLogType = LogType.Warning;
            Time.captureDeltaTime = simDeltaTime;
            yield return RecordGridPlanner("astar", "Astar", "Assets/Scenes/Astar.unity", n => Get<int>(n, "gCost") + Get<int>(n, "hCost"));
            yield return RecordGridPlanner("dijkstra", "Dijkstra", "Assets/Scenes/Dijkstra.unity", n => Get<int>(n, "gCost"));
            yield return RecordRrt("rrt", "RRT", "Assets/Scenes/RRT.unity", DemoPair * 1000 + 1);
            yield return RecordRrt("rrtstar", "RRTstar", "Assets/Scenes/RRTstar.unity", DemoPair * 1000 + 0);
            yield return RecordBugs();
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            Debug.unityLogger.filterLogType = LogType.Log;
            Debug.LogWarning($"[Demo] done -> {outRoot}");
#if UNITY_EDITOR
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else EditorApplication.isPlaying = false;
#endif
        }

        // ---------------- 공통 ----------------

        IEnumerator LoadScene(string path)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Unit.Start() 전에 호출된다: Seeker/Target을 데모 쌍으로 옮기고, 탐색을 재생하는 동안 Seeker가 움직이지 않게 시간을 멈춘다.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Seeker") root.transform.position = DemoStart;
                if (root.name == "Target")
                {
                    root.transform.position = DemoGoal;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var c in root.GetComponentsInChildren<CameraController>(true)) c.enabled = false;
            }
            if (pendingRandomSeed >= 0) UnityEngine.Random.InitState(pendingRandomSeed);
            Time.timeScale = 0f;
        }

        void BeginClip(string name)
        {
            currentDir = Path.Combine(outRoot, name);
            if (Directory.Exists(currentDir)) Directory.Delete(currentDir, true);
            Directory.CreateDirectory(currentDir);
            frameIndex = 0;
        }

        void Capture()
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(currentDir, $"f_{frameIndex++:0000}.png"), readback.EncodeToPNG());
        }

        IEnumerator Hold(int frames)
        {
            for (int i = 0; i < frames; i++) { Capture(); yield return null; }
        }

        static T Get<T>(object o, string field) =>
            (T)o.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        static Component FindComponent(string typeName) =>
            (Component)FindObjectOfType(Type.GetType(typeName + ", Assembly-CSharp"));

        LineRenderer MakeLine(string name, Color color, float width, IList<Vector3> pts, float y = 0.3f)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.startColor = lr.endColor = color.linear; // 정점 색은 Linear 색 공간에서 자동 변환되지 않음
            lr.widthMultiplier = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.alignment = LineAlignment.TransformZ;
            lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 보이도록 선을 바닥면에 눕힘
            lr.sortingOrder = 10;
            lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, new Vector3(pts[i].x, y, pts[i].z));
            return lr;
        }

        void MakeMarker(Vector3 p, Color color, float size, float y = 0.2f)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(m.GetComponent<Collider>());
            m.transform.position = new Vector3(p.x, y, p.z);
            m.transform.localScale = new Vector3(size, 0.2f, size);
            var r = m.GetComponent<Renderer>();
            r.material = new Material(lineMat) { color = color };
            r.sortingOrder = 20;
        }

        void MakeGoalMarker()
        {
            MakeMarker(DemoGoal, Hex("#0b0b0b"), 3.6f, 0.4f);
            MakeMarker(DemoGoal, Hex("#fcfcfb"), 2.2f, 0.6f);
            MakeMarker(DemoGoal, Hex("#0b0b0b"), 1.0f, 0.8f);
        }

        // 시간을 다시 흐르게 해서 Seeker가 경로를 따라가는 모습을 기록
        IEnumerator RecordFollow(Transform seeker, Vector3 last, float timeout)
        {
            Time.timeScale = 1f;
            int frames = 0, maxFrames = Mathf.CeilToInt(timeout / simDeltaTime);
            while (frames < maxFrames && (seeker.position - last).sqrMagnitude > 1e-4f)
            {
                yield return null;
                if (++frames % captureEvery == 0) Capture();
            }
            Time.timeScale = 0f;
        }

        static List<Vector3> PathWithEnds(Vector3[] wp)
        {
            var pts = new List<Vector3> { DemoStart };
            pts.AddRange(wp);
            return pts;
        }

        // ---------------- A* / Dijkstra ----------------

        IEnumerator RecordGridPlanner(string clip, string ns, string scene, Func<object, int> order)
        {
            yield return LoadScene(scene);
            yield return null; yield return null; // Start -> RequestPath -> 콜백
            BeginClip(clip);

            var grid = FindComponent(ns + ".Grid");
            var unit = FindComponent(ns + ".Unit");
            var nodes = Get<Array>(grid, "grid");
            int sx = nodes.GetLength(0), sy = nodes.GetLength(1);
            var size = Get<Vector2>(grid, "gridWorldSize");

            // 탐색된(부모가 정해진) 노드를 비용 순으로 정렬해 파면처럼 퍼지는 모습으로 재생
            var touched = nodes.Cast<object>().Where(n => Get<object>(n, "parent") != null).OrderBy(order).ToList();
            var tex = new Texture2D(sx, sy, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = Enumerable.Repeat(new Color32(0, 0, 0, 0), sx * sy).ToArray();
            tex.SetPixels32(clear); tex.Apply();
            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(overlay.GetComponent<Collider>());
            overlay.transform.SetPositionAndRotation(new Vector3(grid.transform.position.x, 0.05f, grid.transform.position.z), Quaternion.Euler(90f, 0f, 0f));
            overlay.transform.localScale = new Vector3(size.x, size.y, 1f);
            overlay.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = tex };

            MakeMarker(DemoStart, Hex("#0b0b0b"), 2.2f);
            MakeGoalMarker();
            yield return Hold(holdFrames / 2);

            int painted = 0;
            for (int f = 1; f <= exploreFrames; f++)
            {
                int upto = touched.Count * f / exploreFrames;
                for (; painted < upto; painted++)
                    tex.SetPixel(Get<int>(touched[painted], "gridX"), Get<int>(touched[painted], "gridY"), ExploredColor);
                tex.Apply();
                Capture();
                yield return null;
            }

            var wp = Get<Vector3[]>(unit, "path");
            MakeLine("path", AlgoColor[ns], 1.1f, PathWithEnds(wp));
            yield return Hold(holdFrames);
            yield return RecordFollow(unit.transform, wp[wp.Length - 1], 20f);
            yield return Hold(holdFrames * 2);
            Debug.LogWarning($"[Demo] {clip}: {touched.Count} explored, {wp.Length} waypoints, {frameIndex} frames");
        }

        // ---------------- RRT / RRT* ----------------

        IEnumerator RecordRrt(string clip, string ns, string scene, int seed)
        {
            pendingRandomSeed = seed; // 벤치마크와 같은 시드 (시작 전에 Random 상태를 맞춘다)
            yield return LoadScene(scene);
            pendingRandomSeed = -1;
            yield return null; yield return null;
            BeginClip(clip);

            var pf = FindComponent(ns + ".Pathfinding");
            var unit = FindComponent(ns + ".Unit");
            var tree = ((IList)Get<object>(pf, "treeNodes")).Cast<object>().ToList();
            var wp = Get<Vector3[]>(unit, "path");
            if (wp == null || wp.Length == 0 || Get<bool>(pf, "lastRetraceAborted"))
            {
                Debug.LogError($"[Demo] {clip}: path not found with seed {seed}");
                yield break;
            }

            // 트리 간선을 삽입 순서대로 그린다 (RRT*는 rewire 이후의 최종 부모 기준)
            var mesh = new Mesh();
            var treeGo = new GameObject("tree");
            treeGo.AddComponent<MeshFilter>().mesh = mesh;
            treeGo.AddComponent<MeshRenderer>().material = lineMat;
            var verts = new List<Vector3>();
            var cols = new List<Color>();

            MakeMarker(DemoStart, Hex("#0b0b0b"), 2.2f);
            MakeGoalMarker();
            yield return Hold(holdFrames / 2);

            int added = 0;
            for (int f = 1; f <= exploreFrames; f++)
            {
                int upto = tree.Count * f / exploreFrames;
                for (; added < upto; added++)
                {
                    var n = tree[added];
                    var parent = Get<object>(n, "parent");
                    if (parent == null) continue;
                    Vector3 a = Get<Vector3>(n, "worldPosition"), b = Get<Vector3>(parent, "worldPosition");
                    verts.Add(new Vector3(a.x, 0.2f, a.z)); verts.Add(new Vector3(b.x, 0.2f, b.z));
                    cols.Add(TreeColor.linear); cols.Add(TreeColor.linear);
                }
                mesh.Clear();
                mesh.SetVertices(verts);
                mesh.SetColors(cols);
                mesh.SetIndices(Enumerable.Range(0, verts.Count).ToArray(), MeshTopology.Lines, 0);
                Capture();
                yield return null;
            }

            MakeLine("path", AlgoColor[ns], 1.1f, PathWithEnds(wp));
            yield return Hold(holdFrames);
            yield return RecordFollow(unit.transform, wp[wp.Length - 1], 20f);
            yield return Hold(holdFrames * 2);
            Debug.LogWarning($"[Demo] {clip}: {tree.Count} tree nodes, {wp.Length} waypoints, {frameIndex} frames");
        }

        // ---------------- Bug0 / Bug1 / Bug2 ----------------

        IEnumerator RecordBugs()
        {
            // 벤치마크와 같이 공통 맵에 Bug.unity 설정의 격자를 두고, 세 Seeker를 동시에 출발시킨다 (서로 충돌체 없음)
            yield return LoadScene("Assets/Scenes/Astar.unity");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "Seeker") root.SetActive(false);
                if (root.name == "Target") foreach (var c in root.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            var gridGo = new GameObject("BugGrid");
            gridGo.SetActive(false);
            var grid = gridGo.AddComponent<base2.Grid>();
            grid.unwalkableMask = UnwalkableMask;
            grid.gridWorldSize = new Vector2(100f, 100f);
            grid.nodeRadius = 0.1f;
            gridGo.SetActive(true);
            yield return null;

            BeginClip("bug");
            var target = new GameObject("BugTarget").transform;
            target.position = DemoGoal;
            MakeMarker(DemoStart, Hex("#0b0b0b"), 2.2f);
            MakeGoalMarker();

            var algos = new[] { "Bug0", "Bug1", "Bug2" };
            var seekers = new List<(string algo, GameObject go, MonoBehaviour comp, LineRenderer trail, List<Vector3> pts)>();
            foreach (var algo in algos)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                DestroyImmediate(go.GetComponent<Collider>());
                go.SetActive(false);
                go.transform.position = DemoStart;
                go.transform.localScale = Vector3.one * 2.2f;
                go.GetComponent<Renderer>().material = new Material(lineMat) { color = AlgoColor[algo] };
                MonoBehaviour comp = null;
                switch (algo)
                {
                    case "Bug0": { var b = go.AddComponent<base2.Bug0Seeker>(); b.target = target; b.moveSpeed = 20f; comp = b; break; }
                    case "Bug1": { var b = go.AddComponent<base2.Bug1Seeker>(); b.target = target; b.moveSpeed = 20f; comp = b; break; }
                    case "Bug2": { var b = go.AddComponent<base2.Bug2Seeker>(); b.target = target; b.moveSpeed = 20f; comp = b; break; }
                }
                var pts = new List<Vector3> { DemoStart };
                seekers.Add((algo, go, comp, MakeLine(algo + "_trail", AlgoColor[algo], 0.9f, pts), pts));
            }
            yield return Hold(holdFrames / 2);

            Time.timeScale = 1f;
            foreach (var s in seekers) s.go.SetActive(true);
            int frames = 0, maxFrames = Mathf.CeilToInt(25f / simDeltaTime);
            while (frames < maxFrames && seekers.Any(s => s.comp.enabled))
            {
                yield return null;
                frames++;
                foreach (var s in seekers)
                {
                    if (!s.comp.enabled) continue;
                    var p = s.go.transform.position;
                    if ((p - s.pts[s.pts.Count - 1]).sqrMagnitude > 0.04f)
                    {
                        s.pts.Add(p);
                        s.trail.positionCount = s.pts.Count;
                        s.trail.SetPosition(s.pts.Count - 1, new Vector3(p.x, 0.3f, p.z));
                    }
                    if (Vector3.Distance(p, DemoGoal) <= 1f) s.comp.enabled = false; // 도착하면 정지
                }
                if (frames % captureEvery == 0) Capture();
            }
            Time.timeScale = 0f;
            yield return Hold(holdFrames * 2);
            Debug.LogWarning($"[Demo] bug: {frames * simDeltaTime:F1}s sim, {frameIndex} frames, arrived=" +
                             string.Join(",", seekers.Select(s => $"{s.algo}:{!s.comp.enabled}")));
        }
    }
}
