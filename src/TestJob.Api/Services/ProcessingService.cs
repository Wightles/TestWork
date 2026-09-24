using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using TestJob.Api.Models;

namespace TestJob.Api.Services;

public sealed class ProcessingService
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    // Контроллер вызывает сервис только после успешной FluentValidation.
    public async Task<ProcessResponse> ProcessAsync(
        ProcessRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = DecodeText(request.UrlB64!, "URL");
        var page = DecodeText(request.PageB64!, "PAGE");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ProcessingException("INVALID_URL", "URL должен быть абсолютным HTTP или HTTPS адресом.");
        }

        // Разбираем переданный HTML; URL и внешние ресурсы не загружаются.
        using var document = await new HtmlParser().ParseDocumentAsync(page, cancellationToken);
        IHtmlCollection<IElement> elements;
        try
        {
            elements = document.QuerySelectorAll(request.Selector!);
        }
        catch (DomException)
        {
            throw new ProcessingException("INVALID_SELECTOR", "Некорректный CSS-селектор.");
        }

        var attributes = new List<string>(elements.Length);
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attributes.Add(element.GetAttribute(request.Attribute!) ?? string.Empty);
        }

        var emails = new List<string>();
        try
        {
            foreach (Match match in EmailRegex.Matches(page))
            {
                cancellationToken.ThrowIfCancellationRequested();
                emails.Add(match.Value);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            throw new ProcessingException("EMAIL_SEARCH_TIMEOUT", "Превышено время поиска email-адресов.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string plainText;
        try
        {
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(request.KeyBytesB64!);
            var encrypted = Convert.FromBase64String(request.EncryptedTextBytesB64!);
            plainText = Utf8.GetString(aes.DecryptEcb(encrypted, PaddingMode.None));
        }
        catch (CryptographicException)
        {
            throw new ProcessingException("DECRYPTION_FAILED", "Не удалось расшифровать текст AES-256 ECB.");
        }
        catch (DecoderFallbackException)
        {
            throw new ProcessingException("INVALID_PLAIN_TEXT_UTF8", "Расшифрованные данные не являются текстом UTF-8.");
        }

        return new ProcessResponse
        {
            Url = url,
            ElementsCount = elements.Length,
            ElementsAttrList = attributes,
            EmailsCount = emails.Count,
            EmailsList = emails,
            DecryptedPlainText = plainText
        };
    }

    private static string DecodeText(string value, string field)
    {
        try
        {
            return Utf8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            throw new ProcessingException($"INVALID_{field}_BASE64", $"Поле {field} содержит некорректный Base64.");
        }
        catch (DecoderFallbackException)
        {
            throw new ProcessingException($"INVALID_{field}_UTF8", $"Поле {field} содержит некорректный UTF-8.");
        }
    }
}

public sealed class ProcessingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
