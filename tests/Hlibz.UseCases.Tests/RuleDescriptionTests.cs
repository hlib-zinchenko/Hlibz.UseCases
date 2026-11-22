namespace Hlibz.UseCases.Tests;

public sealed class RuleDescriptionTests
{
    [Fact]
    public void AddText_WithSameSentenceTwice_KeepsOne()
    {
        RuleDescription rule = new();

        rule.AddText("Must be in capitals.");
        rule.AddText("Must be in capitals.");

        Assert.Equal(["Must be in capitals."], rule.Texts);
    }

    [Fact]
    public void RuleSummary_Describe_AddsItsSentence()
    {
        RuleDescription rule = new();

        new RuleSummaryAttribute("End must not be earlier than Start.").Describe(rule);

        Assert.Equal(["End must not be earlier than Start."], rule.Texts);
    }

    [Fact]
    public void DescribedRule_Describe_SetsKeywordAndText()
    {
        RuleDescription rule = new();

        new UpperCaseAttribute().Describe(rule);

        Assert.Equal("^[^a-z]*$", rule.Pattern);
        Assert.Equal([UpperCaseAttribute.Text], rule.Texts);
    }
}
