using FluentValidation;
using RiskInsure.FileRetrieval.API.Models;
using RiskInsure.FileRetrieval.Application.Services;
using FileRetrieval.Contracts.DTOs;

namespace RiskInsure.FileRetrieval.API.Validators;

/// <summary>
/// Validator for CreateConfigurationRequest using FluentValidation.
/// </summary>
public class ConfigurationRequestValidator : AbstractValidator<CreateConfigurationRequest>
{
    private readonly TokenReplacementService _tokenService;

    public ConfigurationRequestValidator(TokenReplacementService tokenService)
    {
        _tokenService = tokenService;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.Protocol)
            .NotEmpty().WithMessage("Protocol is required")
            .Must(BeValidProtocol).WithMessage("Protocol must be one of: Ftp, Https, AzureBlob");

        RuleFor(x => x.ProtocolSettings)
            .NotNull().WithMessage("ProtocolSettings are required")
            .Must(x => x != null && x.Count > 0).WithMessage("ProtocolSettings must not be empty");

        RuleFor(x => x.FilePathPattern)
            .NotEmpty().WithMessage("FilePathPattern is required")
            .MaximumLength(500).WithMessage("FilePathPattern must not exceed 500 characters")
            .Must(NotContainTokensInServerPortion).WithMessage("FilePathPattern must not contain tokens in server/host portion");

        RuleFor(x => x.FilenamePattern)
            .NotEmpty().WithMessage("FilenamePattern is required")
            .MaximumLength(200).WithMessage("FilenamePattern must not exceed 200 characters");

        RuleFor(x => x.FileExtension)
            .MaximumLength(10).WithMessage("FileExtension must not exceed 10 characters")
            .Matches("^[a-zA-Z0-9]*$").When(x => !string.IsNullOrWhiteSpace(x.FileExtension))
            .WithMessage("FileExtension must be alphanumeric only");

        RuleFor(x => x.Schedule)
            .NotNull().WithMessage("Schedule is required")
            .SetValidator(new ScheduleDefinitionValidator());

        // Protocol-specific validation
        When(x => x.Protocol?.Equals("Ftp", StringComparison.OrdinalIgnoreCase) == true, () =>
        {
            RuleFor(x => x.ProtocolSettings)
                .Must(ContainRequiredFtpSettings)
                .WithMessage("FTP protocol requires: Server, Port, Username, PasswordKeyVaultSecret");
        });

        When(x => x.Protocol?.Equals("Https", StringComparison.OrdinalIgnoreCase) == true, () =>
        {
            RuleFor(x => x.ProtocolSettings)
                .Must(ContainRequiredHttpsSettings)
                .WithMessage("HTTPS protocol requires: BaseUrl");
        });

        When(x => x.Protocol?.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase) == true, () =>
        {
            RuleFor(x => x.ProtocolSettings)
                .Must(ContainRequiredAzureBlobSettings)
                .WithMessage("AzureBlob protocol requires: StorageAccountName, ContainerName");
        });
    }

    private bool BeValidProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)) return false;
        return protocol.Equals("Ftp", StringComparison.OrdinalIgnoreCase) ||
               protocol.Equals("Https", StringComparison.OrdinalIgnoreCase) ||
               protocol.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase);
    }

    private bool NotContainTokensInServerPortion(string? filePathPattern)
    {
        if (string.IsNullOrWhiteSpace(filePathPattern)) return true;

        // Simple heuristic: if pattern starts with //, tokens should not appear before the third /
        // This validates that server names don't contain tokens like {yyyy}
        if (filePathPattern.StartsWith("//"))
        {
            var thirdSlashIndex = filePathPattern.IndexOf('/', 2);
            if (thirdSlashIndex > 0)
            {
                var serverPortion = filePathPattern.Substring(0, thirdSlashIndex);
                return !serverPortion.Contains('{');
            }
        }

        return true; // For relative paths, tokens are allowed anywhere
    }

    private bool ContainRequiredFtpSettings(Dictionary<string, object>? settings)
    {
        if (settings == null) return false;
        return settings.ContainsKey("Server") &&
               settings.ContainsKey("Port") &&
               settings.ContainsKey("Username") &&
               settings.ContainsKey("PasswordKeyVaultSecret");
    }

    private bool ContainRequiredHttpsSettings(Dictionary<string, object>? settings)
    {
        if (settings == null) return false;
        return settings.ContainsKey("BaseUrl");
    }

    private bool ContainRequiredAzureBlobSettings(Dictionary<string, object>? settings)
    {
        if (settings == null) return false;
        return settings.ContainsKey("StorageAccountName") &&
               settings.ContainsKey("ContainerName");
    }
}

/// <summary>
/// Validator for ScheduleDefinitionDto.
/// </summary>
public class ScheduleDefinitionValidator : AbstractValidator<ScheduleDefinitionDto>
{
    public ScheduleDefinitionValidator()
    {
        RuleFor(x => x.CronExpression)
            .NotEmpty().WithMessage("CronExpression is required")
            .Must(BeValidCronExpression).WithMessage("CronExpression must be a valid cron format");

        RuleFor(x => x.Timezone)
            .Must(BeValidTimeZone).When(x => !string.IsNullOrWhiteSpace(x.Timezone))
            .WithMessage("Timezone must be a valid IANA time zone");
    }

    private bool BeValidCronExpression(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression)) return false;

        try
        {
            // Simple validation: cron should have 5 or 6 parts separated by spaces
            var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length is >= 5 and <= 6;
        }
        catch
        {
            return false;
        }
    }

    private bool BeValidTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone)) return true; // Allow null/empty

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
