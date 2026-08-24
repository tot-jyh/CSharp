namespace Hunbjter;

public interface ILoginAutomation
{
    Task<LoginAutomationResult> LoginAsync(LoginSettings settings, string password, CancellationToken cancellationToken);
}
