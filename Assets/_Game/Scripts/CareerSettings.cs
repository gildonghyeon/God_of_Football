using UnityEngine;
namespace MyFootballCareer
{
    [CreateAssetMenu(menuName = "Football Career/Settings", fileName = "CareerSettings")]
    public class CareerSettings : ScriptableObject
    {
        [Min(1)] public float attributeCap = 100;
        [Min(0)] public float initialAttribute = 10;
        [Min(0)] public float traitBonus = 3;
        [Range(1, 30)] public int maxNameLength = 16;
        public PlayerData CreatePlayer(string name, Position position, Trait trait)
        {
            float initial = Mathf.Clamp(initialAttribute, 0, attributeCap);
            var p = new PlayerData { playerName = name, position = position, trait = trait,
                shooting = initial, passing = initial, dribbling = initial,
                defending = initial, stamina = initial, goalkeeping = initial };
            var kind = Ko.TraitAttribute(trait);   // 특성에 맞는 능력치에 보너스
            p.Set(kind, Mathf.Min(attributeCap, initial + traitBonus));
            return p;
        }
    }
}
