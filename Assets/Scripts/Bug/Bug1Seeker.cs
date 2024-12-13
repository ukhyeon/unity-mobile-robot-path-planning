using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UIElements;

namespace base2 {
    public class Bug1Seeker : MonoBehaviour {
        public Transform target;
        public float moveSpeed = 10f;
        public float raycastDist_target = 3f;
        public float raycastDist_obstacle =3f;
        public float raycastDist_obstacle_digonal =3f;

        private Grid grid;
        private List<Node> PathNodeList = new List<Node>();
        //Grid gird;
        private Node currentNode;
        private Node previousCheckNode;
        private Node nextNode;
        private Node targetNode;
        private Node CheckNode;
        private List<Node> adjNode;

        private float closestDistance = float.MaxValue; // 최단 거리 기록
        private bool left_obstacle =false;
        private bool right_obstacle = false;
        private bool forward_obstacle = false;
        private bool backward_obstacle = false;
        private bool left_forward_obstacle=false;
        private bool left_backward_obstacle = false;
        private bool right_forward_obstacle = false;
        private bool right_backward_obstacle = false;
        private bool renewd=false;
    

        private Vector3 left_vector = new Vector3(-1,0,0);
        private Vector3 right_vector = new Vector3(1,0,0);
        private Vector3 forward_vector = new Vector3(0,0,1);
        private Vector3 forward_vector_test = new Vector3(0,1,0);
        private Vector3 backward_vector = new Vector3(0,0,-1);
        private bool followobstacle_once=true; 
        

        void Start() {
            grid = FindObjectOfType<Grid>();
            CheckNode=grid.NodeFromWorldPoint(transform.position);
            previousCheckNode = null;
            targetNode=grid.NodeFromWorldPoint(target.position);
        }

        void Update() {
            MoveTowardsTarget();
        }

        void MoveTowardsTarget() {
            currentNode = grid.NodeFromWorldPoint(transform.position);
            PathNodeList.Add(currentNode);
            adjNode = grid.GetNeighbours(currentNode); // 타겟 노드좌표                              
            // }!IsPathClear(transform.position, target.position)
            if (!IsPathClear(transform.position, target.position) && followobstacle_once ==true) { // 현재 노드와 목표 노드 사이의 장애물이 없는지 확인
                    MoveDirectlyToTarget(target.position);                   // 없다면 목표 노드로 직선 방향 전진
                    print("Move to target");
                } 
            else{
                    //장애물에 부딪히면 외곽을 따라 이동
                    FollowObstacle();
                    //print("FollowObstacle");                         // 현재 노드와 목표 노드 사이의 장애물이 있다면 장애물을 외곽을 따라감
                    currentNode = grid.NodeFromWorldPoint(transform.position);
                    if(currentNode==CheckNode && CheckNode==previousCheckNode && renewd==false){
                    MoveDirectlyToTarget(target.position);
                    followobstacle_once=true;
            }
                }


            
        }

        void MoveDirectlyToTarget(Vector3 target) {
            // 직선 경로로 목표 노드로 이동
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }

