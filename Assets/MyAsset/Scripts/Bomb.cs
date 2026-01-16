using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
	public Vector3 direction;
	float speed = 3.5f;
	Rigidbody rb;
	Explosion explosionPrefab;
	bool exploded = false;
	Vector3 moveSpeed = Vector3.zero;
	public int level = 1;

	public class Direction {
		public static Vector3 N = new Vector3(0f,0f,1f);
		public static Vector3 E = new Vector3(1f,0f,0f);
		public static Vector3 S = new Vector3(0f,0f,-1f);
		public static Vector3 W = new Vector3(-1f,0f,0f);
	}
	protected Vector3[] cardinalDirections = {
		Direction.N,
		Direction.E,
		Direction.S,
		Direction.W,
	};
	public const int cardinalDirectionNum = 4;
	
	// Start is called before the first frame update
	void Start()
	{
		explosionPrefab = Resources.Load<Explosion>("Explosion");
		rb = GetComponent<Rigidbody>();
		direction.y = 0f;
		// 方向を強制的に４方向にする
		int n = 0;
		float max_d = 0f;
		for(int i=0; i<cardinalDirectionNum; i++){
			float cosd = Vector3.Dot(direction, cardinalDirections[i]);
			if(cosd > max_d){
				n = i;
				max_d = cosd;
			}
		}
		direction = cardinalDirections[n];

		Vector3 d = direction * speed;
		moveSpeed = d;
		rb.linearVelocity = moveSpeed;
	}

	// Update is called once per frame
	void Update()
	{
		// なんらかの原因で止まったら爆発
		if(rb.linearVelocity.magnitude < 0.5f){
			Explode();
		}
	}
	
	void FixedUpdate()
	{
		//rb.MovePosition(rb.position + moveSpeed*Time.deltaTime);
		if(!exploded){
			rb.linearVelocity = moveSpeed;
		}else{
			rb.linearVelocity = Vector3.zero;
		}
	}
	
	void Explode()
	{
		if(exploded){
			return;
		}
		exploded = true;
		Vector3 explodePosition = transform.position;
		explodePosition.x = Mathf.RoundToInt(explodePosition.x);
		explodePosition.z = Mathf.RoundToInt(explodePosition.z);

		Instantiate( explosionPrefab, explodePosition, Quaternion.identity);

		GetComponent<MeshRenderer>().enabled = false;
		StartCoroutine( Blast(explodePosition, Vector3.forward));
		StartCoroutine( Blast(explodePosition, Vector3.right));
		StartCoroutine( Blast(explodePosition, Vector3.left));
		StartCoroutine( Blast(explodePosition, Vector3.back));

		GetComponent<SphereCollider>().enabled = false;
		
		AudioSource audioSource = GetComponent<AudioSource>();
		audioSource.PlayOneShot(audioSource.clip, 0.4f);
		
		Destroy(gameObject, 1.2f);  // コルーチン終了まで待つ必要あり
	}

	IEnumerator Blast(Vector3 base_pos, Vector3 dir_)
	{
		for (int i=1; i<1+level; i++){
			RaycastHit hit;
			LayerMask layer = ~LayerMask.NameToLayer("Wall");
			Physics.Raycast( base_pos + new Vector3(0f,0.5f,0), dir_, out hit, i, layer);
			
			if(!hit.collider){
				Instantiate(explosionPrefab, base_pos + (i*dir_), Quaternion.identity);
			}else{
				// 障害物により爆風は広がらない
				break;
			}
			
			yield return new WaitForSeconds(0.1f);
		}
	}
	
	
	void OnCollisionEnter(Collision collision)
	{
		GameObject other = collision.gameObject;
		// プレイヤーとぶつかったら爆発
		if (other.CompareTag ("Player")){
			other.gameObject.GetComponent<Pawn>().Dead();
			Explode();
			return;
		}
		
		// 他の爆弾や爆風とぶつかったら爆発
		if (other.CompareTag ("Bomb") || other.CompareTag ("Explosion")){
			Explode();
			return;
		}
		
		// 壁に正面からぶつかったら爆発
		foreach(ContactPoint p in collision.contacts){
			Vector3 point = p.point;
			Vector3 diff = point - transform.position;
			float d = Vector3.Dot(moveSpeed, diff);
			if(d > 0.95f){
				rb.linearVelocity = Vector3.zero;
				Explode();
				break;
			}
		}
	}
	
}
