using UnityEngine;
using System.Collections;
namespace Astar
{
// IHeapItem<Node> 인터페이스: 힙 자료구조에서 노드를 정렬하기 위해 사용
public class Node : IHeapItem<Node> {
	
	public bool walkable;
	public Vector3 worldPosition;
	public int gridX;
	public int gridY;

	public int gCost; // 비용함수
	public int hCost;
	public Node parent;
	int heapIndex; // 힙 자료구조에서 노드의 인덱스
	
	public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY) {
		walkable = _walkable;
		worldPosition = _worldPos;
		gridX = _gridX;
		gridY = _gridY;
	}
	// A* 알고리즘에서 경로 탐색 순서를 결정하는 비용 
	public int fCost {
		get {
			return gCost + hCost;
		}
	}
	// 힙 자료 구조에서 노드의 위치를 나타냄
	public int HeapIndex {
		get {
			return heapIndex;
		}
		set {
			heapIndex = value;
		}
	}

	// 두 노드를 비교하여 힙 정렬에 사용, 노드의 우선 순위 비교 fCost가 낮은 노드를 우선, fCost가 같다면 hCost가 낮은 노드 우선, -compare를 사용하는 이유는 경로의 정렬 방향을 반전 시키기 위함
	public int CompareTo(Node nodeToCompare) {
		int compare = fCost.CompareTo(nodeToCompare.fCost); // fCost가 nodeToCompare.fCost 보다 작다면 -1 반환
		if (compare == 0) {
			compare = hCost.CompareTo(nodeToCompare.hCost); // 두 fcost가 같다면 h cost를 비교 hcost가 Comapare.hcost보다 크다면 1 반환
		}
		return -compare;
	}
}
}