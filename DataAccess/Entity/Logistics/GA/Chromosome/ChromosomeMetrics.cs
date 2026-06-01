public class ChromosomeMetrics
{
    public double TotalDistance { get; set; }
    public double TotalTimeHours { get; set; }
    public TimeSpan RouteStartTime { get; set; }
    public TimeSpan RouteEndTime { get; set; }
    public int LoadMoreMaxKg { get; set; } // загружаем сверх меры
    public double FuelCost { get; set; }
    public int ProductViolations { get; set; }      // выгружаем не загруженый товар
    public double MaxLoadKg { get; set; }        // Максимальный перегруз
    public double FinalLoadKg { get; set; }        // Остаток груза в конце (штраф если > 0)
    public double CapacityUtilizationPercent { get; set; } // % использования вместимости
    public int TimeWindowViolations { get; set; }  // Пропуски временных окон магазинов

    public ChromosomeMetrics Clone() => (ChromosomeMetrics)MemberwiseClone();

    public override string ToString() =>
        $"RouteMetrics[Dist={TotalDistance:F1}km, Time={TotalTimeHours:F1}h, " +
        $"Violations={TimeWindowViolations}, Fuel={FuelCost:F0}₽, Utilization={CapacityUtilizationPercent:F0}%]";
}