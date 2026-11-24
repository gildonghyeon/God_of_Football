namespace MyFootballCareer
{
    // 화면에 보이는 이름을 한국어로 바꿉니다. (코드 안의 enum 이름은 영어 그대로 둡니다)
    public static class Ko
    {
        public static string Attr(AttributeKind kind)
        {
            switch (kind)
            {
                case AttributeKind.Shooting: return "슈팅";
                case AttributeKind.Passing: return "패스";
                case AttributeKind.Dribbling: return "드리블";
                case AttributeKind.Defending: return "수비";
                default: return "체력";
            }
        }

        public static string Pos(Position position)
        {
            switch (position)
            {
                case Position.Forward: return "공격수";
                case Position.Midfielder: return "미드필더";
                case Position.Goalkeeper: return "골키퍼";
                default: return "수비수";
            }
        }

        public static string TraitName(Trait trait)
        {
            switch (trait)
            {
                case Trait.Finisher: return "피니셔";
                case Trait.Playmaker: return "플레이메이커";
                case Trait.Dribbler: return "드리블러";
                case Trait.Pressing: return "압박형 공격수";
                case Trait.BoxToBox: return "박스 투 박스";
                case Trait.Stopper: return "스토퍼";
                case Trait.BallPlaying: return "빌드업 수비수";
                case Trait.Overlapping: return "공격형 풀백";
                case Trait.ShotStopper: return "샷 스토퍼";
                case Trait.SweeperKeeper: return "스위퍼 키퍼";
                default: return "볼 위너";
            }
        }

        // 포지션마다 고를 수 있는 특성
        public static Trait[] TraitsFor(Position p)
        {
            switch (p)
            {
                case Position.Forward: return new[] { Trait.Finisher, Trait.Dribbler, Trait.Pressing };
                case Position.Midfielder: return new[] { Trait.Playmaker, Trait.BoxToBox, Trait.BallWinner };
                case Position.Goalkeeper: return new[] { Trait.ShotStopper, Trait.SweeperKeeper };
                default: return new[] { Trait.Stopper, Trait.BallPlaying, Trait.Overlapping };
            }
        }

        // 특성이 처음에 올려 주는 능력치
        public static AttributeKind TraitAttribute(Trait t)
        {
            switch (t)
            {
                case Trait.Finisher: return AttributeKind.Shooting;
                case Trait.Dribbler: case Trait.Overlapping: return AttributeKind.Dribbling;
                case Trait.Playmaker: case Trait.BallPlaying: case Trait.SweeperKeeper: return AttributeKind.Passing;
                case Trait.Pressing: case Trait.BoxToBox: return AttributeKind.Stamina;
                default: return AttributeKind.Defending;   // 볼 위너, 스토퍼, 샷 스토퍼(골키퍼는 수비 = 선방)
            }
        }
    }
}
