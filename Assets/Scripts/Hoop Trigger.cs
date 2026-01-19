using UnityEngine;

public class HoopTrigger : MonoBehaviour
{
    private void Awake()
    {
        // Make sure this object has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        // Set the tag
        gameObject.tag = "HoopTrigger";
    }

    private void OnTriggerEnter(Collider other)
    {
        Basketball ball = other.GetComponent<Basketball>();
        if (ball != null)
        {
            Debug.Log("Ball entered hoop trigger!");
        }
    }
}