using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace NeoIPC.Reporting.Resources;

/// <summary>
/// Minimal-API handlers for <c>/reference-data</c> (public listing) and
/// <c>/admin/reference-data</c> (full CRUD). The handlers themselves
/// don't enforce authorization — the <c>NeoIpcAdmin</c> policy on the
/// <c>/admin</c> route group does that.
/// </summary>
/// <remarks>
/// Uploads use the staged-commit lifecycle on
/// <see cref="FileStorage"/>: stream the body to a staging file → run
/// the metadata extractor against it → build the sidecar → commit
/// (atomic move into place). On any failure the staged file is
/// discarded.
/// </remarks>
public static class ReferenceDataEndpoints
{
    /// <summary>Public listing — abstracted metadata only, no admin-only fields.</summary>
    public static IResult List(ReferenceDataStorage storage)
    {
        var items = new List<PublicReferenceDataMetadata>();
        foreach (var id in storage.EnumerateIds())
        {
            var sidecar = ReadSidecar(storage, id);
            if (sidecar is not null)
                items.Add(PublicReferenceDataMetadata.From(id, sidecar));
        }
        return Results.Ok(items);
    }

    /// <summary>Admin listing — public fields plus size, content type, uploader id.</summary>
    public static IResult AdminList(ReferenceDataStorage storage)
    {
        var items = new List<AdminReferenceDataMetadata>();
        foreach (var id in storage.EnumerateIds())
        {
            var sidecar = ReadSidecar(storage, id);
            if (sidecar is not null)
                items.Add(AdminReferenceDataMetadata.From(id, sidecar));
        }
        return Results.Ok(items);
    }

    /// <summary>Admin download — streams the raw stored JSON.</summary>
    public static IResult AdminDownload(string id, ReferenceDataStorage storage)
    {
        if (!FileStorage.IsValidId(id))
            return ProblemDetailsHelper.BadRequest(
                ProblemCodes.InvalidId, "Invalid id", "The id must be 32 hex characters.");
        if (!storage.Exists(id))
            return ProblemDetailsHelper.NotFound(
                ProblemCodes.ResourceNotFound, "Not found",
                "The requested reference dataset does not exist.");
        var sidecar = ReadSidecar(storage, id);
        var contentType = sidecar?.ContentType ?? "application/json";
        return Results.File(storage.DataPath(id), contentType: contentType);
    }

    /// <summary>
    /// Admin upload — stages the body, runs the metadata extractor, rejects a
    /// byte-identical re-upload, builds the sidecar from the extracted filter
    /// set, and commits. Returns 415 when the Content-Type isn't
    /// <c>application/json</c>; 400 when the body fails extraction (likely not a
    /// valid reference dataset); 409 when its content is byte-for-byte identical
    /// to an already-stored dataset.
    /// </summary>
    public static async Task<IResult> AdminUpload(
        string? displayName,
        HttpRequest request,
        ReferenceDataStorage storage,
        ReferenceDataMetadataExtractor extractor,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        if (!IsJsonContentType(request.ContentType))
            return ProblemDetailsHelper.UnsupportedMediaType(
                ProblemCodes.UnsupportedMediaType,
                "Reference-data upload requires Content-Type: application/json.");

        var stagedPath = await storage.StageAsync(request.Body, ct);
        var committed = false;
        try
        {
            var extraction = await extractor.ExtractAsync(stagedPath, ct);
            if (!extraction.Success || extraction.Metadata is null)
                return ProblemDetailsHelper.BadRequest(
                    ProblemCodes.InvalidReferenceData,
                    "Invalid reference data",
                    extraction.ErrorMessage ?? "The uploaded file is not a valid reference dataset.");

            // Reject a byte-identical re-upload: hash the staged bytes and compare
            // against every stored dataset, so the same benchmark can't pile up
            // under different display names.
            var contentHash = await ComputeContentHashAsync(stagedPath, ct);
            foreach (var existingId in storage.EnumerateIds())
            {
                var existingPath = storage.DataPath(existingId);
                if (!File.Exists(existingPath)) continue;
                if (await ComputeContentHashAsync(existingPath, ct) != contentHash) continue;
                var existing = ReadSidecar(storage, existingId);
                return ProblemDetailsHelper.Conflict(
                    ProblemCodes.DuplicateReferenceData,
                    "Duplicate reference dataset",
                    $"An identical reference dataset already exists as " +
                    $"'{existing?.DisplayName ?? existingId}'.");
            }

            var id = FileStorage.GenerateId();
            var fileInfo = new FileInfo(stagedPath);
            var createdAt = DateTimeOffset.UtcNow;
            var sidecar = new ReferenceDataSidecar
            {
                DisplayName = displayName ?? DefaultDisplayName(createdAt),
                ContentType = "application/json",
                SizeBytes = fileInfo.Length,
                UploaderUserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = createdAt,
                ReportingPeriodFrom = extraction.Metadata.ReportingPeriodFrom,
                ReportingPeriodTo = extraction.Metadata.ReportingPeriodTo,
                BirthWeightFrom = extraction.Metadata.BirthWeightFrom,
                BirthWeightTo = extraction.Metadata.BirthWeightTo,
                GestationalAgeFrom = extraction.Metadata.GestationalAgeFrom,
                GestationalAgeTo = extraction.Metadata.GestationalAgeTo,
                Countries = extraction.Metadata.Countries,
                IncludeTestUnits = extraction.Metadata.IncludeTestUnits,
                IncludeNonCorePatients = extraction.Metadata.IncludeNonCorePatients,
            };

            var sidecarJson = JsonSerializer.Serialize(sidecar);
            await storage.CommitAsync(id, stagedPath, sidecarJson, ct);
            committed = true;
            return Results.Created($"/admin/reference-data/{id}",
                AdminReferenceDataMetadata.From(id, sidecar));
        }
        finally
        {
            // Any exit that did not commit — extraction 400, duplicate 409, or a
            // thrown exception — leaves the staged temp file behind; discard it so
            // invalid uploads don't accumulate staging-*.tmp files in the storage root.
            if (!committed) storage.Discard(stagedPath);
        }
    }

    /// <summary>Admin delete — removes both the data file and the sidecar.</summary>
    public static IResult AdminDelete(string id, ReferenceDataStorage storage)
    {
        if (!FileStorage.IsValidId(id))
            return ProblemDetailsHelper.BadRequest(
                ProblemCodes.InvalidId, "Invalid id", "The id must be 32 hex characters.");
        if (!storage.Exists(id))
            return ProblemDetailsHelper.NotFound(
                ProblemCodes.ResourceNotFound, "Not found",
                "The requested reference dataset does not exist.");
        storage.Delete(id);
        return Results.NoContent();
    }

    static ReferenceDataSidecar? ReadSidecar(ReferenceDataStorage storage, string id)
    {
        try
        {
            using var fs = File.OpenRead(storage.MetaPath(id));
            return JsonSerializer.Deserialize<ReferenceDataSidecar>(fs);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Uppercase-hex SHA-256 of a file's raw bytes, for duplicate detection.</summary>
    static async Task<string> ComputeContentHashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }

    static bool IsJsonContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType)
        && contentType.Split(';', 2)[0].Trim().Equals("application/json",
            StringComparison.OrdinalIgnoreCase);

    static string DefaultDisplayName(DateTimeOffset createdAt) =>
        $"Reference data {createdAt:yyyy-MM-dd HH:mm} UTC";
}
