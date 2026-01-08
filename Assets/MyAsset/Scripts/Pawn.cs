/*
	キャラクターを動かすルール
	
	経路探索機能を提供するコンポーネントを取得します
	NavMeshTool GetNavMeshTool()
	
	位置を取得する
	Vector3 GetPosition()
	
	向きを設定する。ただし、y方向はかならず０に設定される
	void SetDirection(Vector3)
	
	移動速度を設定する。ただし最大値を超えることはできない。
	(最大値は2.5なので、適当に10などを設定すれば最大速度になる)
	void SetMoveSpeed(float)

	参考用
	public float DistanceToWall()
		自キャラ正面方向の壁までの距離を返します

	public bool FrontCheckBomb(ref float distance)
		自分正面の視線上に爆弾があれば true を返し、distance に爆弾までの距離を格納する
		正面視線上に爆弾がなければ false を返す

	public bool FrontCheckAlly(ref float distance)
		自分正面の視線上に味方がいれば true を返し、distance に味方までの距離を格納する
		正面視線上に味方がいなければ false を返す
		
	現在の向きを取得する
	Vector3 GetDirection()
	
	現在の移動速度を取得する
	float GetMoveSpeed()
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : MonoBehaviour {
	
	float moveSpeedMax = 2.5f;
	float moveSpeed = 0f;
	Vector3 direction = new Vector3(1f,0f,0f);
	Vector3 position;
	Rigidbody rb;
    Animator animator;
	NavMeshTool navi;
	
	public NavMeshTool GetNavMeshTool()
	{
		return navi;
	}
	
	public Vector3 GetPosition()
	{
		return position;
	}
	
	public Vector3 GetDirection()
	{
		return direction;
	}
	
	public void SetDirection(Vector3 dir)
	{
		direction = dir;
		direction.y = 0f;
		direction = direction.normalized;
	}
	
	public float GetMoveSpeed()
	{
		return moveSpeed;
	}
	
	public void SetMoveSpeed(float speed)
	{
		if(speed > moveSpeedMax){
			speed = moveSpeedMax;
		}
		else if(speed < 0f){
			speed = 0f;
		}
		moveSpeed = speed;
	}
	
	// 壁までの距離を返す
	// 正面に壁がなければ 0 が返る
	public float DistanceToWall()
	{
		Debug.DrawRay(position, direction, Color.white);
		GameManager.instance.DispStr("Ray Dir:" + direction);

		RaycastHit hit;
		Ray ray = new Ray(position, direction);
		LayerMask layer = ~LayerMask.NameToLayer("Wall");
		
		Physics.Raycast(ray, out hit, Mathf.Infinity, layer);
		if(hit.collider){
			Vector3 d = hit.transform.position - position;
			return d.magnitude;
		}else{
			return 0f;
		}
	}
	
	// 自分正面の視線上に爆弾があれば true を返し、distance に爆弾までの距離を格納する
	// 正面視線上に爆弾がなければ false を返す
	public bool FrontCheckBomb(ref float distance)
	{
		Vector3 start = position;
		start.y = 0.5f;	// 地表ではない
		Ray ray = new Ray(start, direction);
		RaycastHit hit;
		
		Physics.Raycast(ray, out hit);
		if(hit.collider != null && hit.collider.CompareTag("Bomb")){
			Vector3 d = hit.transform.position - position;
			distance = d.magnitude;
			return true;
		}
		
		distance = float.PositiveInfinity;
		return false;
	}
	
	// 自分正面の視線上に味方がいれば true を返し、distance に味方までの距離を格納する
	// 正面視線上に味方がいなければ false を返す
	public bool FrontCheckAlly(ref float distance)
	{
		Vector3 start = position;
		start.y = 0.5f;	// 地表ではない
		Ray ray = new Ray(start, direction);
		Debug.DrawRay(start, direction, Color.cyan);
		RaycastHit hit;
		
		Physics.Raycast(ray, out hit);
		if(hit.collider != null && hit.collider.CompareTag("Player")){
			Vector3 d = hit.transform.position - position;
			distance = d.magnitude;
			return true;
		}
		
		distance = float.PositiveInfinity;
		return false;
	}
	
	// =======================================================================
	void Awake()
	{
		rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
		navi = new NavMeshTool(gameObject, GetComponent<UnityEngine.AI.NavMeshAgent>());

		position = transform.position;
		direction = rb.linearVelocity.normalized;
	}
	
	// Unity では Update() 前に実行される
	void FixedUpdate()
	{
		position = transform.position;
		direction = rb.linearVelocity.normalized;
	}
	
	// Update() 後に実行
	void LateUpdate()
	{
		Vector3 vel = direction.normalized * moveSpeed;
		rb.linearVelocity = vel;
		Vector3 target = this.transform.position + direction;
		transform.LookAt (target);
		if(moveSpeed > 0.01f){
			animator.SetBool("running", true);
		}else{
			animator.SetBool("running", false);
		}
		navi.UpdateCurrentPosition();
	}
	
	virtual public void TargetHit(int index)
	{
		
	}
	
}
