namespace Hunbjter;

public sealed record LoginAutomationResult(bool UserFound, bool PasswordFound, bool SubmitFound, string Message);
