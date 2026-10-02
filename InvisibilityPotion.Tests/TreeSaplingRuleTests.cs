using System.Collections.Generic;
using InvisibilityPotion.Plants;
using Xunit;

public class TreeSaplingRuleTests
{
    private static readonly HashSet<int> Eligible = new HashSet<int> { 11, 22, 33 };

    [Fact]
    public void No_candidates_means_no_tree()
    {
        var r = TreeSaplingRule.Choose(new List<TreeSaplingRule.Candidate>(), Eligible);
        Assert.Equal(TreeSaplingRule.Verdict.NoTree, r.Verdict);
        Assert.Equal(-1, r.Index);
    }

    [Fact]
    public void Ineligible_prefabs_and_out_of_radius_trees_are_ignored()
    {
        var c = new List<TreeSaplingRule.Candidate>
        {
            new TreeSaplingRule.Candidate(99, 0.1f, false),                                   // birch: not eligible
            new TreeSaplingRule.Candidate(11, TreeSaplingRule.TrunkRadius + 0.01f, false),   // too far
        };
        Assert.Equal(TreeSaplingRule.Verdict.NoTree, TreeSaplingRule.Choose(c, Eligible).Verdict);
    }

    [Fact]
    public void Nearest_free_eligible_tree_wins()
    {
        var c = new List<TreeSaplingRule.Candidate>
        {
            new TreeSaplingRule.Candidate(22, 0.9f, false),
            new TreeSaplingRule.Candidate(11, 0.3f, true),    // nearer but already carries lichen
            new TreeSaplingRule.Candidate(33, 0.5f, false),
            new TreeSaplingRule.Candidate(99, 0.0f, false),
        };
        var r = TreeSaplingRule.Choose(c, Eligible);
        Assert.Equal(TreeSaplingRule.Verdict.Ok, r.Verdict);
        Assert.Equal(2, r.Index);
    }

    [Fact]
    public void Only_taken_trees_in_range_report_taken()
    {
        var c = new List<TreeSaplingRule.Candidate> { new TreeSaplingRule.Candidate(11, 0.2f, true) };
        var r = TreeSaplingRule.Choose(c, Eligible);
        Assert.Equal(TreeSaplingRule.Verdict.Taken, r.Verdict);
        Assert.Equal(0, r.Index);
    }

    [Fact]
    public void Radius_edge_is_inclusive_and_eligible_names_match_the_ruling()
    {
        var c = new List<TreeSaplingRule.Candidate> { new TreeSaplingRule.Candidate(11, TreeSaplingRule.TrunkRadius, false) };
        Assert.Equal(TreeSaplingRule.Verdict.Ok, TreeSaplingRule.Choose(c, Eligible).Verdict);
        Assert.Equal(new[] { "FirTree", "Pinetree_01", "FirTree_big" }, TreeSaplingRule.EligibleTrees);
        Assert.Equal(1.0f, TreeSaplingRule.TrunkRadius);
    }
}
