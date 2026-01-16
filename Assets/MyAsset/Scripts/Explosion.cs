using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
	float timer = 0.5f;
	BoxCollider box;
	
	// Start is called before the first frame update
	void Start()
	{
		box = GetComponent<BoxCollider>();
		Destroy(gameObject, 2.0f);	// 2秒後に自滅
	}

	// Update is called once per frame
	void Update()
	{
		timer -= Time.deltaTime;
		if(timer < 0){
			box.enabled = false;
		}
	}
	
	public void OnTriggerEnter (Collider other)
	{
		if (other.CompareTag ("Player")){
			other.gameObject.GetComponent<Pawn>().Dead();
		}
	}	
}

