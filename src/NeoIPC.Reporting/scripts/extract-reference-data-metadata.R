#!/usr/bin/env Rscript
#
# Extracts the metadata block from a serializeJSON-wrapped neoipcr
# reference dataset and writes it to a separate file as plain JSON.
#
# Invoked by the NeoIPC.Reporting service when an admin uploads a
# reference dataset, so the service can index it by the filter set
# that shaped it without reimplementing R's unserializeJSON in C#.
#
# Usage:
#   Rscript --vanilla extract-reference-data-metadata.R \
#     --in <serialized-dataset.json> \
#     --out <metadata.json>

# Only jsonlite is needed: the dataset is decoded with jsonlite::unserializeJSON
# and its metadata block re-emitted as plain JSON below. neoipcr is deliberately
# NOT loaded — a plain library(neoipcr) fails in the workspace/dev container
# (where neoipcr is load_all-ed, not installed), and jsonlite serialises the
# metadata on its own (the dataset_options' neoipcr_dhis2_dsopt class inherits
# "list", so it needs no bespoke asJSON method).
suppressPackageStartupMessages(library(jsonlite))

args <- commandArgs(trailingOnly = TRUE)
get_arg <- function(flag) {
  i <- match(flag, args)
  if (is.na(i) || i == length(args)) {
    stop("Missing value for ", flag, call. = FALSE)
  }
  args[[i + 1L]]
}

in_path <- get_arg("--in")
out_path <- get_arg("--out")

if (!file.exists(in_path)) {
  stop("Input file not found: ", in_path, call. = FALSE)
}

dataset <- tryCatch(
  jsonlite::unserializeJSON(readLines(in_path, warn = FALSE)),
  error = function(e) {
    stop("Input is not a serializeJSON-wrapped reference dataset: ",
      conditionMessage(e), call. = FALSE)
  }
)

if (!is.list(dataset) || is.null(dataset$metadata)) {
  stop("Input does not contain a 'metadata' field; not a neoipcr reference dataset.",
    call. = FALSE)
}

metadata <- dataset$metadata

# country_filter must stay a JSON array even for a single country: auto_unbox
# collapses a length-1 vector to a scalar string, which the .NET string[] binder
# rejects. I() marks it as-is so auto_unbox leaves it an array (length 0 and >1
# already serialise as arrays).
if (!is.null(metadata$dataset_options$country_filter)) {
  metadata$dataset_options$country_filter <-
    I(as.character(metadata$dataset_options$country_filter))
}

# calculated is a UTC instant; emit an explicit Z so .NET binds it unambiguously.
# jsonlite's POSIXt = "ISO8601" is offset-less, so .NET would otherwise reinterpret
# it in the container's local time zone.
if (!is.null(metadata$calculated)) {
  metadata$calculated <- format(metadata$calculated, "%Y-%m-%dT%H:%M:%SZ", tz = "UTC")
}

# No force: the dataset_options' neoipcr_dhis2_dsopt class inherits "list" (see
# the header note), so jsonlite serialises it as its underlying list without a
# bespoke asJSON method. The remaining options mirror the shape the .NET
# extractor expects: scalars unboxed, dates ISO 8601.
# A binary connection, because R translates LF to CRLF in text mode on Windows and passing a
# path to writeLines() opens it as file(path, "w") — i.e. "wt". The container runs Linux, where
# text mode emits LF anyway, but this script is also run directly on a developer's machine and
# the JSON it writes is bound by the .NET service; the bytes must not depend on where it ran.
# In binary mode sep is written literally, so stating it is what fixes the line endings.
out_con <- file(out_path, open = "wb")
on.exit(close(out_con), add = TRUE)
writeLines(
  jsonlite::toJSON(metadata, auto_unbox = TRUE,
    null = "null", na = "null", Date = "ISO8601", POSIXt = "ISO8601"),
  out_con, sep = "\n", useBytes = TRUE)
