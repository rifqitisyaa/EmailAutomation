namespace EmailAutomation.Services;

public interface IPdfCompressionService
{
    byte[] CompressPdf(byte[] pdfBytes);
}
