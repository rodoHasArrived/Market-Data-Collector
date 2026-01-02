using Prometheus;

namespace DataIngestion.ValidationService.Services;

public sealed class ValidationMetrics
{
    private readonly Counter _validationsTotal = Metrics.CreateCounter("validation_total", "Total validations", new CounterConfiguration { LabelNames = ["result"] });
    private readonly Counter _issuesTotal = Metrics.CreateCounter("validation_issues_total", "Total issues by code", new CounterConfiguration { LabelNames = ["code", "severity"] });

    private long _totalValidations;
    private long _validCount;
    private long _invalidCount;

    public long ValidationsPerformed => _totalValidations;
    public long ValidRecords => _validCount;
    public long InvalidRecords => _invalidCount;
    public double ValidityRate => _totalValidations > 0 ? (double)_validCount / _totalValidations : 1.0;

    public void RecordValidation(bool isValid)
    {
        Interlocked.Increment(ref _totalValidations);
        if (isValid)
        {
            Interlocked.Increment(ref _validCount);
            _validationsTotal.WithLabels("valid").Inc();
        }
        else
        {
            Interlocked.Increment(ref _invalidCount);
            _validationsTotal.WithLabels("invalid").Inc();
        }
    }

    public void RecordIssue(string code, string severity)
    {
        _issuesTotal.WithLabels(code, severity).Inc();
    }
}
