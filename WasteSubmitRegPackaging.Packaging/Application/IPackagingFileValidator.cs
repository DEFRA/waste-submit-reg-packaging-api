using WasteSubmitRegPackaging.Packaging.Domain;

namespace WasteSubmitRegPackaging.Packaging.Application;

/// <summary>
/// Validates a stored packaging file. This is the seam for validation: the workflow only needs the result, which is
/// recorded on the file row and decides the next step. Called by <c>ValidatePackagingFile</c> and nothing else.
/// </summary>
internal interface IPackagingFileValidator
{
    Task<PackagingFileCheck> CheckAsync(StoredFile file, CancellationToken cancellationToken);
}

/// <summary>
/// PLACEHOLDER: passes every file, with no errors or warnings, without reading it. It stands in until validation is
/// built, so a first submission can go from upload straight to submit. Replace this class; nothing else needs to move.
///
/// Not decided: whether each error and warning is stored. See "Errors and warnings" in docs/packaging_workflow.md.
/// </summary>
internal sealed class PlaceholderPackagingFileValidator : IPackagingFileValidator
{
    public Task<PackagingFileCheck> CheckAsync(StoredFile file, CancellationToken cancellationToken) =>
        Task.FromResult(PackagingFileCheck.Passed);
}
