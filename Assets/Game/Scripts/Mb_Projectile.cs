using UnityEngine;

public class Mb_Projectile : MonoBehaviour
{
    Rigidbody rigidBody;

    float damageAmount;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>( );
        rigidBody.isKinematic = false;
        rigidBody.AddRelativeForce(Vector3.forward * 500f);
    }

    public void SetDamageAmount(float amount)
    {
        damageAmount = amount;
    }

    public float GetDamageAmount()
    {
        return damageAmount; 
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
        {
            if (collision.gameObject != GameObject.Find("Player"))
            {
                Destroy(this.gameObject);
            }
        }
    }
}
