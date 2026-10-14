using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Controllers;
using PropertyManagement.Services;

namespace PropertyManagement.Tests;

public class InjuryTests
{
    [Fact]
    public void BodyParts_CoverBothSidesDigitsAndUnspecifiedInjuriesWithoutDuplicateIds()
    {
        var parts = InjuryCatalog.Parts;
        Assert.Equal(parts.Count, parts.Select(p => p.Id).Distinct().Count());
        Assert.Contains(parts, p => p.Id == "head");
        Assert.Contains(parts, p => p.Id == "upper-back" && p.Surface == "背面");
        Assert.Contains(parts, p => p.Id == "whole-body");
        Assert.Contains(parts, p => p.Id == "other");
        foreach (var side in new[] { "left", "right" })
        {
            foreach (var joint in new[] { "eye", "ear", "shoulder", "elbow", "wrist", "knee", "ankle" })
                Assert.Contains(parts, p => p.Id == $"{side}-{joint}");
            for (var digit = 1; digit <= 5; digit++)
            {
                Assert.Contains(parts, p => p.Id == $"{side}-finger-{digit}");
                Assert.Contains(parts, p => p.Id == $"{side}-toe-{digit}");
            }
        }
        Assert.All(parts, part => Assert.False(string.IsNullOrWhiteSpace(part.Name)));
    }

    [Fact]
    public void Index_ProvidesSelectableBodyPartsAndCorrectNavigationState()
    {
        var controller = new InjuriesController();
        var result = Assert.IsType<ViewResult>(controller.Index());
        Assert.Same(InjuryCatalog.Parts, result.Model);
        Assert.Equal("injuries", controller.ViewData["ActiveModule"]);
    }
}
