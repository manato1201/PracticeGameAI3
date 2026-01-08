using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{
	public int index = 0;
	GameManager gameManager;
	
	void Start()
	{
		gameManager = GameManager.instance;
	}

	// Update is called once per frame
	void Update()
	{
		
	}
	
	public void Delete()
	{
		gameManager.decTarget(index);
		Destroy(gameObject);
	}
	
	void OnTriggerEnter(Collider other)
	{
		if (other.gameObject.tag == "Player")
		{
			other.gameObject.GetComponent<Pawn>().TargetHit(index);
			Delete();
		}
	}
	
}