        void FollowObstacle() {
            followobstacle_once=false;
            // 장애물의 외곽을 따라 이동
            List<Node> neighbours = grid.GetNeighbours(currentNode); //인접 노드 리스트
            // 왼쪽 앞 오른쪽 세개의 노드중, 앞쪽 노드가 walkable 이라면 계속해서 앞쪽으로 움직임, 이후 왼쪽 노드가 walkable 이라면 왼쪽 노드로 움직임.

            float dist =float.MaxValue;
            bool nextNode = false;
            renewd = false;
            forward_obstacle = IsObstacle(currentNode.worldPosition,currentNode.worldPosition+forward_vector);
            left_obstacle = IsObstacle(currentNode.worldPosition,currentNode.worldPosition+left_vector);
            right_obstacle=IsObstacle(currentNode.worldPosition,currentNode.worldPosition+right_vector);
            backward_obstacle = IsObstacle(currentNode.worldPosition, currentNode.worldPosition+backward_vector);
            left_forward_obstacle = IsObstacle2(currentNode.worldPosition, currentNode.worldPosition+left_vector+forward_vector);
            left_backward_obstacle = IsObstacle2(currentNode.worldPosition,currentNode.worldPosition+left_vector+backward_vector);
            right_forward_obstacle = IsObstacle2(currentNode.worldPosition,currentNode.worldPosition+right_vector+forward_vector);
            right_backward_obstacle = IsObstacle2(currentNode.worldPosition,currentNode.worldPosition+right_vector+backward_vector);
            //print($"forward:   {forward_obstacle},  left:   {left_obstacle},   right:   {right_obstacle},   back:   {backward_obstacle} ");
            //print($"l_f:  {left_forward_obstacle}, l_b: {left_backward_obstacle},  r_f:  {right_forward_obstacle},  r_b:  {right_backward_obstacle} ");
            
            // 0: 왼쪽, 1: 아래, 2: 위쪽, 3: 오른쪽 
            if (forward_obstacle){
                nextNode = true;
                
                if (neighbours[0].walkable){
                    transform.position = Vector3.MoveTowards(transform.position, neighbours[0].worldPosition, moveSpeed * Time.deltaTime);
                    dist=Vector3.Distance(neighbours[0].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                        closestDistance= dist;
                        CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                        print("갱신! ");
                        }
                        return;
                    }
                    
            }
            if (left_obstacle){
                nextNode=true;
                if (neighbours[1].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[1].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[1].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("left_obstacle");
                return;
                }
                //print(neighbours[1].walkable);
            }
            if (right_obstacle){
                nextNode = true;
                if (neighbours[2].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[2].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[2].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("right_obstacle");
                return;
            }
                
            }
            if (backward_obstacle){
                nextNode = true;
                if (neighbours[3].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[3].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[3].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                }
                //print("backward_obstacle");
                return;
                }
            }
            if (left_forward_obstacle){
                nextNode = true;
                if (neighbours[0].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[0].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[0].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("left_frward_obstacle");
                return;
                }
                else{
                    transform.position = Vector3.MoveTowards(transform.position, neighbours[1].worldPosition, moveSpeed * Time.deltaTime);
                }
            }
            if (left_backward_obstacle){
                nextNode = true;
                if (neighbours[1].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[1].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[1].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("left_backward_obstacle\n\n\n");
                return;
                }
                else{
                    transform.position = Vector3.MoveTowards(transform.position, neighbours[3].worldPosition, moveSpeed * Time.deltaTime);
                }
                
            }
            if(right_forward_obstacle){
                nextNode = true;
                if (neighbours[2].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[2].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[2].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("right_froward_obstacle");
                return;
                }
                else{
                    transform.position = Vector3.MoveTowards(transform.position, neighbours[0].worldPosition, moveSpeed * Time.deltaTime);
                }
        
                
            }
            if(right_backward_obstacle){
                nextNode=true;
                if (neighbours[3].walkable){
                transform.position = Vector3.MoveTowards(transform.position, neighbours[3].worldPosition, moveSpeed * Time.deltaTime);
                dist=Vector3.Distance(neighbours[3].worldPosition,targetNode.worldPosition);
                    if(closestDistance > dist){
                    closestDistance= dist;
                    CheckNode=grid.NodeFromWorldPoint(transform.position); renewd=true;
                    print("갱신! ");
                    }
                //print("right_backward_obstacle");
                return;
                }
                else{
                    transform.position = Vector3.MoveTowards(transform.position, neighbours[2].worldPosition, moveSpeed * Time.deltaTime);
                }
            
            }
            //currentNode=grid.NodeFromWorldPoint(transform.position);
            //|| (Vector3.Distance(currentNode.worldPosition, CheckNode.worldPosition) <10f && previousCheckNode==CheckNode)
            if(nextNode==false){
                //Debug.Log("No valid path around the obstacle.");
                MoveDirectlyToTarget(target.position); 
            }
            // if(currentNode==CheckNode && CheckNode==previousCheckNode && renewd==false){
            //     MoveDirectlyToTarget(target.position);
            //     followobstacle_once=true;
            // }
            //print(currentNode==CheckNode && CheckNode==previousCheckNode && renewd==false);
            previousCheckNode=CheckNode;
            //print(closestDistance);
            //print(currentNode.gridX);
            //print(CheckNode.gridX);
            //print(currentNode==CheckNode);

        }

        bool IsPathClear(Vector3 start, Vector3 end) {
            // 레이캐스트로 직선 경로의 장애물 여부 확인
            Vector3 direction = (end - start).normalized;
            return Physics.Raycast(start, direction, raycastDist_target);
        }
        bool IsObstacle(Vector3 start, Vector3 end) {
            // 레이캐스트로 직선 경로의 장애물 여부 확인
            Vector3 direction = (end - start).normalized;
            return Physics.Raycast(start, direction, raycastDist_obstacle);
        }
        bool IsObstacle2(Vector3 start, Vector3 end) {
            // 레이캐스트로 직선 경로의 장애물 여부 확인
            Vector3 direction = (end - start).normalized;
            return Physics.Raycast(start, direction, raycastDist_obstacle_digonal);
        }

