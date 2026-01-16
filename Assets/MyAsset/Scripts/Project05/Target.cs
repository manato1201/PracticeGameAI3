using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project05
{


    public class Target : MonoBehaviour
    {
        public int index = 0;
        GameManager_Project05 gameManager;

        void Start()
        {
            gameManager = GameManager_Project05.instance;
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
                other.gameObject.GetComponent<Pawn_Project05>().TargetHit(index);
                Delete();
            }
        }

    }
}
