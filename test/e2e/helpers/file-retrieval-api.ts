import { APIRequestContext, expect } from '@playwright/test';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { createHmac } from 'node:crypto';

const execFileAsync = promisify(execFile);

export interface FileRetrievalConfig {
  apiBaseUrl: string;
  jwtSecret: string;
  jwtIssuer: string;
  jwtAudience: string;
  bearerToken?: string;
  ftpContainerName: string;
  ftpPasswordSecretName: string;
  ftpHost: string;
  ftpPort: number;
  ftpUsername: string;
}

export interface CreatedConfiguration {
  id: string;
}

export function getFileRetrievalConfig(): FileRetrievalConfig {
  return {
    apiBaseUrl: process.env.FILE_RETRIEVAL_API_BASE_URL || 'http://127.0.0.1:7090',
    jwtSecret: process.env.FILE_RETRIEVAL_JWT_SECRET || 'REPLACE_WITH_SECRET_KEY_AT_LEAST_32_CHARS_LONG_FOR_PRODUCTION',
    jwtIssuer: process.env.FILE_RETRIEVAL_JWT_ISSUER || 'https://riskinsure.com',
    jwtAudience: process.env.FILE_RETRIEVAL_JWT_AUDIENCE || 'file-retrieval-api',
    bearerToken: process.env.FILE_RETRIEVAL_BEARER_TOKEN,
    ftpContainerName: process.env.FILE_RETRIEVAL_FTP_CONTAINER || 'file-retrieval-ftp',
    ftpPasswordSecretName:
      process.env.FILE_RETRIEVAL_FTP_PASSWORD_SECRET_NAME || 'testpass',
    ftpHost: process.env.FILE_RETRIEVAL_FTP_HOST || 'file-retrieval-ftp',
    ftpPort: parseInt(process.env.FILE_RETRIEVAL_FTP_PORT || '21', 10),
    ftpUsername: process.env.FILE_RETRIEVAL_FTP_USERNAME || 'testuser',
  };
}

export async function ensureFtpContainerRunning(containerName: string): Promise<void> {
  const { stdout } = await execFileAsync('docker', ['inspect', '-f', '{{.State.Running}}', containerName]);
  expect(stdout.trim()).toBe('true');
}

export async function seedFileToFtpContainer(
  containerName: string,
  fileName: string,
  fileContent: string
): Promise<void> {
  const tempFilePath = `${process.cwd()}\\${fileName}`;

  await import('node:fs/promises').then((fs) => fs.writeFile(tempFilePath, fileContent, 'utf8'));

  try {
    await execFileAsync('docker', [
      'exec',
      containerName,
      'sh',
      '-c',
      'mkdir -p /home/vsftpd/testuser',
    ]);

    await execFileAsync('docker', [
      'cp',
      tempFilePath,
      `${containerName}:/home/vsftpd/testuser/${fileName}`,
    ]);

    await execFileAsync('docker', ['cp', tempFilePath, `${containerName}:/home/vsftpd/${fileName}`]);
  } finally {
    await import('node:fs/promises').then((fs) => fs.rm(tempFilePath, { force: true }));
  }
}

export async function createConfiguration(
  request: APIRequestContext,
  config: FileRetrievalConfig,
  fileName: string,
  clientId: string
): Promise<CreatedConfiguration> {
  const token = config.bearerToken || createJwtToken(config.jwtSecret, config.jwtIssuer, config.jwtAudience, clientId);

  const response = await request.post(`${config.apiBaseUrl}/api/v1/configuration`, {
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    data: {
      name: 'Playwright E2E Scheduled FTP Check',
      description: 'Playwright migration for scheduled FTP retrieval E2E',
      protocol: 'FTP',
      protocolSettings: {
        Server: config.ftpHost,
        Port: config.ftpPort,
        Username: config.ftpUsername,
        PasswordKeyVaultSecret: config.ftpPasswordSecretName,
        UseTls: false,
        UsePassiveMode: true,
        ConnectionTimeoutSeconds: 30,
      },
      filePathPattern: '/',
      filenamePattern: fileName,
      fileExtension: 'txt',
      schedule: {
        cronExpression: '*/5 * * * * *',
        timezone: 'UTC',
        description: 'Every 5 seconds',
      },
      eventsToPublish: [
        {
          eventType: 'FileDiscovered',
          eventData: {},
        },
      ],
      commandsToSend: [],
    },
  });

  const bodyText = await response.text();
  expect(response.status(), `CreateConfiguration failed. Body: ${bodyText}`).toBe(202);

  const parsed = JSON.parse(bodyText) as CreatedConfiguration;
  expect(parsed.id).toBeTruthy();

  return parsed;
}

export async function waitForFileFound(
  request: APIRequestContext,
  config: FileRetrievalConfig,
  configurationId: string,
  clientId: string,
  timeoutMs = 120000,
  pollIntervalMs = 5000
): Promise<void> {
  const token = config.bearerToken || createJwtToken(config.jwtSecret, config.jwtIssuer, config.jwtAudience, clientId);

  await waitForConfigurationAvailable(
    request,
    config,
    configurationId,
    token,
    Math.min(timeoutMs, 60000),
    pollIntervalMs
  );

  const deadline = Date.now() + timeoutMs;
  let lastBody = '';

  console.log(`Waiting for file to be found for configuration ${configurationId}...`);
  while (Date.now() < deadline) {
    const response = await request.get(
      `${config.apiBaseUrl}/api/v1/configuration/${configurationId}/executionhistory?pageSize=50`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: 'application/json',
        },
      }
    );

    lastBody = await response.text();

    if (response.status() === 200) {
      const payload = JSON.parse(lastBody) as {
        executions?: Array<{ filesFound?: number }>;
      };

      const found = (payload.executions || []).some((e) => (e.filesFound || 0) > 0);
      if (found) {
        return;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(
    `File was not found within ${timeoutMs}ms for configuration ${configurationId}. Last payload: ${lastBody}`
  );
}

async function waitForConfigurationAvailable(
  request: APIRequestContext,
  config: FileRetrievalConfig,
  configurationId: string,
  token: string,
  timeoutMs: number,
  pollIntervalMs: number
): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let lastBody = '';

  while (Date.now() < deadline) {
    const response = await request.get(`${config.apiBaseUrl}/api/v1/configuration/${configurationId}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
      },
    });

    lastBody = await response.text();

    if (response.status() === 200) {
      return;
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(
    `Configuration ${configurationId} was not materialized within ${timeoutMs}ms. Last payload: ${lastBody}`
  );
}

function createJwtToken(secret: string, issuer: string, audience: string, clientId: string): string {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: 'HS256', typ: 'JWT' };
  const payload = {
    sub: 'e2e-test-user',
    client_id: clientId,
    iss: issuer,
    aud: audience,
    iat: now,
    nbf: now,
    exp: now + 30 * 60,
    jti: `${clientId}-${now}`,
  };

  const encodedHeader = toBase64Url(Buffer.from(JSON.stringify(header), 'utf8'));
  const encodedPayload = toBase64Url(Buffer.from(JSON.stringify(payload), 'utf8'));
  const signingInput = `${encodedHeader}.${encodedPayload}`;
  const signature = createHmac('sha256', secret).update(signingInput).digest();
  const encodedSignature = toBase64Url(signature);

  return `${signingInput}.${encodedSignature}`;
}

function toBase64Url(buffer: Buffer): string {
  return buffer
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}
