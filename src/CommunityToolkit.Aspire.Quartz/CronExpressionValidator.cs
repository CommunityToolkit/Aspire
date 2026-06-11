using System.Text.RegularExpressions;

namespace Aspire.Quartz;

internal static partial class CronExpressionValidator
{
    public static void Validate(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new ArgumentException(
                "Cron expression cannot be null or empty.",
                nameof(cronExpression));
        }

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 6 || parts.Length > 7)
        {
            throw new ArgumentException(
                $"Invalid cron expression: '{cronExpression}'. " +
                "Expected format: 'second minute hour day month weekday [year]'. " +
                "Example: '0 0 12 * * ?' (daily at noon).",
                nameof(cronExpression));
        }

        // Basic validation for each part
        ValidatePart(parts[0], "second", 0, 59);
        ValidatePart(parts[1], "minute", 0, 59);
        ValidatePart(parts[2], "hour", 0, 23);
        ValidatePart(parts[3], "day", 1, 31);
        ValidatePart(parts[4], "month", 1, 12);
        ValidatePart(parts[5], "weekday", 0, 7);

        if (parts.Length == 7)
        {
            ValidatePart(parts[6], "year", 1970, 2099);
        }
    }

    private static void ValidatePart(string part, string name, int min, int max)
    {
        // Allow wildcards and special characters
        if (part == "*" || part == "?" || part == "L" || part == "W")
        {
            return;
        }

        // Allow ranges (e.g., 1-5)
        if (part.Contains('-'))
        {
            var range = part.Split('-');
            if (range.Length == 2 &&
                int.TryParse(range[0], out var start) &&
                int.TryParse(range[1], out var end) &&
                start >= min && end <= max && start <= end)
            {
                return;
            }
        }

        // Allow lists (e.g., 1,2,3)
        if (part.Contains(','))
        {
            var values = part.Split(',');
            if (values.All(v => int.TryParse(v, out var val) && val >= min && val <= max))
            {
                return;
            }
        }

        // Allow steps (e.g., */5)
        if (part.Contains('/'))
        {
            var step = part.Split('/');
            if (step.Length == 2 &&
                (step[0] == "*" || int.TryParse(step[0], out _)) &&
                int.TryParse(step[1], out _))
            {
                return;
            }
        }

        // Allow single number
        if (int.TryParse(part, out var value) && value >= min && value <= max)
        {
            return;
        }

        throw new ArgumentException(
            $"Invalid cron expression part for {name}: '{part}'. " +
            $"Expected value between {min} and {max}, or wildcard/range/list/step.",
            nameof(part));
    }
}
