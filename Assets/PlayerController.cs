using UnityEngine;

public class playercontroller : MonoBehaviour
{
    Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       animator = GetComponent<Animator>(); 
    }

    // Update is called once per frame
    void Update()
    {
        // Check if the "w" key is pressed
        if (Input.GetKeyDown("w"))
        {
            // Set the "IsRunning" parameter in the Animator to true
            animator.SetBool("IsRunning", true);
        }
    }
}
