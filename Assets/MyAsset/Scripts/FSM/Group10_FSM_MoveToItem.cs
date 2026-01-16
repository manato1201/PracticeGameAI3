using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

public class Group10_FSM_MoveToItem : Group10_FSM_Base
{
	bool moveEnd;
	
	public Group10_FSM_MoveToItem(ItemCollector s) : base(s)
	{
		
	}
	
	// このステートに遷移した最初に1度実行する
	public override void OnEnter()
	{
		pawn.SetMoveSpeedRatio(1f);
		moveEnd = false;
	}
	
	// このステートで毎回実行する
	public override void OnUpdate()
	{
		if(pawn.navi.IsReady()){
			// 作成された経路に従って移動します
			pawn.SetDirection(pawn.navi.MoveDirection());
			// 目的地についたら、次の行動を設定します
			if(pawn.navi.IsArrived()){
				moveEnd = true;
			}
		}
		
		// 狙っているアイテムが消えたら移動終了
		if(!GameManager.instance.IsItemAvailable(pawn.targetItemNo)){
			moveEnd = true;
		}		
	}
	
	// このステートを終了する時に1度実行する
	public override void OnExit()
	{
		
	}
	
	// 遷移先のステートを返す
	// 今のステートを継続する時は、現在のステートを返す
	public override ItemCollector.State CheckTransitions()
	{
		if(moveEnd){
			// 移動が終了したら、次のアイテムへ移動する
			return ItemCollector.State.SELECT_ITEM;
		}else{
			return ItemCollector.State.MOVE_TO_ITEM;
		}
	}
}

}
