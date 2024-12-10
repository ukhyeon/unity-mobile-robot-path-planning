using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RRTstar
{
    public class Grid : MonoBehaviour
    {
        public LayerMask unwalkableMask; // 장애물로 간주할 레이어
        public Vector2 gridWorldSize;    // 그리드 월드 크기 100, 100
        public float nodeRadius;         // 노드의 반지름 0.5
        public Node[,] grid;             // 2D 노드 배열

        float nodeDiameter; 
        int gridSizeX, gridSizeY; // 그리드의 크기

        void Awake()
        {
            nodeDiameter = nodeRadius * 2; // 1
            gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter); // 100/ 1 =100
            gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter); 
            CreateGrid();
        }
        public int MaxSize {
            get {
                // 그리드 전체 노드 수 반환
                return gridSizeX * gridSizeY; // 100 * 100 
            }
        }

        void CreateGrid()
        {
            grid = new Node[gridSizeX, gridSizeY]; // 노드 100,100
            Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;

            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                    bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask)); // 장애물 여부 확인
                    grid[x, y] = new Node(walkable,worldPoint,x,y);
                }
            }
        }
        public List<Node> AllNodes {
            get {
                List<Node> allNodes = new List<Node>();
                for (int x = 0; x < gridSizeX; x++) {
                    for (int y = 0; y < gridSizeY; y++) {
                        allNodes.Add(grid[x, y]);
                    }
                }
                return allNodes;
            }
        }


        public Node NodeFromWorldPoint(Vector3 worldPosition) {
            // 월드 좌표를 그리드 좌표로 변환
            float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
            float percentY = (worldPosition.z + gridWorldSize.y / 2) / gridWorldSize.y;
            percentX = Mathf.Clamp01(percentX);
            percentY = Mathf.Clamp01(percentY);

            int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
            int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
            return grid[x, y];
        }

        public Vector3 WorldBottomLeft {
            get {
                return transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;
            }
        }

        public Vector3 WorldTopRight {
            get {
                return transform.position + Vector3.right * gridWorldSize.x / 2 + Vector3.forward * gridWorldSize.y / 2;
            }

        }
    }
}