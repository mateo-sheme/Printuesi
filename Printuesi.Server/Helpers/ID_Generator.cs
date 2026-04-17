namespace Printuesi.Server.Helpers
{
    public class ID_Generator
    {
        private static readonly Random _random = new Random();

        public static string Generate(string prefix)
        {
            long number = (long)(_random.NextDouble() * 90000000) +10000000;
            return $"{prefix}{number}";
        }

        public static string Client() => Generate("C");
        public static string Template() => Generate("LT");
        public static string PrintJob() => Generate("PJ");
        public static string PrintJobObject() => Generate("PJO");
        public static string PrintEvent() => Generate("PE");
        public static string Supply() => Generate("S");
        public static string SupplyUsage() => Generate("SU");
        public static string User() => Generate("U");
        public static string Order() => Generate("ORD");
    }
}
