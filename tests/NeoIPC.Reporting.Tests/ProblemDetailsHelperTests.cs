using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NeoIPC.Reporting;
using NUnit.Framework;

namespace NeoIPC.Reporting.Tests;

/// <summary>
/// Unit coverage for the <see cref="ProblemDetailsHelper"/> factory: every
/// <c>problem+json</c> response must carry the stable <c>code</c> extension
/// member (the contract the app maps to a localized message) alongside the
/// right status, title, and detail.
/// </summary>
[TestFixture]
[Category("Unit")]
public class ProblemDetailsHelperTests
{
    static ProblemHttpResult Problem(IResult result)
    {
        Assert.That(result, Is.InstanceOf<ProblemHttpResult>());
        return (ProblemHttpResult)result;
    }

    [Test]
    public void BadRequest_CarriesCodeStatusTitleDetail()
    {
        var problem = Problem(ProblemDetailsHelper.BadRequest(
            ProblemCodes.MissingUnitCodes, "Missing unitCodes", "detail text"));

        Assert.Multiple(() =>
        {
            Assert.That(problem.StatusCode, Is.EqualTo(400));
            Assert.That(problem.ProblemDetails.Title, Is.EqualTo("Missing unitCodes"));
            Assert.That(problem.ProblemDetails.Detail, Is.EqualTo("detail text"));
            Assert.That(problem.ProblemDetails.Extensions["code"],
                Is.EqualTo(ProblemCodes.MissingUnitCodes));
        });
    }

    [Test]
    public void NotFound_CarriesCodeAnd404()
    {
        var problem = Problem(ProblemDetailsHelper.NotFound(
            ProblemCodes.ReferenceDatasetNotFound, "Reference dataset not found", "d"));

        Assert.Multiple(() =>
        {
            Assert.That(problem.StatusCode, Is.EqualTo(404));
            Assert.That(problem.ProblemDetails.Extensions["code"],
                Is.EqualTo(ProblemCodes.ReferenceDatasetNotFound));
        });
    }

    [Test]
    public void Forbidden_CarriesCodeAnd403()
    {
        var problem = Problem(ProblemDetailsHelper.Forbidden(
            ProblemCodes.InsufficientAuthority, "Forbidden", "d"));

        Assert.Multiple(() =>
        {
            Assert.That(problem.StatusCode, Is.EqualTo(403));
            Assert.That(problem.ProblemDetails.Extensions["code"],
                Is.EqualTo(ProblemCodes.InsufficientAuthority));
        });
    }

    [Test]
    public void UnsupportedMediaType_CarriesCodeAnd415()
    {
        var problem = Problem(ProblemDetailsHelper.UnsupportedMediaType(
            ProblemCodes.UnsupportedMediaType, "only json"));

        Assert.Multiple(() =>
        {
            Assert.That(problem.StatusCode, Is.EqualTo(415));
            Assert.That(problem.ProblemDetails.Title, Is.EqualTo("Unsupported media type"));
            Assert.That(problem.ProblemDetails.Extensions["code"],
                Is.EqualTo(ProblemCodes.UnsupportedMediaType));
        });
    }
}
