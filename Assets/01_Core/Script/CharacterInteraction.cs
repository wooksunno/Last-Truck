using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInteraction : MonoBehaviour
{
    [Header("Character Data")]
    [SerializeField] private CharacterStatsData statsData;

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                if (statsData != null && StatsUIManager.Instance != null)
                {
                    StatsUIManager.Instance.OpenUI(statsData);
                }
            }
        }
    }
}