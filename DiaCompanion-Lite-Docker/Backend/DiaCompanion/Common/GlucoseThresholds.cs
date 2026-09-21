using DiaCompanion.Api.Common;

namespace DiaCompanion.Common
{
    public readonly record struct GlucoseTargetRange(decimal? Lower, decimal? Upper);

    public class GlucoseThresholds
    {
        public const byte Normal = 0;
        public const byte Type1 = 1;
        public const byte Type2 = 2;
        public const byte Pregnancy = 3;

        public static bool IsAbnormal(byte diabetesType, decimal value, MetricContext? context)
        {
            return diabetesType switch
            {
                Normal => IsNormalAbnormal(value, context),
                Type1 => IsType1Abnormal(value, context),
                Type2 => IsType2Abnormal(value, context),
                Pregnancy => IsPregnancyAbnormal(value, context),
                _ => throw new ArgumentOutOfRangeException(nameof(diabetesType), "Invalid diabetes type")
            };
        }

        /// <summary>
        /// Trả khoảng tham chiếu đúng với các ngưỡng đang dùng để đánh dấu bất thường.
        /// Dùng cho client vẽ vạch ngưỡng; không để frontend tự hard-code một bộ số khác.
        /// </summary>
        public static GlucoseTargetRange GetTargetRange(byte diabetesType, MetricContext context)
        {
            return (diabetesType, context) switch
            {
                (Normal, MetricContext.BeforeMeal) => new(null, 5.6m),
                (Normal, MetricContext.AfterMeal) => new(null, 7.8m),

                (Type1, MetricContext.BeforeMeal) => new(4.0m, 8.0m),
                (Type1, MetricContext.AfterMeal) => new(4.0m, 10.0m),

                (Type2, MetricContext.BeforeMeal) => new(4.4m, 7.2m),
                (Type2, MetricContext.AfterMeal) => new(null, 10.0m),

                (Pregnancy, MetricContext.BeforeMeal) => new(null, 5.3m),
                (Pregnancy, MetricContext.AfterMeal) => new(null, 6.7m),

                _ => throw new ArgumentOutOfRangeException(nameof(diabetesType), "Invalid diabetes type/context")
            };
        }

        private static bool IsNormalAbnormal(decimal value, MetricContext? context) =>
            context switch
            {
                MetricContext.BeforeMeal => value >= 5.6m,
                MetricContext.AfterMeal => value >= 7.8m,
                _ => false
            };

        private static bool IsType1Abnormal(decimal value, MetricContext? context) =>
            context switch
            {
                MetricContext.BeforeMeal => value < 4.0m || value > 8.0m,
                MetricContext.AfterMeal => value < 4.0m || value > 10m,
                _ => false
            };

        private static bool IsType2Abnormal(decimal value, MetricContext? context) =>
            context switch
            {
                MetricContext.BeforeMeal => value < 4.4m || value > 7.2m,
                MetricContext.AfterMeal => value >= 10.0m,
                _ => false
            };

        private static bool IsPregnancyAbnormal(decimal value, MetricContext? context) =>
            context switch
            {
                MetricContext.BeforeMeal => value >= 5.3m,
                MetricContext.AfterMeal => value >= 6.7m,
                _ => false
            };
    }
}
