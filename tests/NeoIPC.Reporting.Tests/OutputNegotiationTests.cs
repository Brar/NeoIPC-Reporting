using System.Collections.Immutable;
using Microsoft.Net.Http.Headers;
using NUnit.Framework;

namespace NeoIPC.Reporting.Tests;

[TestFixture]
[Category("Unit")]
public class OutputNegotiationTests
{
    static ImmutableArray<MediaTypeHeaderValue> Accept(params string[] mediaTypes)
        => [.. mediaTypes.Select(m => MediaTypeHeaderValue.Parse(m))];

    [Test]
    public void OnlyRenderedOutputsAreAcceptable_RenderedOnly_IsTrue()
    {
        // A locale is mandatory for these, so with none available the request
        // must be refused (406) up front.
        Assert.Multiple(() =>
        {
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("application/pdf")), Is.True);
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("text/html")), Is.True);
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("text/html", "application/pdf")), Is.True);
        });
    }

    [Test]
    public void OnlyRenderedOutputsAreAcceptable_DataOutputAcceptable_IsFalse()
    {
        Assert.Multiple(() =>
        {
            // Pure locale-independent data output — no locale needed.
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("application/json")), Is.False);
            // A rendered type is offered, but so is the locale-independent JSON:
            // the request is serviceable without a locale, so it is not rendered-only.
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("application/pdf", "application/json")), Is.False);
            // Wildcards accept the JSON data output too.
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("*/*")), Is.False);
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("application/*")), Is.False);
        });
    }

    [Test]
    public void OnlyRenderedOutputsAreAcceptable_NoSupportedOutput_IsFalse()
    {
        // An unsupported Accept type is neither a rendered nor a data output: not
        // a locale problem (that is a 415 media-type problem decided in producer
        // selection), so this must not report a rendered-only request.
        Assert.Multiple(() =>
        {
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable(Accept("application/xml")), Is.False);
            Assert.That(OutputNegotiation.OnlyRenderedOutputsAreAcceptable([]), Is.False);
        });
    }

    [Test]
    public void SortAccept_DropsZeroQualityEntries()
    {
        // RFC 9110 §12.4.2: q=0 means "not acceptable", so the type must be dropped —
        // otherwise it slips past the locale gate or is served as a fallback.
        var kept = OutputNegotiation.SortAccept(
        [
            MediaTypeHeaderValue.Parse("application/json;q=0"),
            MediaTypeHeaderValue.Parse("text/html"),
        ]).Select(h => h.MediaType.ToString()).ToList();
        Assert.That(kept, Is.EqualTo(new[] { "text/html" }));
    }

    [Test]
    public void SortAcceptLanguage_DropsZeroQualityEntries()
    {
        var kept = OutputNegotiation.SortAcceptLanguage(
        [
            StringWithQualityHeaderValue.Parse("de;q=0"),
            StringWithQualityHeaderValue.Parse("en"),
        ]).Select(h => h.Value.ToString()).ToList();
        Assert.That(kept, Is.EqualTo(new[] { "en" }));
    }
}
