namespace Printuesi.Server.Helpers
{
    public class ID_Generator
    {

        public static string Generate(string prefix)
        {
            // Use part of a Guid to guarantee uniqueness while keeping it short
            string unique = Guid.NewGuid().ToString("N")[..8].ToUpper();
            return $"{prefix}-{unique}";
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
