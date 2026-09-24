using FluentValidation;

namespace TestJob.Api.Models;

public sealed class ProcessRequest
{
    public string? Selector { get; init; }
    public string? Attribute { get; init; }
    public string? UrlB64 { get; init; }
    public string? EncryptedTextBytesB64 { get; init; }
    public string? KeyBytesB64 { get; init; }
    public string? PageB64 { get; init; }
}

public sealed class ProcessRequestValidator : AbstractValidator<ProcessRequest>
{
    public ProcessRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Selector)
            .NotEmpty().WithErrorCode("EMPTY_SELECTOR")
            .WithMessage("Поле selector обязательно и не должно быть пустым.");
        RuleFor(request => request.Attribute)
            .NotEmpty().WithErrorCode("EMPTY_ATTRIBUTE")
            .WithMessage("Поле attribute обязательно и не должно быть пустым.");
        RuleFor(request => request.UrlB64)
            .NotEmpty().WithErrorCode("MISSING_URL").WithMessage("Поле url_b64 обязательно.")
            .Must(IsBase64).WithErrorCode("INVALID_URL_BASE64")
            .WithMessage("Поле url_b64 должно содержать корректный Base64.");
        RuleFor(request => request.PageB64)
            .NotEmpty().WithErrorCode("MISSING_PAGE").WithMessage("Поле page_b64 обязательно.")
            .Must(IsBase64).WithErrorCode("INVALID_PAGE_BASE64")
            .WithMessage("Поле page_b64 должно содержать корректный Base64.");
        RuleFor(request => request.KeyBytesB64)
            .NotEmpty().WithErrorCode("MISSING_KEY").WithMessage("Поле key_bytes_b64 обязательно.")
            .Must(IsBase64).WithErrorCode("INVALID_KEY_BASE64")
            .WithMessage("Поле key_bytes_b64 должно содержать корректный Base64.")
            .Must(value => Convert.FromBase64String(value!).Length == 32)
            .WithErrorCode("INVALID_KEY_LENGTH").WithMessage("Ключ AES-256 должен содержать 32 байта.");
        RuleFor(request => request.EncryptedTextBytesB64)
            .NotEmpty().WithErrorCode("MISSING_ENCRYPTED_TEXT")
            .WithMessage("Поле encrypted_text_bytes_b64 обязательно.")
            .Must(IsBase64).WithErrorCode("INVALID_ENCRYPTED_TEXT_BASE64")
            .WithMessage("Поле encrypted_text_bytes_b64 должно содержать корректный Base64.")
            .Must(value => Convert.FromBase64String(value!).Length % 16 == 0)
            .WithErrorCode("INVALID_ENCRYPTED_TEXT_LENGTH")
            .WithMessage("Длина шифротекста AES без padding должна быть кратна 16 байтам.");
    }

    private static bool IsBase64(string? value)
    {
        try
        {
            return value is not null && Convert.FromBase64String(value).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
