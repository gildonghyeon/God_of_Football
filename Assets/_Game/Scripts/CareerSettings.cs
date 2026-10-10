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
                defending = initial, stamina = initial };
            switch (trait)
            {
                case Trait.Finisher: p.shooting = Mathf.Min(attributeCap, initial + traitBonus); break;
                case Trait.Playmaker: p.passing = Mathf.Min(attributeCap, initial + traitBonus); break;
                case Trait.BallWinner: p.defending = Mathf.Min(attributeCap, initial + traitBonus); break;
            }
            return p;
        }
    }
}
