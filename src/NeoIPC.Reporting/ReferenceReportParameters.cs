using System.Collections.Immutable;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace NeoIPC.Reporting;

readonly struct ReferenceReportParameters
{
    public ReferenceReportParameters(
        DateOnly? reportingPeriodFrom,
        DateOnly? reportingPeriodTo,
        ushort? birthWeightFrom,
        ushort? birthWeightTo,
        ushort? gestationalAgeFrom,
        ushort? gestationalAgeTo,
        string[] countryFilter,
        string[] hospitalFilter,
        bool? testUnitFilter,
        bool? defaultPatientFilter,
        HttpRequest httpRequest)
    {
        ReportingPeriodFrom = reportingPeriodFrom;
        ReportingPeriodTo = reportingPeriodTo;
        BirthWeightFrom = birthWeightFrom;
        BirthWeightTo = birthWeightTo;
        GestationalAgeFrom = gestationalAgeFrom;
        GestationalAgeTo = gestationalAgeTo;
        CountryFilter = [..countryFilter];
        HospitalFilter = [..hospitalFilter];
        TestUnitFilter = testUnitFilter;
        DefaultPatientFilter = defaultPatientFilter;
        var headers = httpRequest.GetTypedHeaders();
        SessionId = headers.Cookie.FirstOrDefault(cookieHeaderValue => cookieHeaderValue is
                { Name: { HasValue: true, Value: "JSESSIONID" }, Value.HasValue: true })
            ?.Value.ToString() ?? throw new ArgumentException("JSESSIONID is missing.");

        AcceptHeaders = [..ReferenceReport.SortHeaders(headers.Accept)];
        AcceptLanguageHeaders = [..ReferenceReport.SortHeaders(headers.AcceptLanguage)];
    }

    public DateOnly? ReportingPeriodFrom { get; }
    public DateOnly? ReportingPeriodTo { get; }
    public ushort? BirthWeightFrom { get; }
    public ushort? BirthWeightTo { get; }
    public ushort? GestationalAgeFrom { get; }
    public ushort? GestationalAgeTo { get; }
    public bool? TestUnitFilter { get; }
    public bool? DefaultPatientFilter { get; }
    public string SessionId { get; }
    public ImmutableArray<string> CountryFilter { get; }
    public ImmutableArray<string> HospitalFilter { get; }
    public ImmutableArray<MediaTypeHeaderValue> AcceptHeaders { get; }
    public ImmutableArray<StringWithQualityHeaderValue> AcceptLanguageHeaders { get; }

    bool PrintMembers(StringBuilder builder)
    {
        const string nullString = "null";
        const string emptyArrayString = "[]";
        const string dateFormatString = "o";

        Span<char> buffer = stackalloc char[10];
        // Scalars
        builder.Append(nameof(ReportingPeriodFrom))
            .Append(" = ");
        if (ReportingPeriodFrom.HasValue)
        {
            ReportingPeriodFrom.Value.TryFormat(buffer, out var written, dateFormatString);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(ReportingPeriodTo))
            .Append(" = ");
        if (ReportingPeriodTo.HasValue)
        {
            ReportingPeriodTo.Value.TryFormat(buffer, out var written, dateFormatString);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(BirthWeightFrom))
            .Append(" = ");
        if (BirthWeightFrom.HasValue)
        {
            BirthWeightFrom.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(BirthWeightTo))
            .Append(" = ");
        if (BirthWeightTo.HasValue)
        {
            BirthWeightTo.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(GestationalAgeFrom))
            .Append(" = ");
        if (GestationalAgeFrom.HasValue)
        {
            GestationalAgeFrom.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(GestationalAgeTo))
            .Append(" = ");
        if (GestationalAgeTo.HasValue)
        {
            GestationalAgeTo.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(TestUnitFilter))
            .Append(" = ");
        if (TestUnitFilter.HasValue)
        {
            TestUnitFilter.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(DefaultPatientFilter))
            .Append(" = ");
        if (DefaultPatientFilter.HasValue)
        {
            DefaultPatientFilter.Value.TryFormat(buffer, out var written);
            builder.Append(buffer[..written]);
        }
        else builder.Append(nullString);

        builder.Append(nameof(CountryFilter))
            .Append(" = ");
        if (CountryFilter.IsDefaultOrEmpty)
            builder.Append(emptyArrayString);
        else
        {
            builder.Append('[');
            for (var i = 0; i < CountryFilter.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(CountryFilter[i]);
            }
            builder.Append(']');
        }

        builder.Append(nameof(HospitalFilter))
            .Append(" = ");
        if (HospitalFilter.IsDefaultOrEmpty)
            builder.Append(emptyArrayString);
        else
        {
            builder.Append('[');
            for (var i = 0; i < HospitalFilter.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(HospitalFilter[i]);
            }
            builder.Append(']');
        }

        builder.Append(nameof(AcceptHeaders))
            .Append(" = ");
        if (AcceptHeaders.IsDefaultOrEmpty)
            builder.Append(emptyArrayString);
        else
        {
            builder.Append('[');
            for (var i = 0; i < AcceptHeaders.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(AcceptHeaders[i]);
            }
            builder.Append(']');
        }

        builder.Append(nameof(AcceptLanguageHeaders))
            .Append(" = ");
        if (AcceptLanguageHeaders.IsDefaultOrEmpty)
            builder.Append(emptyArrayString);
        else
        {
            builder.Append('[');
            for (var i = 0; i < AcceptLanguageHeaders.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(AcceptLanguageHeaders[i]);
            }
            builder.Append(']');
        }

        return true;
    }

    public void Deconstruct(out DateOnly? ReportingPeriodFrom, out DateOnly? ReportingPeriodTo, out ushort? BirthWeightFrom, out ushort? BirthWeightTo, out ushort? GestationalAgeFrom, out ushort? GestationalAgeTo, out ImmutableArray<string> CountryFilter, out ImmutableArray<string> HospitalFilter, out bool? TestUnitFilter, out bool? DefaultPatientFilter, out ImmutableArray<MediaTypeHeaderValue> AcceptHeaders, out ImmutableArray<StringWithQualityHeaderValue> AcceptLanguageHeaders)
    {
        ReportingPeriodFrom = this.ReportingPeriodFrom;
        ReportingPeriodTo = this.ReportingPeriodTo;
        BirthWeightFrom = this.BirthWeightFrom;
        BirthWeightTo = this.BirthWeightTo;
        GestationalAgeFrom = this.GestationalAgeFrom;
        GestationalAgeTo = this.GestationalAgeTo;
        CountryFilter = this.CountryFilter;
        HospitalFilter = this.HospitalFilter;
        TestUnitFilter = this.TestUnitFilter;
        DefaultPatientFilter = this.DefaultPatientFilter;
        AcceptHeaders = this.AcceptHeaders;
        AcceptLanguageHeaders = this.AcceptLanguageHeaders;
    }
}