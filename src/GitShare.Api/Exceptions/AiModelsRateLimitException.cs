namespace GitShare.Api.Exceptions;

public sealed class AiModelsRateLimitException()
    : Exception("GitHub Models API rate limit exceeded. Please wait and try again.");
