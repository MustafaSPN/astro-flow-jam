namespace AstroFlowJam
{
    /// <summary>Sık güncellenen sayı metinleri için hazır dizgiler: her atışta yeni dizgi ayrılmaz.</summary>
    public static class NumberText
    {
        const int Count = 1024;
        static readonly string[] Cache = new string[Count];

        public static string Of(int value)
        {
            if (value < 0 || value >= Count) return value.ToString();
            return Cache[value] ??= value.ToString();
        }
    }
}
