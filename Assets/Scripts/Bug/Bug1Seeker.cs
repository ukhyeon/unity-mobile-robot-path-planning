using UnityEngine;
using System.Collections.Generic;

namespace base2 {
    public class Bug1Seeker : MonoBehaviour {
        public Transform target;
        public float moveSpeed = 2f;

        private Grid grid;
        private Node currentNode;
        private Node nextNode;

        private bool isFollowingObstacle = false; // 장애물 외곽을 따라가는 중인지 여부
        private float closestDistance = float.MaxValue; // 최단 거리 기록

        void Start() {
            grid = FindObjectOfType<Grid>();
            currentNode = grid.NodeFromWorldPoint(transform.position);
        }

        void Update() {
            MoveTowardsTarget();
        }

        void MoveTowardsTarget() {
            Node targetNode = grid.NodeFromWorldPoint(target.position); // 타겟 노드좌표

            if (!isFollowingObstacle) { // 장애물을 따라가지 않는 상황
                // 직선 경로로 이동
                if (IsPathClear(transform.position, target.position)) { // 현재 노드와 목표 노드 사이의 장애물이 없는지 확인
                    MoveDirectlyToTarget(targetNode);                   // 없다면 목표 노드로 직선 방향 전진
                } else {
                    // 장애물에 부딪히면 외곽을 따라 이동
                    isFollowingObstacle = true;                         // 현재 노드와 목표 노드 사이의 장애물이 있다면 장애물을 외곽을 따라감
                    FollowObstacle();
                }
            } else {
                // 장애물 외곽을 따라 이동
                FollowObstacle();                                       
                // 장애물을 따라 이동 중 직선 경로가 다시 가능해지면 목표로 가는 경로로 전환
                if (IsPathClear(transform.position, target.position)) {
                    isFollowingObstacle = false;
                    MoveDirectlyToTarget(targetNode);
                }
            }
        }

        void MoveDirectlyToTarget(Node targetNode) {
            // 직선 경로로 목표 노드로 이동
            transform.position = Vector3.MoveTowards(transform.position, targetNode.worldPosition, moveSpeed * Time.deltaTime);
            currentNode = grid.NodeFromWorldPoint(transform.position);
        }

        void FollowObstacle() {
            // 장애물의 외곽을 따라 이동
            List<Node> neighbours = grid.GetNeighbours(currentNode); //인접 노드 리스트
            nextNode = FindNextObstacleNode(neighbours);            // 다음으로 이동할 노드 계산

            if (nextNode != null) {
                transform.position = Vector3.MoveTowards(transform.position, nextNode.worldPosition, moveSpeed * Time.deltaTime);
                currentNode = grid.NodeFromWorldPoint(transform.position);

                // 장애물 외곽을 따라가며 최단 거리 기록
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget < closestDistance) {
                    closestDistance = distanceToTarget;
        
                }
            } else {
                Debug.Log("No valid path around the obstacle.");
            }
        }

        // 외곽을 따라 이동
        Node FindNextObstacleNode(List<Node> neighbours) {
            // 외곽을 따라 이동할 다음 노드를 선택
            Node closestNode = null;
            float shortestDistance = float.MaxValue;

            foreach (Node neighbour in neighbours) {
                if (neighbour.walkable) {
                    float distanceToTarget = Vector3.Distance(neighbour.worldPosition, target.position);
                    if (distanceToTarget < shortestDistance) {
                        shortestDistance = distanceToTarget;
                        closestNode = neighbour;
                    }
                }
            }

            return closestNode;
        }

        bool IsPathClear(Vector3 start, Vector3 end) {
            // 레이캐스트로 직선 경로의 장애물 여부 확인
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            return !Physics.Raycast(start, direction, distance);
        }

        void OnDrawGizmos() {
            if (target != null) {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}
