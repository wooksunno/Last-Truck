using UnityEngine;

namespace LastTruck
{
    public class PlayerMove : MonoBehaviour
    {
        public float speed = 5f;
        public Transform cameraTransform;

        float hAxis;
        float vAxis;
        bool wDown;

        Vector3 moveVec;
        Animator anim;

        private void Start()
        {
            anim = GetComponentInChildren<Animator>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            hAxis = Input.GetAxisRaw("Horizontal");
            vAxis = Input.GetAxisRaw("Vertical");
            wDown = Input.GetButton("Walk");

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveVec = (camForward * vAxis + camRight * hAxis).normalized;
            transform.position += moveVec * speed * (wDown ? 0.3f : 1f) * Time.deltaTime;

            if (anim != null)
            {
                anim.SetBool("isRun", moveVec != Vector3.zero);
                anim.SetBool("isWalk", wDown);
            }

            if (moveVec != Vector3.zero)
            {
                transform.LookAt(transform.position + moveVec);
            }
        }
    }
}