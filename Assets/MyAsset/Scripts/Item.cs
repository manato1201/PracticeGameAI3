using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
	public bool taken = false;
	// Start is called before the first frame update
	void Start()
	{
		
	}

	// Update is called once per frame
	void Update()
	{
		
	}
	
	public void OnTriggerEnter (Collider other)
	{
		if (other.gameObject.CompareTag ("Player")){
			taken = true;
			GetComponent<SphereCollider>().enabled = false;
			other.gameObject.GetComponent<Pawn>().BombLevelUp(this.gameObject);
			AudioSource audioSource = GetComponent<AudioSource>();
			audioSource.PlayOneShot(audioSource.clip, 1.0f);
		}
	}	
	
}
