using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Dijkstra
{
    public class Pathfinding : MonoBehaviour
    {
        PathRequestManager requestManager;
        Grid grid;

        // 컴포넌트 가져오기
        void Awake()
        {
            requestManager = GetComponent<PathRequestManager>();
            grid = GetComponent<Grid>();
        }

        public void StartFindPath(Vector3 startPos, Vector3 targetPos)
        {
            StartCoroutine(FindPath(startPos, targetPos));
        }

        IEnumerator FindPath(Vector3 startPos, Vector3 targetPos) {

            Vector3[] waypoints = new Vector3[0]; // 크기가 0인 vector3 배열 초기화
            bool pathSuccess = false;
            
            Node startNode = grid.NodeFromWorldPoint(startPos);
            Node targetNode = grid.NodeFromWorldPoint(targetPos);
            
            
            if (startNode.walkable && targetNode.walkable) {
                Heap<Node> openSet = new Heap<Node>(grid.MaxSize); // 열린 리스트로 저장하는데 사용되는 힙 자료 구조.
                HashSet<Node> closedSet = new HashSet<Node>();		// 닫힌 리스트는 이미 확인한 노드를 저장
                openSet.Add(startNode);
        
                while (openSet.Count > 0) {
                    Node currentNode = openSet.RemoveFirst(); // Openset에서 가장 Cost를 가진 노드를 꺼냄
                    closedSet.Add(currentNode);					// 그 노드를 닫힌 리스트에 넣음
                    
                    if (currentNode == targetNode) {
                        pathSuccess = true;
                        break;
                    }
                    // 현재 노드의 이웃 노드의 코스트 계산
                    foreach (Node neighbour in grid.GetNeighbours(currentNode)) {
                        // walkable 하거나 이미 closedset에 넣었다면 pass
                        if (!neighbour.walkable || closedSet.Contains(neighbour)) {
                            continue;
                        }
                        // 이웃 노드와의 비용 계산
                        int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                        // 기존 이웃 노드의 cost 보다 작거나 openset이 해당 이웃노드를 가지고 있지 않다면
                        if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour)) {
                            neighbour.gCost = newMovementCostToNeighbour;
                            //neighbour.hCost = GetDistance(neighbour, targetNode);
                            neighbour.parent = currentNode;
                            
                            if (!openSet.Contains(neighbour))
                                openSet.Add(neighbour);
                        }
                    }
                }
            }
            yield return null;
            if (pathSuccess) {
                waypoints = RetracePath(startNode,targetNode);
            }
            requestManager.FinishedProcessingPath(waypoints,pathSuccess); // 경로 탐색이 끝난 이후, 경로를 요청한 컴포넌트에 결과를 전달함.
            
	}

        // 경로 재구성, 목표 노드에서 시작 노드까지 역추적
        Vector3[] RetracePath(Node startNode, Node endNode) {
		List<Node> path = new List<Node>();
		Node currentNode = endNode;
		
		while (currentNode != startNode) {
			path.Add(currentNode);
			currentNode = currentNode.parent;
		}
		Vector3[] waypoints = FullPath(path);
		Array.Reverse(waypoints); // 경로를 시작 지점부터 목표지점까지 반전
		return waypoints;
		
	}

        // 경로 간소화, 직선 부분 제거
        Vector3[] SimplifyPath(List<Node> path)
        {
            List<Vector3> waypoints = new List<Vector3>();
            Vector2 directionOld = Vector2.zero;

            for (int i = 1; i < path.Count; i++)
            {
                Vector2 directionNew = new Vector2(
                    path[i - 1].gridX - path[i].gridX,
                    path[i - 1].gridY - path[i].gridY
                );
                if (directionNew != directionOld)
                {
                    waypoints.Add(path[i].worldPosition);
                }
                directionOld = directionNew;
            }
            return waypoints.ToArray();
        }
        
        Vector3[] FullPath(List<Node> path)
        {
            List<Vector3> waypoints = new List<Vector3>();

            for (int i = 1; i < path.Count; i++)
            {
                waypoints.Add(path[i].worldPosition);
            }

            return waypoints.ToArray();
        }


        // 두 노드 간의 거리 계산
        int GetDistance(Node nodeA, Node nodeB) {
            int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
            int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);
            
            if (dstX > dstY)
                return 14*dstY + 10* (dstX-dstY);

            return 14*dstX + 10 * (dstY-dstX);
	}


    }
}
