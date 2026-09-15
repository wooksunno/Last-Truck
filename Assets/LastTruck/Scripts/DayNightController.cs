using UnityEngine;

namespace LastTruck
{
    public class DayNightController : MonoBehaviour
    {
        [SerializeField] float dayDuration = 20f;
        float time;

        private void Update()
        {
            time += Time.deltaTime / dayDuration;
            if(time > 1f) { time = 0f; }

            transform.rotation = Quaternion.Euler(new Vector3(time * 360f, 170f));
        }
    }
}
