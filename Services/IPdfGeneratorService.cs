namespace EmailAutomation.Services;

public interface IPdfGeneratorService
{
    Task<byte[]> GeneratePdfAsync(string htmlContent);
}
