using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Group10
{

public class Group10_FSM_FreeMove : Group10_FSM_Base
{
	int step = 0;
	float timer = 0f;
	bool attackReady = false;
	
	public Group10_FSM_FreeMove(ItemCollector s) : base(s)
	{
		
	}
	
	// このステートに遷移した最初に1度実行する
	public override void OnEnter()
	{
		pawn.SetMoveSpeedRatio(1f);
		step = 0;
		attackReady = false;
	}
	
	// このステートで毎回実行する
	public override void OnUpdate()
	{
		int n;
		
		switch(step){
			case 0:
			// 方向を選ぶ
			n = Random.Range(0,4);
			pawn.SetDirection(pawn.cardinalDirections[n]);
			// 時間
			timer = Random.Range(1f,5f);
			step++;
			break;
			
			case 1:
			// 目の前が壁だったら方向を再選択
			if(pawn.DistanceToWall(pawn.GetDirection())<0.7f){
				step = 0;
			}
			timer -= Time.deltaTime;
			if(timer<0f)
				step = 2;
			break;
			
			case 2:
			// 色々な方向に爆弾を投げやすいように移動マスの中心付近に行く
			pawn.SetMoveSpeedRatio(0.5f);
			Vector3 pos = Vector3.Scale(pawn.GetPosition(), pawn.GetDirection());
			pos.x = pos.x % 1;
			pos.z = pos.z % 1;
			if(pos.x < 0.1f && pos.z < 0.1f){
				attackReady = true;
			}else if(pawn.DistanceToWall(pawn.GetDirection())<0.45f){
				step = 0;
			}
			break;
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
		if(attackReady){
			return ItemCollector.State.ATTACK;
		}else{
			return ItemCollector.State.FREE_MOVE;
		}
	}
}

}
