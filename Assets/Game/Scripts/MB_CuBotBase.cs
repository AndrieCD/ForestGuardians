using UnityEngine;

public class MB_CuBotBase : MonoBehaviour
{
    // TEST VALUES (prototyping only)
    float MaxHealth = 500f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        Mb_Projectile projectile = collision.gameObject.GetComponent<Mb_Projectile>( );
        if (projectile != null)
        {
            projectile.GetDamageAmount( );
            float damage = projectile.GetDamageAmount( );
            MaxHealth -= damage;
            Debug.Log("CuBot hit! Current Health: " + MaxHealth);
            if (MaxHealth <= 0)
            {
                Destroy(gameObject);
                Debug.Log("CuBot destroyed!");
            }
        }
    }
}
