using UnityEngine;
using System.Collections.Generic;

namespace base2 {
    public class Bug0Seeker : MonoBehaviour {
        public Transform target;
        public float moveSpeed = 2f;
        public int rayCount = 8;        // 360도 방향을 나누는 레이 개수
        public float rayDistance = 5f;  // 레이캐스트의 감지 거리

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
                    isFollowingObstacle = true;                         // 장애물을 외곽을 따라가도록 상태 변경
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
            Vector3 avoidanceDirection = Vector3.zero;
            bool obstacleDetected = false;

            // 360도 방향으로 레이캐스트 발사
            for (int i = 0; i < rayCount; i++) {
                float angle = i * (360f / rayCount); // 각도를 나누어 레이의 방향 설정
                Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * transform.forward;
                
                RaycastHit hit;
                if (Physics.Raycast(transform.position, rayDirection, out hit, rayDistance)) {
                    // 장애물이 감지된 경우, 그 법선 벡터를 피할 방향으로 설정
                    avoidanceDirection += hit.normal;
                    obstacleDetected = true;
                }
            }

            if (obstacleDetected) {
                avoidanceDirection = avoidanceDirection.normalized;
                // 장애물 반대 방향으로 이동
                transform.position = Vector3.MoveTowards(transform.position, transform.position + avoidanceDirection, moveSpeed * Time.deltaTime);
                currentNode = grid.NodeFromWorldPoint(transform.position);
            } else {
                // 장애물이 없으면 목표 방향으로 이동
                MoveDirectlyToTarget(grid.NodeFromWorldPoint(target.position));
            }
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

            // 디버그용으로 360도 레이캐스트 시각화
            for (int i = 0; i < rayCount; i++) {
                float angle = i * (360f / rayCount);
                Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * transform.forward;
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, rayDirection * rayDistance);
            }
        }
    }
}
