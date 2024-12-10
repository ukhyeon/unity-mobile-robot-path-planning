using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace RRT
{
	

public class PathRequestManager : MonoBehaviour {

	Queue<PathRequest> pathRequestQueue = new Queue<PathRequest>(); // 경로 요청을 저장하는 큐, 이 큐에 경로 요청을 추가하고, 하나씩 처리함
	PathRequest currentPathRequest;		// 현재 처리 중인 경로 요청을 저장함

	static PathRequestManager instance; 
	Pathfinding pathfinding; // Pathfinding 컴포넌트를 참조하여 경로 찾기를 처리

	bool isProcessingPath;	// 현재 경로 찾기가 진행중인지 

	void Awake() {
		instance = this;
		pathfinding = GetComponent<Pathfinding>(); // pathfinding 컴포넌트를 가져옴
	}

	public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, Action<Vector3[], bool> callback) { // 외부에서 경로를 요청하는 메소드
		PathRequest newRequest = new PathRequest(pathStart,pathEnd,callback);
		instance.pathRequestQueue.Enqueue(newRequest);		// 
		instance.TryProcessNext(); // 다음 경로 요청 처리
	}

	void TryProcessNext() {
		if (!isProcessingPath && pathRequestQueue.Count > 0) {
			currentPathRequest = pathRequestQueue.Dequeue();
			isProcessingPath = true;
			pathfinding.StartFindPath(currentPathRequest.pathStart, currentPathRequest.pathEnd);
		}
	}

	public void FinishedProcessingPath(Vector3[] path, bool success) { // 
		currentPathRequest.callback(path,success);
		isProcessingPath = false;
		TryProcessNext();
	}

	struct PathRequest { // 경로 요청을 나타내는 구조체, callback은 경로 탐색이 완료되었을 때 호출된 콜백 함수
		public Vector3 pathStart;
		public Vector3 pathEnd;
		public Action<Vector3[], bool> callback;

		public PathRequest(Vector3 _start, Vector3 _end, Action<Vector3[], bool> _callback) {
			pathStart = _start;
			pathEnd = _end;
			callback = _callback;
		}

	}
}
}

// 7. 전체 흐름 요약
// 외부에서 **RequestPath()**로 경로 요청을 합니다.
// 경로 요청은 **pathRequestQueue**에 추가되고, **TryProcessNext()**가 호출되어 경로 찾기를 시작합니다.
// 경로 찾기 작업이 완료되면 **FinishedProcessingPath()**가 호출되어 요청한 콜백 함수에 경로 결과를 전달합니다.
// 경로 처리가 끝난 후, 다음 경로 요청을 처리하기 위해 **TryProcessNext()**가 다시 호출됩니다.
// 이 클래스의 역할
// 여러 경로 요청을 큐에 저장하고, 하나씩 처리하는 역할을 합니다.
// 경로 탐색을 요청한 후, 콜백을 통해 경로 찾기 결과를 외부에 전달합니다.