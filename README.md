# Unity Mobile Robot Path Planning

Unity에서 모바일 로봇의 경로계획 알고리즘(Bug0/1/2, Dijkstra, A\*, RRT, RRT\*)을 구현하고, 각 알고리즘을 씬별로 시각화한 프로젝트입니다.

이 저장소는 [PathPlanning_project_unity](https://github.com/ukhyeon/PathPlanning_project_unity)에서 경로계획 부분만 분리한 것입니다. 원본 저장소는 학사 졸업 프로젝트(ZMP 기반 전복 방지)용으로 그대로 유지됩니다. 경로계획 관련 커밋 이력은 이 저장소에도 남아 있습니다.

## 환경

- Unity **2021.3.36f1**
- Unity Hub → *Add project from disk* → 이 폴더 선택. `Library/`는 처음 열 때 자동으로 생성됩니다.

## 씬과 스크립트

| 알고리즘 | 씬 | 스크립트 (namespace) |
|---|---|---|
| Bug0 / Bug1 / Bug2 | `Assets/Scenes/Bug.unity` | `Assets/Scripts/Bug/` (`base2`) |
| Dijkstra | `Assets/Scenes/Dijkstra.unity` | `Assets/Scripts/Dijkstra/` (`Dijkstra`) |
| A\* | `Assets/Scenes/Astar.unity` | `Assets/Scripts/Astar/` (`Astar`) |
| RRT | `Assets/Scenes/RRT.unity` | `Assets/Scripts/RRT/` (`RRT`) |
| RRT\* | `Assets/Scenes/RRTstar.unity` | `Assets/Scripts/RRTstar/` (`RRTstar`) |

`Assets/Scripts/Common/CameraController.cs`는 모든 씬이 함께 쓰는 카메라 추적 스크립트입니다.

알고리즘마다 폴더와 namespace가 따로 있어서, 같은 이름의 클래스(`Grid`, `Node`, `Pathfinding` 등)가 있어도 서로 충돌하지 않습니다.

- **Grid / Node**: 격자 기반 공간 표현. `unwalkableMask` 레이어로 장애물 노드를 판정합니다.
- **Pathfinding**: 알고리즘 본체. Dijkstra와 A\*는 `Heap<T>`을 우선순위 큐로 씁니다.
- **PathRequestManager / Unit**: 경로 요청을 큐로 처리하고, `Unit`이 `target`까지 받은 경로를 따라 이동합니다.
- **Bug0Seeker / Bug1Seeker / Bug2Seeker**: 목표 방향으로 직진하다가 장애물을 만나면 벽을 따라 도는 방식의 Bug 계열 알고리즘입니다. 이동 경로는 Gizmos로 시각화됩니다.

씬에서 Play를 누른 뒤 Scene 뷰에서 Gizmos를 켜면 격자, 탐색 트리, 최종 경로를 볼 수 있습니다.

## 정량 평가 (Benchmark)

`Assets/Benchmark/`에 알고리즘 코드를 수정하지 않고 각 씬의 컴포넌트를 그대로 호출하는 측정 하네스가 있습니다. 4개 씬이 공유하는 장애물 맵에서 시작·목표 쌍 31개(원본 1 + 무작위 30)를 모든 알고리즘에 똑같이 적용합니다.

```bash
# 1) Unity에서 측정 (에디터 메뉴: Tools > Path Planning Benchmark > Run 도 가능)
Unity -batchmode -nographics -projectPath . -executeMethod Benchmark.BenchmarkLauncher.RunBatch -logFile bench.log
# 2) 집계·그림 (pandas, matplotlib, scipy)
python3 Tools/analyze_benchmark.py
```

결과는 `BenchmarkResults/`에 저장됩니다. 요약표는 [`summary.md`](BenchmarkResults/summary.md), 그림은 `figures/`에 있습니다.

RRT/RRT\*의 `RetracePath`에는 측정용 안전장치가 하나 들어 있습니다. 새 노드가 목표 격자 노드와 겹치면 `targetNode.parent`가 자기 자신이 되어 무한 루프에 빠지는데, 이 루프를 끊고 해당 시행을 실패로 기록합니다. 그 외의 동작은 원본과 같습니다.
