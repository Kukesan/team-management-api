using Application.Features.Reports.Dtos;
using Application.Features.Reports.Validators;

namespace Api.Tests;

/// <summary>
/// Spec Sec3: "Request Changes by leaving one general comment explaining what needs
/// correction" -- the comment is mandatory precisely when RequestedChanges is chosen.
/// </summary>
public class ReviewRequestValidatorTests
{
    private readonly ReviewRequestValidator _validator = new();

    [Fact]
    public void RequestedChanges_WithoutComment_IsInvalid()
    {
        var result = _validator.Validate(new ReviewRequest { Action = "RequestedChanges", Comment = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ReviewRequest.Comment));
    }

    [Fact]
    public void RequestedChanges_WithComment_IsValid()
    {
        var result = _validator.Validate(new ReviewRequest { Action = "RequestedChanges", Comment = "Please revise the hours." });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Approved_WithoutComment_IsValid()
    {
        var result = _validator.Validate(new ReviewRequest { Action = "Approved", Comment = null });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("Rejected")]
    [InlineData("")]
    public void UnrecognizedAction_IsInvalid(string action)
    {
        var result = _validator.Validate(new ReviewRequest { Action = action, Comment = "irrelevant" });

        Assert.False(result.IsValid);
    }
}
