using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace Dijkstra
{
public class Grid : MonoBehaviour {

    public bool displayGridGizmos; // 그리드를 시각적으로 표시할지 여부
    public LayerMask unwalkableMask; // 이동할 수 없는 영역을 정의하는 레이어
    public Vector2 gridWorldSize; // 그리드의 가로, 세로 크기 (월드 단위)
    public float nodeRadius; // 각 노드의 반지름 크기
    Node[,] grid; // 노드를 2차원 배열로 저장

    float nodeDiameter; // 노드의 직경 (반지름 * 2)
    int gridSizeX, gridSizeY; // 그리드의 X축, Y축 노드 개수

    void Awake() {
        // 노드 직경과 그리드의 크기를 계산
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }

    public int MaxSize {
        get {
            // 그리드 전체 노드 수 반환
            return gridSizeX * gridSizeY;
        }
    }

    // 모든 노드를 반환하는 속성
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

    void CreateGrid() {
        // 그리드 초기화
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position 
                                - Vector3.right * gridWorldSize.x / 2 
                                - Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++) {
            for (int y = 0; y < gridSizeY; y++) {
                // 각 노드의 월드 좌표 계산
                Vector3 worldPoint = worldBottomLeft 
                                   + Vector3.right * (x * nodeDiameter + nodeRadius) 
                                   + Vector3.forward * (y * nodeDiameter + nodeRadius);
                // 현재 위치가 이동 가능한지 여부 확인
                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public List<Node> GetNeighbours(Node node) {
        // 특정 노드의 이웃 노드들을 찾음
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                // 자신을 제외한 주변 노드 확인
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                // 그리드 범위 내에 있는지 확인 후 추가
                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY) {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbours;
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

    void OnDrawGizmos() {
        // Gizmo를 사용해 그리드를 시각적으로 표시
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        if (grid != null && displayGridGizmos) {
            foreach (Node n in grid) {
                Gizmos.color = (n.walkable) ? Color.white : Color.red;
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));
            }
        }
    }
}

}