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
                default: return "수비수";
            }
        }

        public static string TraitName(Trait trait)
        {
            switch (trait)
            {
                case Trait.Finisher: return "피니셔";
                case Trait.Playmaker: return "플레이메이커";
                default: return "볼 위너";
            }
        }
    }
}
