/*  ランダムに動くプレイヤー
	経路探索を使用せずに移動します
	shoot を true にすると、移動と移動の合間に爆弾を投げます
*/

using UnityEngine;

public class RandomWalk : Pawn
{
	public bool shoot = false;
	
	int step = 0;
	Vector3 targetPosition; // 移動目的地
	float timer;    // 目的地再設定タイマー
	
	// Start is called before the first frame update
	void Start()
	{
		SetGroupNo(0);
	}

	// Update is called once per frame
	void Update()
	{
		int n;
		Vector3 dv;
		float[] distances = new float[4];
		// 4方向の壁までの距離をチェック
		for(int i=0; i<cardinalDirectionNum; i++){
			distances[i] = DistanceToWall(cardinalDirections[i]);
		}
		switch(step){
			case 0:
				// 方向を選ぶ
				n = Random.Range(0,4);
				for(int i=0; i<4; i++){
					if(distances[n] > 1.0f) // 1ブロック以上は離れていること
						break;
					// 今見ている方向は壁が近いので別の方向にする
					n += 1;
					n = n&3; // 4以上になったら0に戻る
				}
				SetDirection(cardinalDirections[n]);
				
				// 移動距離を選ぶ
				float d = Mathf.FloorToInt(distances[n]); // 小数点切り捨て
				d = Random.Range(1,d+1);    // 今の位置から壁までの距離の間
				targetPosition = GetIntPosition() + GetDirection() * d;
				
				// 移動速度をセット（最大にする）
				SetMoveSpeedRatio(1f);
				timer = 10f;    // 一定時間経過したら目的地を再設定する
				step++;
			break;
			
			case 1:
				// 動く
				timer -= Time.deltaTime;
				// 目的地までの距離を確認
				dv = GetPosition() - targetPosition;
				dv.y = 0f;
				if(dv.magnitude<0.2f){
					// 0.2以下なら到着とみなす
					timer = 0f;
				}
				// 目的地につくか、一定時間経過したら
				if(timer <= 0f){
					// 壁までの距離が2以上で shoot の指定があったら
					if(shoot && DistanceToWall(GetDirection()) > 2f){
						ShootBomb();
					}
					step = 0;
				}
			break;
		}
	}
	
}
