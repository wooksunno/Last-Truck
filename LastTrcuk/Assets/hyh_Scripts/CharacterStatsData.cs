using UnityEngine;

[CreateAssetMenu(fileName = "Character Data", menuName = "CharacterStats/Data")]
public class CharacterStatsData : ScriptableObject
{
    // 기본
    public string playerName;
    public float maxHealth;
    public float attackPower;
    public float moveSpeed;

    // 유틸리티
    public float costReducation;    // 제작재료 감소
    public float harvestSpeed;      // 채집 속도
    public float repairBonus;       // 수리 내구도 증가
}
