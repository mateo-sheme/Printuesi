namespace Printuesi.Server.Helpers
{
    public static class LotNumberHelper
    {
        /// <summary>
        /// Generates lot number in format: L : XXVI/0405
        /// XXVI = last roman numeral part of year (e.g. 2026 → XXVI)
        /// 04   = week number within the month (1-5)
        /// 05   = month number (zero-padded)
        /// </summary>
        public static string Generate(DateTime productionDate)
        {
            var yearRoman = GetYearSuffix(productionDate.Year);
            var weekOfMonth = GetWeekOfMonth(productionDate);
            var month = productionDate.Month.ToString("D2");

            return $"L : {yearRoman}/{weekOfMonth:D2}{month}";
        }

        /// <summary>
        /// Returns only the last meaningful Roman numeral segment of a year.
        /// 2026 → MMXXVI → we return XXVI (everything after MM)
        /// This strips the leading MM (thousands) since it's constant for 2000s.
        /// </summary>
        private static string GetYearSuffix(int year)
        {
            var full = ToRoman(year);
            // Strip leading MM (2000) to keep it short as per business convention
            if (full.StartsWith("MM") && full.Length > 2)
                return full.Substring(2);
            return full;
        }

        /// <summary>
        /// Returns which week of the month the date falls in (1-5).
        /// Week 1 = days 1-7, Week 2 = days 8-14, etc.
        /// </summary>
        private static int GetWeekOfMonth(DateTime date)
        {
            return (int)Math.Ceiling(date.Day / 7.0);
        }

        private static string ToRoman(int number)
        {
            var values = new[]
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
                (100,  "C"), (90,  "XC"), (50,  "L"), (40,  "XL"),
                (10,   "X"), (9,   "IX"), (5,   "V"), (4,   "IV"),
                (1,    "I")
            };

            var result = "";
            foreach (var (val, numeral) in values)
            {
                while (number >= val)
                {
                    result += numeral;
                    number -= val;
                }
            }
            return result;
        }
    }
}