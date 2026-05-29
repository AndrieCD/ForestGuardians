using UnityEngine;

// Attach to a spinner Image to rotate it continuously.
// Cheap visual feedback that the game hasn't frozen.
public class Sc_SpinnerRotate : MonoBehaviour
{
    [SerializeField] private float _RotateSpeed = 200f;

    private void Update()
    {
        transform.Rotate(0f, 0f, -_RotateSpeed * Time.unscaledDeltaTime);
    }
}