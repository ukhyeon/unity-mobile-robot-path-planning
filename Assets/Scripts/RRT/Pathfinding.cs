using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace RRT
{
    public class Pathfinding : MonoBehaviour
    {
        Grid grid;
        PathRequestManager requestManager;

        public float maxStepSize = 2.0f; // RRT 확장 시 한 번에 이동할 최대 거리
        public float maxIterations = 100; // 최대 반복 횟수

        private List<Node> treeNodes = new List<Node>();
        private HashSet<Node> visitedNodes = new HashSet<Node>();

        void Awake() {
		    requestManager = GetComponent<PathRequestManager>();
		    grid = GetComponent<Grid>();
	    }

        public void StartFindPath(Vector3 startPos, Vector3 targetPos) {
		    StartCoroutine(FindPath(startPos,targetPos));
	    }

        IEnumerator FindPath(Vector3 startPos, Vector3 targetPos)
        {
            Vector3[] waypoints = new Vector3[0]; // 크기가 0인 Vector3 배열 초기화
            bool pathSuccess = false;
            int iter =0;
            Node startNode = grid.NodeFromWorldPoint(startPos);
            Node targetNode = grid.NodeFromWorldPoint(targetPos);

            // Start and end positions are walkable
            if (startNode.walkable && targetNode.walkable)
            {
                // RRT 알고리즘의 트리 시작점
                treeNodes.Add(startNode);

                // 목표점까지 도달할 수 있을 때까지 반복
                while (iter < maxIterations)
                {   
                    iter++;
                    // 랜덤한 점을 찾음
                    Vector3 randomPoint = GetRandomPoint();
                    Node randomNode = grid.NodeFromWorldPoint(randomPoint);

                    // 트리에서 랜덤 점까지의 가장 가까운 노드를 찾음
                    Node nearestNode = GetNearestNode(randomPoint, treeNodes);

                    // 랜덤 점과 가까운 노드를 연결하기 위한 노드 계산
                    Vector3 newPoint = GetNewPoint(nearestNode.worldPosition, randomPoint);

                    // 새로운 점이 walkable한지 확인
                    Node newNode = grid.NodeFromWorldPoint(newPoint);
                    if (newNode.walkable && !visitedNodes.Contains(newNode)){   
                        treeNodes.Add(newNode);
                        visitedNodes.Add(newNode);
                        //Debug.Log("경로 탐색");
                        newNode.parent = nearestNode;

                        // 목표에 도달했는지 확인
                        if (Vector3.Distance(newNode.worldPosition, targetNode.worldPosition) < 3f)
                        {   
                            targetNode.parent=newNode;
                            pathSuccess = true;
                            Debug.Log(" 목표에 도달");
                            break;
                        }
                    } 
                    else{ 
                        //Debug.Log("탐새중인 경로가 없음");
                        continue; 
                    }
                }
            }   

            // 경로가 성공적으로 찾은 경우에만 retrace path
            if (pathSuccess){
                waypoints = RetracePath(startNode, targetNode);
                Debug.Log("경로 찾기 성공");
            }
            else{
                Debug.Log("경로 찾기 실패");
            }

            // 경로 탐색 완료 후 요청자에게 경로 반환
            requestManager.FinishedProcessingPath(waypoints, pathSuccess);
            yield return null;
        }

        // 그리드를 랜덤한 점에 생성함, 
        Vector3 GetRandomPoint(){
            // 목표 지점 주위의 공간을 탐색할 수 있도록 범위 설정
            float randomX = UnityEngine.Random.Range(grid.WorldBottomLeft.x, grid.WorldTopRight.x);
            float randomZ = UnityEngine.Random.Range(grid.WorldBottomLeft.z, grid.WorldTopRight.z);

            return new Vector3(randomX, 0, randomZ);  // y 값은 0으로 고정, 2D 환경을 가정
        }
        // 목표 노드에서 시작노드까지 parent를 따라가며 경로를 추적, 실제 경로를 반환
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
        
        // 경로는 리스트로 저장된 후 FullPath에서 Vector3[] 로 변환됨
        Vector3[] FullPath(List<Node> path)
        {
            List<Vector3> waypoints = new List<Vector3>();

            for (int i = 1; i < path.Count; i++)
            {
                waypoints.Add(path[i].worldPosition);
            }

            return waypoints.ToArray();
        }
        // 주어진 점에 대해, 트리 노드들 중 가장 가까운 노드를 찾는 함수, 트리 확장시 이 함수를 통해 랜덤 점에 대해 가장 가까운 노드를 찾음
        Node GetNearestNode(Vector3 point, List<Node> treeNodes){
            Node nearestNode = null;
            float closestDistance = Mathf.Infinity; // 초기에는 무한대로 설정하여 가장 가까운 노드를 찾음

            foreach (Node node in treeNodes) 
            {
                float distance = Vector3.Distance(node.worldPosition, point); // 트리 노드와 랜덤 점 사이의 거리를 계산
                if (distance < closestDistance) //만약 현재 노드가 지금까지 가장 가까운 노드라면
                {
                    closestDistance = distance; // 거리 업데이트
                    nearestNode = node; // 가장 가까운 노드를 업데이트
                }
            }

            return nearestNode;
        }
        // 시작 지점에서 목표 지점까지의 방향을 계산하고, 한번에 최대 이동거리 만큼 새로운 점을 계산 
        Vector3 GetNewPoint(Vector3 start, Vector3 target){
            // 방향 벡터 계산
            Vector3 direction = (target - start).normalized;

            // 최대 이동 거리만큼만 이동
            Vector3 newPoint = start + direction * maxStepSize;

            return newPoint;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;  // 노드를 나타내는 색상

            // 모든 트리 노드들 그리기
            foreach (Node node in treeNodes)
            {
                Gizmos.DrawSphere(node.worldPosition, grid.nodeRadius);
            }

            // 부모-자식 노드 간의 선을 그리기
            Gizmos.color = Color.red;  // 트리의 연결을 나타내는 색상

            foreach (Node node in treeNodes)
            {
                if (node.parent != null)
                {
                    Gizmos.DrawLine(node.worldPosition, node.parent.worldPosition);  // 부모와 자식 노드를 연결
                }
            }
        }

    }
}
