using UnityEngine;
using System.Collections;
namespace Dijkstra
{
    using System;
    using Unity.VisualScripting;
    // IHeapItem<Node> 인터페이스: 힙 자료구조에서 노드를 정렬하기 위해 사용
    using UnityEngine;

public class Node : IHeapItem<Node>
{
    public bool walkable;          // 노드가 이동 가능한지 여부
    public Vector3 worldPosition; // 노드의 월드 좌표
    public int gridX;             // 노드의 X 좌표
    public int gridY;             // 노드의 Y 좌표

    public int gCost;             // 시작 노드부터 현재 노드까지의 비용
    public Node parent;           // 이전 노드 (경로 추적용)
    int heapIndex;                // 힙에서의 인덱스 (우선순위 큐에서 사용)

    // 생성자
    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY)
    {
        walkable = _walkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridY = _gridY;
    }

    // 힙 자료 구조에서 노드의 위치를 나타냄
    public int HeapIndex
    {
        get { return heapIndex; }
        set { heapIndex = value; }
    }

    // 두 노드를 비교하여 힙 정렬에 사용
    // Dijkstra는 fCost 대신 gCost만 비교
    public int CompareTo(Node nodeToCompare)
    {
        int compare = gCost.CompareTo(nodeToCompare.gCost);
        return -compare;
    }
}


}