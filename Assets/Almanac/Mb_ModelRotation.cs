using UnityEngine;

public class Mb_ModelRotation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Rotate the model around the Y-axis at a constant speed
        transform.Rotate(0f, 20f * Time.deltaTime, 0f);
    }
}
