using UnityEngine;
using System.Collections;
namespace RRTstar
{
    public class Node
    {
        public Vector3 position; // 노드의 위치
        public Node parent;      // 부모 노드
        public float cost;       // 시작점부터 현재 노드까지의 비용 (가중치)
        public bool walkable;
        public Vector3 worldPosition;
        public int gridX;
	    public int gridY;

        public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY) {
            walkable = _walkable;
            worldPosition = _worldPos;
            gridX = _gridX;
            gridY = _gridY;
	    }

    
    }
}


