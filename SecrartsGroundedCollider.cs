using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecrartsGroundedCollider : MonoBehaviour
{

    public SecrartsCharacterControllerSetup player;


    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject != player.gameObject)
        {

            player.setGroundedValue(true);
            player.StopJumping();
            player.StopGravity();

        }else
        {

            player.setGroundedValue(false);

        }

    }

}
