public enum FinalBossQTEInputType
{
    None = 0,
    Space = 1,
    E = 2,
    Q = 3,
    LeftMouse = 4,
    RightMouse = 5
}

public static class FinalBossQTEInputTypeUtility
{
    public static string ToDisplayLabel(FinalBossQTEInputType inputType)
    {
        switch (inputType)
        {
            case FinalBossQTEInputType.Space:
                return "Space";

            case FinalBossQTEInputType.E:
                return "E";

            case FinalBossQTEInputType.Q:
                return "Q";

            case FinalBossQTEInputType.LeftMouse:
                return "Mouse0";

            case FinalBossQTEInputType.RightMouse:
                return "Mouse1";

            default:
                return "None";
        }
    }
}
