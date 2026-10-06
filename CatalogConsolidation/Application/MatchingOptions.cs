namespace CatalogConsolidation.Application;

/// <summary>Bound from configuration key "Matching" (see README's "Threshold calibration").</summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    public double SimilarityThreshold { get; set; } = 0.81;
}
