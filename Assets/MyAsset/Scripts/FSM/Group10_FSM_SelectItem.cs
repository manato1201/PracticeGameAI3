using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

public class Group10_FSM_SelectItem : Group10_FSM_Base
{
	int targetItemNo = -1;
	bool routeCalculating = false;
	
	public Group10_FSM_SelectItem(ItemCollector s) : base(s)
	{
		
	}
	
	public override void OnEnter()
	{
		pawn.SetMoveSpeedRatio(0f);
		targetItemNo = -1;
		
		GameManager gameManager = GameManager.instance;
		// 狙うアイテムを選びます
		int n;
		n = gameManager.NumItems();
		for(int i=0;i<n;i++){
			if(!gameManager.IsItemAvailable(i))
				continue;
				targetItemNo = i;
			break;
		}
		if(targetItemNo >= 0){
			// 狙うアイテムの位置に向けて移動を開始します
			pawn.navi.SetDestination(gameManager.ItemPosition(targetItemNo));
			routeCalculating = true;
		}else{
			routeCalculating = false;
		}
		// 次のステート（移動）へ目指しているアイテムの番号を伝えられるように記録
		pawn.targetItemNo = targetItemNo;
	}
	
	public override void OnUpdate()
	{
		// 経路探索をしていたら終了まで待つ
		if(routeCalculating){
			if(pawn.navi.IsReady()){
				routeCalculating = false;
			}
		}
	}
	
	public override void OnExit()
	{
		
	}
	
	public override ItemCollector.State CheckTransitions()
	{
		if(targetItemNo < 0){
			// もうアイテムが残っていないので適当に移動モードに変更
			return ItemCollector.State.FREE_MOVE;
		}else{
			if(routeCalculating == false){
				// 経路探索が終わったので移動開始
				return ItemCollector.State.MOVE_TO_ITEM;
			}else{
				// まだこのステートのままで待つ
				return ItemCollector.State.SELECT_ITEM;
			}
		}
	}
}

}
