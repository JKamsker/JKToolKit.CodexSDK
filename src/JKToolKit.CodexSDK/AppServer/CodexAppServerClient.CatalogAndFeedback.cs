namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Lists available models.
    /// </summary>
    public Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct = default) =>
        _configClient.ListModelsAsync(options, ct);

    /// <summary>
    /// Lists available models using default pagination options.
    /// </summary>
    public Task<ModelListResult> ListModelsAsync(CancellationToken ct = default) =>
        _configClient.ListModelsAsync(new ModelListOptions(), ct);

    /// <summary>
    /// Lists experimental features.
    /// </summary>
    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct = default) =>
        _configClient.ListExperimentalFeaturesAsync(options, ct);

    /// <summary>
    /// Lists experimental features using default pagination options.
    /// </summary>
    public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(CancellationToken ct = default) =>
        _configClient.ListExperimentalFeaturesAsync(new ExperimentalFeatureListOptions(), ct);

    /// <summary>
    /// Writes a single config value.
    /// </summary>
    public Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct = default) =>
        _configClient.WriteConfigValueAsync(options, ct);

    /// <summary>
    /// Applies multiple config edits atomically.
    /// </summary>
    public Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct = default) =>
        _configClient.WriteConfigBatchAsync(options, ct);

    /// <summary>
    /// Logs out the current account.
    /// </summary>
    public Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct = default) =>
        _configClient.LogoutAccountAsync(ct);

    /// <summary>
    /// Uploads a feedback report.
    /// </summary>
    public Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct = default) =>
        _configClient.UploadFeedbackAsync(options, ct);
}
