using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;

namespace RRTstar
{
    public class Pathfinding : MonoBehaviour
    {
        Grid grid;
        PathRequestManager requestManager;

        public float maxStepSize = 2.0f;
        public float maxIterations = 100;
        public float searchRadius = 5.0f; // 새 노드 주변에서 최적화할 반경

        private List<Node> treeNodes = new List<Node>();
        private HashSet<Node> visitedNodes = new HashSet<Node>();

        public List<Node> GetTreeNodes(){
            return treeNodes;
        }

        void Awake() {
            requestManager = GetComponent<PathRequestManager>();
            grid = GetComponent<Grid>();
        }

        public void StartFindPath(Vector3 startPos, Vector3 targetPos) {
            StartCoroutine(FindPath(startPos, targetPos));
        }

        IEnumerator FindPath(Vector3 startPos, Vector3 targetPos)
        {
            Vector3[] waypoints = new Vector3[0];
            bool pathSuccess = false;
            int iter = 0;

            Node startNode = grid.NodeFromWorldPoint(startPos);
            Node targetNode = grid.NodeFromWorldPoint(targetPos);

            if (startNode.walkable && targetNode.walkable)
            {
                treeNodes.Add(startNode);

                while (iter < maxIterations)
                {
                    iter++;
                    Vector3 randomPoint = GetRandomPoint();
                    Node randomNode = grid.NodeFromWorldPoint(randomPoint);

                    Node nearestNode = GetNearestNode(randomPoint, treeNodes);
                    Vector3 newPoint = GetNewPoint(nearestNode.worldPosition, randomPoint);
                    Node newNode = grid.NodeFromWorldPoint(newPoint);

                    if (newNode.walkable && !visitedNodes.Contains(newNode))
                    {
                        treeNodes.Add(newNode);
                        visitedNodes.Add(newNode);
                        newNode.parent = nearestNode;
                        newNode.cost = nearestNode.cost + Vector3.Distance(nearestNode.worldPosition, newNode.worldPosition);

                        OptimizeTree(newNode);

                        if (Vector3.Distance(newNode.worldPosition, targetNode.worldPosition) < 3f)
                        {
                            targetNode.parent = newNode;
                            pathSuccess = true;
                            break;
                        }
                    }
                }
            }

            if (pathSuccess)
            {
                waypoints = RetracePath(startNode, targetNode);
                Debug.Log("경로 찾기 성공");
            }
            else
            {
                Debug.Log("경로 찾기 실패");
            }

            requestManager.FinishedProcessingPath(waypoints, pathSuccess);
            yield return null;
        }

        void OptimizeTree(Node newNode)
        {
            List<Node> nearbyNodes = GetNearbyNodes(newNode);

            foreach (Node nearbyNode in nearbyNodes)
            {
                float newCost = newNode.cost + Vector3.Distance(newNode.worldPosition, nearbyNode.worldPosition);
                if (newCost < nearbyNode.cost)
                {
                    nearbyNode.parent = newNode;
                    nearbyNode.cost = newCost;
                }
            }
        }

        List<Node> GetNearbyNodes(Node centerNode)
        {
            List<Node> nearbyNodes = new List<Node>();
            foreach (Node node in treeNodes)
            {
                if (Vector3.Distance(node.worldPosition, centerNode.worldPosition) <= searchRadius)
                {
                    nearbyNodes.Add(node);
                }
            }
            return nearbyNodes;
        }

        Vector3 GetRandomPoint() {
            float randomX = UnityEngine.Random.Range(grid.WorldBottomLeft.x, grid.WorldTopRight.x);
            float randomZ = UnityEngine.Random.Range(grid.WorldBottomLeft.z, grid.WorldTopRight.z);

            return new Vector3(randomX, 0, randomZ);
        }

        Vector3[] RetracePath(Node startNode, Node endNode) {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

            while (currentNode != startNode) {
                if(currentNode ==null){
                    Debug.Log("currentNode is Null");
                    break;
                }
                path.Add(currentNode);
                currentNode = currentNode.parent;
            }
            Vector3[] waypoints = FullPath(path);
            Array.Reverse(waypoints);
            return waypoints;
        }

        Vector3[] FullPath(List<Node> path) {
            List<Vector3> waypoints = new List<Vector3>();
            for (int i = 1; i < path.Count; i++) {
                waypoints.Add(path[i].worldPosition);
            }
            return waypoints.ToArray();
        }

        Node GetNearestNode(Vector3 point, List<Node> treeNodes) {
            Node nearestNode = null;
            float closestDistance = Mathf.Infinity;

            foreach (Node node in treeNodes) {
                float distance = Vector3.Distance(node.worldPosition, point);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    nearestNode = node;
                }
            }

            return nearestNode;
        }

        Vector3 GetNewPoint(Vector3 start, Vector3 target) {
            Vector3 direction = (target - start).normalized;
            return start + direction * maxStepSize;
        }

        void OnDrawGizmos() {
            Gizmos.color = Color.green;
            foreach (Node node in treeNodes) {
                Gizmos.DrawSphere(node.worldPosition, grid.nodeRadius);
            }

            Gizmos.color = Color.red;
            foreach (Node node in treeNodes) {
                if (node.parent != null) {
                    Gizmos.DrawLine(node.worldPosition, node.parent.worldPosition);
                }
            }
        }
    }
}
