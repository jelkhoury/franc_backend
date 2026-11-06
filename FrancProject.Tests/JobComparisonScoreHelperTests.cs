using FrancProject.Helpers;
using static FrancProject.Helpers.JobComparisonScoreHelper;

namespace FrancProject.Tests;

/// <summary>
/// The weighted scoring behind the job comparison scorecard: each criterion carries
/// a weight, either side can be marked not-applicable, and the two sides are scored
/// independently so excluding a criterion for one job does not distort the other.
/// </summary>
public class JobComparisonScoreHelperTests
{
    private static AnswerRow Row(
        int scoreA, int scoreB, int weight,
        bool naA = false, bool naB = false, string category = "HEAD") =>
        new(scoreA, scoreB, weight, naA, naB, category);

    [Fact]
    public void Equal_weights_average_the_scores()
    {
        var (a, b) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 4, scoreB: 2, weight: 1),
            Row(scoreA: 2, scoreB: 4, weight: 1),
        });

        Assert.Equal(3, a, precision: 6);
        Assert.Equal(3, b, precision: 6);
    }

    [Fact]
    public void A_heavier_criterion_pulls_the_score_towards_itself()
    {
        // (5*3 + 1*1) / 4 = 4
        var (a, _) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 5, scoreB: 0, weight: 3),
            Row(scoreA: 1, scoreB: 0, weight: 1),
        });

        Assert.Equal(4, a, precision: 6);
    }

    [Fact]
    public void A_not_applicable_criterion_is_excluded_from_that_side_only()
    {
        // A ignores the first row entirely and scores 2 on the second.
        // B counts both: (5*1 + 2*1) / 2 = 3.5
        var (a, b) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 5, scoreB: 5, weight: 1, naA: true),
            Row(scoreA: 2, scoreB: 2, weight: 1),
        });

        Assert.Equal(2, a, precision: 6);
        Assert.Equal(3.5, b, precision: 6);
    }

    [Fact]
    public void Excluding_a_criterion_does_not_penalise_the_side_that_excluded_it()
    {
        // Regression guard against dividing by the full weight instead of the
        // applicable weight, which would silently drag the score toward zero.
        var (a, _) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 4, scoreB: 0, weight: 1),
            Row(scoreA: 0, scoreB: 0, weight: 9, naA: true),
        });

        Assert.Equal(4, a, precision: 6);
    }

    [Fact]
    public void A_side_with_every_criterion_excluded_scores_zero_rather_than_dividing_by_zero()
    {
        var (a, b) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 5, scoreB: 5, weight: 2, naA: true),
        });

        Assert.Equal(0, a, precision: 6);
        Assert.Equal(5, b, precision: 6);
    }

    [Fact]
    public void Zero_weight_criteria_do_not_affect_the_result()
    {
        var (a, _) = ComputeWeightedScores(new[]
        {
            Row(scoreA: 4, scoreB: 0, weight: 2),
            Row(scoreA: 1, scoreB: 0, weight: 0),
        });

        Assert.Equal(4, a, precision: 6);
    }

    [Fact]
    public void An_empty_scorecard_scores_zero_on_both_sides()
    {
        var (a, b) = ComputeWeightedScores(Array.Empty<AnswerRow>());

        Assert.Equal(0, a, precision: 6);
        Assert.Equal(0, b, precision: 6);
    }

    [Theory]
    [InlineData(4.0, 2.0, "A")]
    [InlineData(2.0, 4.0, "B")]
    [InlineData(3.0, 3.0, "Tie")]
    [InlineData(3.0, 3.0005, "Tie")]   // inside the 0.001 tolerance
    [InlineData(3.0, 3.002, "B")]      // outside it
    public void The_winner_is_decided_within_a_small_tolerance(double a, double b, string expected)
    {
        Assert.Equal(expected, DetermineWinner(a, b));
    }

    [Fact]
    public void ComputeComparisonResult_scores_and_decides_in_one_pass()
    {
        var (a, b, winner) = ComputeComparisonResult(new[]
        {
            Row(scoreA: 5, scoreB: 1, weight: 1),
        });

        Assert.Equal(5, a, precision: 6);
        Assert.Equal(1, b, precision: 6);
        Assert.Equal("A", winner);
    }

    [Theory]
    [InlineData("A", "Backend role wins")]
    [InlineData("B", "Data role wins")]
    [InlineData("Tie", "Tie")]
    public void The_winner_label_names_the_job_that_won(string winner, string expected)
    {
        Assert.Equal(expected, FormatWinnerLabel(winner, "Backend role", "Data role"));
    }
}