        void OnDrawGizmos() {
            float rayThickness = 0.6f;
            //그리드 전체를 표시
            
            foreach(Node n in PathNodeList){
                Gizmos.color=Color.green;
                Gizmos.DrawCube(n.worldPosition,Vector3.one);
            }

            if(!IsPathClear(transform.position, target.position) && followobstacle_once==true){
                Vector3 rayDirection = (target.position - transform.position).normalized;
                if (Physics.Raycast(transform.position, rayDirection,raycastDist_target)){
                    Gizmos.color = Color.red;
                }
                else{
                    Gizmos.color = Color.blue;
                }
                Vector3 start = transform.position;
                Vector3 end = start + rayDirection * raycastDist_target;
                Vector3 right = Vector3.Cross(rayDirection, Vector3.up).normalized * rayThickness;
                Vector3 up = Vector3.Cross(rayDirection, Vector3.right).normalized * rayThickness;

                Gizmos.DrawLine(start + right, end + right); // 오른쪽 선
                Gizmos.DrawLine(start - right, end - right); // 왼쪽 선
                Gizmos.DrawLine(start + up, end + up);       // 위쪽 선
                Gizmos.DrawLine(start - up, end - up);
                //Gizmos.DrawRay(transform.position, rayDirection * raycastDist);
                }
            else{
                    if(!renewd && CheckNode != null){
                        Gizmos.color=Color.blue;
                        Gizmos.DrawCube(CheckNode.worldPosition,Vector3.one*2f);
                    }
                    Vector3 rayDirection1 = (target.position - target.position+forward_vector).normalized;
                    Vector3 rayDirection2 = (target.position - target.position+left_vector).normalized;
                    Vector3 rayDirection3 = (target.position - target.position+right_vector).normalized;
                    Vector3 rayDirection4 = (target.position - target.position+backward_vector).normalized;
                    if (Physics.Raycast(transform.position, rayDirection1,raycastDist_obstacle) | Physics.Raycast(transform.position, rayDirection2,raycastDist_obstacle) |Physics.Raycast(transform.position, rayDirection3,raycastDist_obstacle)| Physics.Raycast(transform.position, rayDirection4,raycastDist_obstacle)){
                        Gizmos.color = Color.red;
                    }
                    else{
                        Gizmos.color = Color.blue;
                    }
                    Gizmos.DrawRay(transform.position,rayDirection1 * raycastDist_obstacle);
                    Gizmos.DrawRay(transform.position,rayDirection2 * raycastDist_obstacle);
                    Gizmos.DrawRay(transform.position,rayDirection3 * raycastDist_obstacle);
                    Gizmos.DrawRay(transform.position,rayDirection4 * raycastDist_obstacle);


                    Vector3 rayDirection5 = (target.position - target.position+forward_vector+left_vector).normalized;
                    Vector3 rayDirection6 = (target.position - target.position+forward_vector+right_vector).normalized;
                    Vector3 rayDirection7 = (target.position - target.position+backward_vector+right_vector).normalized;
                    Vector3 rayDirection8 = (target.position - target.position+backward_vector+left_vector).normalized;
                    if (Physics.Raycast(transform.position, rayDirection5,raycastDist_obstacle_digonal) ||
                       Physics.Raycast(transform.position, rayDirection6,raycastDist_obstacle_digonal) ||
                       Physics.Raycast(transform.position, rayDirection7,raycastDist_obstacle_digonal) ||
                       Physics.Raycast(transform.position, rayDirection8,raycastDist_obstacle_digonal) ){
                        Gizmos.color = Color.red;
                    }
                    else{
                        Gizmos.color = Color.blue;
                    }
                    Gizmos.DrawRay(transform.position,rayDirection5 * raycastDist_obstacle_digonal);
                    Gizmos.DrawRay(transform.position,rayDirection6 * raycastDist_obstacle_digonal);
                    Gizmos.DrawRay(transform.position,rayDirection7 * raycastDist_obstacle_digonal);
                    Gizmos.DrawRay(transform.position,rayDirection8 * raycastDist_obstacle_digonal);

                }
            }
        }   
    }




