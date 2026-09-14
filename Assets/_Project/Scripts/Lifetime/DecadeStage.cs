namespace TenCandles.Lifetime
{
    public enum DecadeStage { Childhood = 0, Youth = 1, Adulthood = 2, OldAge = 3 }

    public enum BirthdayChoice { Stay, Advance }

    public static class DecadeStageNames
    {
        public static string Display(this DecadeStage stage) => stage switch
        {
            DecadeStage.Childhood => "Childhood",
            DecadeStage.Youth => "Youth",
            DecadeStage.Adulthood => "Adulthood",
            _ => "Old Age"
        };
    }
}
