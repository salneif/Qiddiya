using UnityEngine;

public class A_FootSounds : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource footStepAudioSource = default;

    [Header("Long/Looping Walk Clips")]
    [SerializeField] private AudioClip woodLoopClip = default;
    [SerializeField] private AudioClip metalLoopClip = default;
    [SerializeField] private AudioClip grassLoopClip = default;
    [SerializeField] private AudioClip defaultLoopClip = default;

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private A_CrouchAndJump crouchAndJumpSystem;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (crouchAndJumpSystem == null)
            crouchAndJumpSystem = A_CrouchAndJump.instance;

        if (footStepAudioSource != null)
        {
            footStepAudioSource.loop = true; // Ensure the source is set to loop
        }
    }

    private void Update()
    {
        HandleContinuousFootsteps();
    }

    private void HandleContinuousFootsteps()
    {
        // Check if player is grounded and moving
        bool isGrounded = characterController != null && characterController.isGrounded;
        bool isMoving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;

        if (isGrounded && isMoving)
        {
            // Determine which surface we are on via Raycast
            AudioClip targetClip = defaultLoopClip;

            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 2f))
            {
                switch (hit.collider.tag)
                {
                    case "Wood":
                        if (woodLoopClip != null) targetClip = woodLoopClip;
                        break;
                    case "Metal":
                        if (metalLoopClip != null) targetClip = metalLoopClip;
                        break;
                    case "Grass":
                        if (grassLoopClip != null) targetClip = grassLoopClip;
                        break;
                }
            }

            // If the audio source isn't playing, or the surface clip changed, switch and play
            if (footStepAudioSource != null)
            {
                if (footStepAudioSource.clip != targetClip)
                {
                    footStepAudioSource.clip = targetClip;
                }

                if (!footStepAudioSource.isPlaying)
                {
                    footStepAudioSource.Play();
                }

                // Adjust pitch if crouching for a slower/lower sound effect
                if (crouchAndJumpSystem != null && crouchAndJumpSystem.isCrouching)
                {
                    footStepAudioSource.pitch = 0.8f;
                }
                else
                {
                    footStepAudioSource.pitch = 1.0f;
                }
            }
        }
        else
        {
            // Stop playing when standing still or in the air
            if (footStepAudioSource != null && footStepAudioSource.isPlaying)
            {
                footStepAudioSource.Stop();
            }
        }
    }
}