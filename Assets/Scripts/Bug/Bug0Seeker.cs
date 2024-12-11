using UnityEngine;
using System.Collections.Generic;

namespace base2 {
    public class Bug0Seeker : MonoBehaviour {
        public Transform target;
        public float moveSpeed = 2f;
        public float raycastDist = 3f;

        private Grid grid;
        private Node currentNode;
        private Node nextNode;
        private Node targetNode;
        private List<Node> adjNode;

        private bool isFollowingObstacle = false; // 장애물 외곽을 따라가는 중인지 여부
        private float closestDistance = float.MaxValue; // 최단 거리 기록


        void Start() {
            grid = FindObjectOfType<Grid>();

        }

        void Update() {
            MoveTowardsTarget();
        }

        void MoveTowardsTarget() {
            currentNode = grid.NodeFromWorldPoint(transform.position);
            adjNode = grid.GetNeighbours(currentNode); // 타겟 노드좌표

            if (!isFollowingObstacle) { // 장애물을 따라가지 않는 상황
                // 직선 경로로 이동
                if (!IsPathClear(transform.position, target.position)) { // 현재 노드와 목표 노드 사이의 장애물이 없는지 확인
                    MoveDirectlyToTarget(target.position);                   // 없다면 목표 노드로 직선 방향 전진
                } 
                else {
                    //장애물에 부딪히면 외곽을 따라 이동
                    isFollowingObstacle = true;                         // 현재 노드와 목표 노드 사이의 장애물이 있다면 장애물을 외곽을 따라감
                }
            } 
            else {
                // 장애물 외곽을 따라 이동
                FollowObstacle();                                       
                // 장애물을 따라 이동 중 직선 경로가 다시 가능해지면 목표로 가는 경로로 전환
                if (!IsPathClear(transform.position, target.position)) {
                    isFollowingObstacle = false;
                }
            }
        }

        void MoveDirectlyToTarget(Vector3 target) {
            // 직선 경로로 목표 노드로 이동
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            //currentNode = grid.NodeFromWorldPoint(transform.position);
        }

        void FollowObstacle() {
            // 장애물의 외곽을 따라 이동
            List<Node> neighbours = grid.GetNeighbours(currentNode); //인접 노드 리스트
            // 왼쪽 앞 오른쪽 세개의 노드중, 앞쪽 노드가 walkable 이라면 계속해서 앞쪽으로 움직임, 이후 왼쪽 노드가 walkable 이라면 왼쪽 노드로 움직임.
            nextNode = FindNextObstacleNode(neighbours);            // 다음으로 이동할 노드 계산

            if (nextNode != null) {
                transform.position = Vector3.MoveTowards(transform.position, nextNode.worldPosition, moveSpeed * Time.deltaTime);
                currentNode = grid.NodeFromWorldPoint(transform.position);

                // 장애물 외곽을 따라가며 최단 거리 기록
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget < closestDistance) {
                    closestDistance = distanceToTarget;
        
                }
            } 
            else {
                Debug.Log("No valid path around the obstacle.");
            }
        }

        // 외곽을 따라 이동
        Node FindNextObstacleNode(List<Node> neighbours) {
            // 외곽을 따라 이동할 다음 노드를 선택
            Node closestNode = null;
            float shortestDistance = float.MaxValue;

            foreach (Node neighbour in neighbours) {
                // 위쪽 노드가 walkable이면 위쪽으로 우선 이동
                if (neighbour.gridY > currentNode.gridY && neighbour.walkable) {
                    float distanceToTarget = Vector3.Distance(neighbour.worldPosition, target.position);
                    if (distanceToTarget < shortestDistance) {
                        shortestDistance = distanceToTarget;
                        closestNode = neighbour;
                    }
                }
            }
            // 위쪽 노드가 unwalkable한 경우, 왼쪽 노드를 따라가기
            if (closestNode == null) {
                foreach (Node neighbour in neighbours) {
                    // 왼쪽 노드가 walkable이면 왼쪽으로 이동
                    if (neighbour.gridX < currentNode.gridX && neighbour.walkable) {
                        float distanceToTarget = Vector3.Distance(neighbour.worldPosition, target.position);
                        if (distanceToTarget < shortestDistance) {
                            shortestDistance = distanceToTarget;
                            closestNode = neighbour;
                        }
                    }
                }
            }



            return closestNode;
        }

        bool IsPathClear(Vector3 start, Vector3 end) {
            // 레이캐스트로 직선 경로의 장애물 여부 확인
            Vector3 direction = (end - start).normalized;
            return Physics.Raycast(start, direction, raycastDist);
        }

        void OnDrawGizmos() {
            float rayThickness = 0.6f;
            //그리드 전체를 표시
            if (adjNode != null) {
                Gizmos.color = Color.green; // 인접 노드 색상
                foreach (Node neighbour in adjNode) {
                    Gizmos.DrawCube(neighbour.worldPosition, Vector3.one*1.5f);
                    
                }
            }

            Vector3 rayDirection = (target.position - transform.position).normalized;
            if (Physics.Raycast(transform.position, rayDirection,raycastDist)){
                Gizmos.color = Color.red;
            }
            else{
                Gizmos.color = Color.blue;
            }
            Vector3 start = transform.position;
            Vector3 end = start + rayDirection * raycastDist;
            Vector3 right = Vector3.Cross(rayDirection, Vector3.up).normalized * rayThickness;
            Vector3 up = Vector3.Cross(rayDirection, Vector3.right).normalized * rayThickness;

            Gizmos.DrawLine(start + right, end + right); // 오른쪽 선
            Gizmos.DrawLine(start - right, end - right); // 왼쪽 선
            Gizmos.DrawLine(start + up, end + up);       // 위쪽 선
            Gizmos.DrawLine(start - up, end - up);
            //Gizmos.DrawRay(transform.position, rayDirection * raycastDist);


            }

                
        }
    }



