using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace base2{
public class Grid : MonoBehaviour {

    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;
    Node[,] grid;

    float nodeDiameter;
    int gridSizeX, gridSizeY;

    void Awake() {
        nodeDiameter = nodeRadius*2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x/nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y/nodeDiameter);
        CreateGrid();
    }
    public int MaxSize {
            get {
                // 그리드 전체 노드 수 반환
                return gridSizeX * gridSizeY; // 100 * 100 
            }
        }
    void CreateGrid() {
        grid = new Node[gridSizeX,gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x/2 - Vector3.forward * gridWorldSize.y/2;

        for (int x = 0; x < gridSizeX; x ++) {
            for (int y = 0; y < gridSizeY; y ++) {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                bool walkable = !(Physics.CheckSphere(worldPoint,nodeRadius,unwalkableMask));
                grid[x,y] = new Node(walkable,worldPoint, x,y);
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



    public List<Node> GetNeighbours(Node node) {
            // 특정 노드의 이웃 노드들을 찾음
            List<Node> neighbours = new List<Node>();

            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    // 자신을 제외한 주변 노드 확인
                    if (x == 0 && y == 0) continue;
                    if (x * y == 1 | x * y ==-1 ) continue;

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
        float percentX = (worldPosition.x + gridWorldSize.x/2) / gridWorldSize.x;
        float percentY = (worldPosition.z + gridWorldSize.y/2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX-1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY-1) * percentY);
        return grid[x,y];
    }

    public List<Node> path;
    // void OnDrawGizmos() {
    //     Gizmos.DrawWireCube(transform.position,new Vector3(gridWorldSize.x,1,gridWorldSize.y));

    //     if (grid != null) {
    //         foreach (Node n in grid) {
    //             Gizmos.color = (n.walkable)?Color.white:Color.red;
    //             if (path != null)
    //                 if (path.Contains(n))
    //                     Gizmos.color = Color.black;
    //             Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter-.1f));
    //         }
    //     }
    // }


}
}