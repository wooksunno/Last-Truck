using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStatsData", menuName = "Scriptable Objects/CharacterStatsData")]
public class CharacterStatsData : ScriptableObject
{
    [Header("Info")]
    public string characterName;
    public Sprite characterSprite;

    [Header("Stats Info")]
    public int dance = 100;
    public int sing = 100;
    public int visual = 100;
    public int humanity = 100;
    public int awareness = 100;
    public int fan = 0;
}
