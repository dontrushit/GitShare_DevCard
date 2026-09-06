namespace GitShare.Api.Exceptions;

public sealed class AiModelsRateLimitException()
    : Exception("AI model API rate limit exceeded. Please wait and try again.");
