using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit.Sdk;
using Xunit;

namespace FileRetrieval.Integration.Tests.Api;

public class ConfigurationScheduledFtpE2ETests
{
    private const string FtpContainerName = "file-retrieval-ftp";
    private const string DefaultApiBaseUrl = "http://localhost:7090";
    private const string ClientId = "e2e-test-client";
    private const int PollIntervalSeconds = 5;
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Category", "E2E")]
    public async Task CreateConfiguration_ScheduledFtpCheck_FindsSeededFileWithinTwoMinutes()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("FILE_RETRIEVAL_API_BASE_URL") ?? DefaultApiBaseUrl;
        var jwtSecret = Environment.GetEnvironmentVariable("FILE_RETRIEVAL_JWT_SECRET")
            ?? "REPLACE_WITH_SECRET_KEY_AT_LEAST_32_CHARS_LONG_FOR_PRODUCTION";
        var jwtIssuer = Environment.GetEnvironmentVariable("FILE_RETRIEVAL_JWT_ISSUER")
            ?? "https://riskinsure.com";
        var jwtAudience = Environment.GetEnvironmentVariable("FILE_RETRIEVAL_JWT_AUDIENCE")
            ?? "file-retrieval-api";
        var ftpPasswordSecretName = Environment.GetEnvironmentVariable("FILE_RETRIEVAL_FTP_PASSWORD_SECRET_NAME")
            ?? "test-ftp-password";

        var sampleFilename = $"e2e-sample-{Guid.NewGuid():N}.txt";

        await EnsureFtpContainerRunningAsync();
        await SeedSampleFileAsync(sampleFilename);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var token = CreateJwtToken(jwtSecret, jwtIssuer, jwtAudience, ClientId);
        ValidateTokenShape(token);

        var createPayload = new
        {
            name = "E2E Scheduled FTP Check",
            description = "End-to-end validation for scheduled FTP discovery",
            protocol = "FTP",
            protocolSettings = new Dictionary<string, object>
            {
                ["Server"] = "file-retrieval-ftp",
                ["Port"] = 21,
                ["Username"] = "testuser",
                ["PasswordKeyVaultSecret"] = ftpPasswordSecretName,
                ["UseTls"] = false,
                ["UsePassiveMode"] = true,
                ["ConnectionTimeoutSeconds"] = 30
            },
            filePathPattern = "/",
            filenamePattern = sampleFilename,
            fileExtension = "txt",
            schedule = new
            {
                cronExpression = "*/5 * * * * *",
                timezone = "UTC",
                description = "Every 5 seconds"
            }
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/configuration")
        {
            Content = JsonContent.Create(createPayload)
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await httpClient.SendAsync(createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var authChallenge = string.Join(",", createResponse.Headers.WwwAuthenticate.Select(x => x.ToString()));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted,
            $"Body: {createContent}; WWW-Authenticate: {authChallenge}; TokenPrefix: {token[..Math.Min(30, token.Length)]}; TokenParts: {token.Split('.').Length}");

        using var createJson = JsonDocument.Parse(createContent);
        createJson.RootElement.TryGetProperty("id", out var idElement).Should().BeTrue("create response should include configuration id");
        var configurationId = idElement.GetGuid();

        var deadline = DateTimeOffset.UtcNow.Add(MaxWait);
        string? latestResponsePayload = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var historyResponse = await httpClient.GetAsync($"/api/v1/configuration/{configurationId}/executionhistory?pageSize=50");
            latestResponsePayload = await historyResponse.Content.ReadAsStringAsync();

            if (historyResponse.StatusCode == HttpStatusCode.OK)
            {
                using var historyJson = JsonDocument.Parse(latestResponsePayload);

                if (historyJson.RootElement.TryGetProperty("executions", out var executions) &&
                    executions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var execution in executions.EnumerateArray())
                    {
                        if (execution.TryGetProperty("filesFound", out var filesFound) && filesFound.GetInt32() > 0)
                        {
                            return;
                        }
                    }
                }
            }
            else if (historyResponse.StatusCode != HttpStatusCode.NotFound)
            {
                historyResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                    $"unexpected status when polling execution history. Response: {latestResponsePayload}");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds));
        }

        throw new XunitException(
            $"File was not found within {MaxWait.TotalSeconds} seconds for configuration {configurationId}. Last execution history payload: {latestResponsePayload}");
    }

    private static async Task EnsureFtpContainerRunningAsync()
    {
        var result = await RunProcessAsync(
            "docker",
            "inspect",
            "-f",
            "{{.State.Running}}",
            FtpContainerName);

        result.ExitCode.Should().Be(0,
            $"Unable to inspect FTP container '{FtpContainerName}'. stderr: {result.Stderr}");

        result.Stdout.Trim().Should().Be("true",
            $"FTP container '{FtpContainerName}' must be running before this E2E test.");
    }

    private static async Task SeedSampleFileAsync(string fileName)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(tempFile, $"seeded at {DateTimeOffset.UtcNow:O}");

        try
        {
            var ensureDirResult = await RunProcessAsync(
                "docker",
                "exec",
                FtpContainerName,
                "sh",
                "-c",
                "mkdir -p /home/vsftpd/testuser");

            ensureDirResult.ExitCode.Should().Be(0,
                $"Failed creating FTP test directory. stderr: {ensureDirResult.Stderr}");

            var copyToUserDir = await RunProcessAsync(
                "docker",
                "cp",
                tempFile,
                $"{FtpContainerName}:/home/vsftpd/testuser/{fileName}");

            copyToUserDir.ExitCode.Should().Be(0,
                $"Failed copying sample file to FTP user directory. stderr: {copyToUserDir.Stderr}");

            var copyToRootDir = await RunProcessAsync(
                "docker",
                "cp",
                tempFile,
                $"{FtpContainerName}:/home/vsftpd/{fileName}");

            copyToRootDir.ExitCode.Should().Be(0,
                $"Failed copying sample file to FTP root directory. stderr: {copyToRootDir.Stderr}");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static string CreateJwtToken(string secret, string issuer, string audience, string clientId)
    {
        var now = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "e2e-test-user"),
            new("client_id", clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(30).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ValidateTokenShape(string token)
    {
        token.Should().NotBeNullOrWhiteSpace("a JWT token must be generated before sending requests");

        var parts = token.Split('.');
        parts.Length.Should().Be(3, $"JWT should have 3 parts but had {parts.Length}");

        foreach (var part in parts)
        {
            part.Should().NotBeNullOrWhiteSpace("JWT segments must not be empty");
            DecodeBase64Url(part).Should().NotBeNull("JWT segment must be valid Base64Url");
        }
    }

    private static byte[] DecodeBase64Url(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        var padding = 4 - output.Length % 4;
        if (padding is > 0 and < 4)
        {
            output = output.PadRight(output.Length + padding, '=');
        }

        return Convert.FromBase64String(output);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }
}