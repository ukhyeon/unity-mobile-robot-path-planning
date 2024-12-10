using UnityEngine;
using System.Collections;
using System;
namespace Dijkstra
{
// 제네릭 클래스로, 힙 자료구조를 구현함, 힙에 추가될 항목 T는 반드시 IHeapItem<T> 인터페이스를 구현 해야 함.
public class Heap<T> where T : IHeapItem<T> {
	
	T[] items; // 힙에 저장된 항목들
	int currentItemCount; // 현재 힙에 저장된 항목의 개수 
	
	// 힙의 크기를 최대 크기로 지정하여 배열을 초기화
	public Heap(int maxHeapSize) {
		items = new T[maxHeapSize];
	}
	// 새로운 노드를 힙에 추가, sortup을 사용하여 추가된 노드가 올바른 위치로 이동
	public void Add(T item) {
		item.HeapIndex = currentItemCount; // 새로운 항목의 힙 인덱스를 설정
		items[currentItemCount] = item;	// 배열의 끝에 항목 추가
		SortUp(item); 					// 힙 정렬 수행 (위로 이동)
		currentItemCount++;				// 항목 개수 증가
	}
	// 가장 적은 비용을 가진 노드를 힙에서 꺼냄,
	public T RemoveFirst() {
		T firstItem = items[0];				// 루트(최상단) 항목을 가져옴
		currentItemCount--;					// 항목 개수 감소
		items[0] = items[currentItemCount];	// 마지막 항목을 루트로 이동
		items[0].HeapIndex = 0;				// 새로운 루트의 힙 인덱스 설정
		SortDown(items[0]);					// 힙 정렬 수행 (아래로 이동)
		return firstItem;					//제거된 항목 반환
	}

	public void UpdateItem(T item) {
		SortUp(item);						// 항목의 위치를 다시 정렬
	}
	
	public int Count {						// 힙에 저장된 항목의 개수를 반환
		get {
			return currentItemCount;
		}
	}

	public bool Contains(T item) {		// 힙에 특정 항목이 포함되어 있는지 확인함
		return Equals(items[item.HeapIndex], item);
	}

	void SortDown(T item) {
		while (true) {
			int childIndexLeft = item.HeapIndex * 2 + 1;   // 왼쪽 자식
			int childIndexRight = item.HeapIndex * 2 + 2;  // 오른쪽 자식
			int swapIndex = 0;

			if (childIndexLeft < currentItemCount) {       // 왼쪽 자식이 존재하는지 확인
				swapIndex = childIndexLeft;

				if (childIndexRight < currentItemCount) {  // 오른쪽 자식이 존재하는지 확인
					if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0) { // 자식 노드간 비교에서 더 큰 자식을선택
						swapIndex = childIndexRight;       // 더 큰 자식을 선택
					}
				}

				if (item.CompareTo(items[swapIndex]) < 0) { // 부모와 비교하여 조건 위반 시 교체
					Swap(item, items[swapIndex]);
				} else {
					return; // 힙 조건 만족
				}

			} else {
				return; // 자식이 없음
			}
		}
}

	
	void SortUp(T item) {
		int parentIndex = (item.HeapIndex - 1) / 2; // 부모 인덱스 계산

		while (true) {
			T parentItem = items[parentIndex];
			if (item.CompareTo(parentItem) > 0) {   // 부모보다 우선순위가 높다면 교체
				Swap(item, parentItem);
			} else {
				break; // 힙 조건 만족
			}

			parentIndex = (item.HeapIndex - 1) / 2; // 새로운 부모 계산
		}
}

	
	void Swap(T itemA, T itemB) {
		items[itemA.HeapIndex] = itemB; // 인덱스 교환
		items[itemB.HeapIndex] = itemA;
		int itemAIndex = itemA.HeapIndex;
		itemA.HeapIndex = itemB.HeapIndex;
		itemB.HeapIndex = itemAIndex;
}

	
	
	
}

public interface IHeapItem<T> : IComparable<T> {
	int HeapIndex {
		get;
		set;
	}
}

}