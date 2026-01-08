/*
	Start() を参考に、ターゲットの数と位置を取得しましょう
	
	Update() を参考に、ターゲットを選んで、そこにたどり着くように移動しましょう
	
	ターゲットを取ったら、TargetHit(int index) が呼び出されます。
	index番のターゲットは取得済になるので、いい感じに処理しましょう
*/

using UnityEngine;
namespace Group05
{

public class Group05Player : Pawn
{
	public int set=0;
	
	int numTargets;
	Vector3[] targetPositions;
	int nextTarget;
	
	int step = 0;
	
	NavMeshTool navi;
	
	// Start is called before the first frame update
	void Start()
	{
		// 経路探索機能を取得します
		navi = GetNavMeshTool();
		
		// 今回のターゲットの数と場所を取得します
		numTargets = GameManager.instance.NumTargets();
		targetPositions = new Vector3[numTargets];
		for(int i=0; i<numTargets; i++){
			targetPositions[i] = GameManager.instance.TargetPosition(i);
		}
		
		nextTarget = 0;
	}

	// Update is called once per frame
	void Update()
	{
		switch(step){
			case 0:
				SetMoveSpeed(0f);
				// 目的地を設定します
				navi.SetDestination(targetPositions[nextTarget]);
				step++;
				break;
			case 1:
				// 経路探索が終わるまで待ちます
				if(!navi.IsReady()){
					break;
				}
				step++;
				break;
			case 2:
				// 作成された経路に従って移動します
				navi.UpdateCurrentPosition();
				SetDirection(navi.MoveDirection());
				SetMoveSpeed(5f);
				// 目的地についたら、次の行動を設定します
				if(navi.IsArrived()){
					step = 0;
				}
				break;
		}
	}
	
	public override void TargetHit(int index)
	{
		// ターゲットと接触したら、そのターゲットの番号がわかります
		Debug.Log("Get:"+index);
	}
	
}
		
}	// Group05


