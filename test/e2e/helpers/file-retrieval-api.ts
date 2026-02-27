import { APIRequestContext, expect } from '@playwright/test';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { createHmac } from 'node:crypto';
import { BlobServiceClient } from '@azure/storage-blob';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as fs from 'node:fs/promises';
import http from 'node:http';
import https from 'node:https';

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

  httpsContainerName: string;
  httpHostBaseUrl: string;
  httpsHostBaseUrl: string;
  httpContainerBaseUrl: string;
  httpsContainerBaseUrl: string;

  azuriteContainerName: string;
  azuriteStorageAccountName: string;
  azuriteBlobContainerName: string;
  azuriteHostConnectionString: string;
  azuriteContainerConnectionString: string;
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

    httpsContainerName: process.env.FILE_RETRIEVAL_HTTPS_CONTAINER || 'file-retrieval-https',
    // The Host URLs can be used by something running on the host system
    httpHostBaseUrl: process.env.FILE_RETRIEVAL_HTTP_BASE_URL || 'http://127.0.0.1:8080',
    httpsHostBaseUrl: process.env.FILE_RETRIEVAL_HTTPS_BASE_URL || 'https://127.0.0.1:8443',
    // The Container URLs can be used by something running in a container
    httpContainerBaseUrl: process.env.FILE_RETRIEVAL_HTTP_BASE_URL || 'http://file-retrieval-https:80',
    httpsContainerBaseUrl: process.env.FILE_RETRIEVAL_HTTPS_BASE_URL || 'https://file-retrieval-https:443',

    azuriteContainerName: process.env.FILE_RETRIEVAL_AZURITE_CONTAINER || 'file-retrieval-azurite',
    azuriteStorageAccountName: process.env.FILE_RETRIEVAL_AZURITE_STORAGE_ACCOUNT || 'devstoreaccount1',
    azuriteBlobContainerName: process.env.FILE_RETRIEVAL_AZURITE_BLOB_CONTAINER || 'e2e-files',
    azuriteHostConnectionString:
      process.env.FILE_RETRIEVAL_AZURITE_HOST_CONNECTION_STRING ||
      'DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;',
    azuriteContainerConnectionString:
      process.env.FILE_RETRIEVAL_AZURITE_CONTAINER_CONNECTION_STRING ||
      'DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://file-retrieval-azurite:10000/devstoreaccount1;',
  };
}

export async function ensureContainerRunning(containerName: string): Promise<void> {
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

export async function seedFileToHttpsContainer(fileName: string, fileContent: string): Promise<string> {
  const testDataDir = await resolveFileRetrievalTestDataDir();
  const filePath = path.join(testDataDir, fileName);
  await fs.writeFile(filePath, fileContent, 'utf8');
  return filePath;
}

export async function seedFileToAzuriteBlob(
  config: FileRetrievalConfig,
  blobName: string,
  fileContent: string
): Promise<void> {
  const blobServiceClient = BlobServiceClient.fromConnectionString(config.azuriteHostConnectionString);
  const containerClient = blobServiceClient.getContainerClient(config.azuriteBlobContainerName);
  await containerClient.createIfNotExists();

  const blobClient = containerClient.getBlockBlobClient(blobName);
  await blobClient.upload(fileContent, Buffer.byteLength(fileContent));
}

export async function createFtpConfigurationInCosmos(
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
  expect(response.status(), `createFtpConfigurationInCosmos failed. Body: ${bodyText}`).toBe(202);

  const parsed = JSON.parse(bodyText) as CreatedConfiguration;
  expect(parsed.id).toBeTruthy();

  return parsed;
}

export async function createHttpsConfigurationInCosmos(
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
      name: 'Playwright E2E Scheduled HTTPS Check',
      description: 'Playwright scheduled HTTPS retrieval E2E',
      protocol: 'Https',
      protocolSettings: {
        BaseUrl: config.httpsContainerBaseUrl,
        AuthenticationType: 'None',
        ConnectionTimeoutSeconds: 30,
        FollowRedirects: true,
        MaxRedirects: 3,
      },
      filePathPattern: fileName,
      filenamePattern: fileName,
      fileExtension: 'txt',
      schedule: {
        cronExpression: '*/5 * * * * *',
        timezone: 'UTC',
        description: 'Every 5 seconds',
      }
    },
  });

  const bodyText = await response.text();
  expect(response.status(), `createHttpsConfigurationInCosmos failed. Body: ${bodyText}`).toBe(202);

  const parsed = JSON.parse(bodyText) as CreatedConfiguration;
  expect(parsed.id).toBeTruthy();

  return parsed;
}

export async function createAzureBlobConfigurationInCosmos(
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
      name: 'Playwright E2E Scheduled Azure Blob Check',
      description: 'Playwright scheduled Azurite blob retrieval E2E',
      protocol: 'AzureBlob',
      protocolSettings: {
        StorageAccountName: config.azuriteStorageAccountName,
        ContainerName: config.azuriteBlobContainerName,
        AuthenticationType: 'ConnectionString',
        ConnectionStringKeyVaultSecret: config.azuriteContainerConnectionString,
      },
      filePathPattern: '/',
      filenamePattern: fileName,
      fileExtension: 'txt',
      schedule: {
        cronExpression: '*/5 * * * * *',
        timezone: 'UTC',
        description: 'Every 5 seconds',
      }
    },
  });

  const bodyText = await response.text();
  expect(response.status(), `createAzureBlobConfigurationInCosmos failed. Body: ${bodyText}`).toBe(202);

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
): Promise<string> {
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
        executions?: Array<{ id?: string; filesFound?: number }>;
      };

      const matchingExecution = (payload.executions || []).find((e) => (e.filesFound || 0) > 0 && !!e.id);
      if (matchingExecution?.id) {
        return matchingExecution.id;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(
    `File was not found within ${timeoutMs}ms for configuration ${configurationId}. Last payload: ${lastBody}`
  );
}

export async function waitForProcessedFileRecord(
  request: APIRequestContext,
  config: FileRetrievalConfig,
  configurationId: string,
  clientId: string,
  expectedFileName?: string,
  executionId?: string,
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

  console.log(`Waiting for persisted processed file record for configuration ${configurationId}...`);
  while (Date.now() < deadline) {
    const encodedFileName = expectedFileName ? encodeURIComponent(expectedFileName) : null;
    const encodedExecutionId = executionId ? encodeURIComponent(executionId) : null;
    const processedFilesUrl = encodedFileName
      ? `${config.apiBaseUrl}/api/v1/configuration/${configurationId}/executionhistory/processedfiles?pageSize=50&fileName=${encodedFileName}${encodedExecutionId ? `&executionId=${encodedExecutionId}` : ''}`
      : `${config.apiBaseUrl}/api/v1/configuration/${configurationId}/executionhistory/processedfiles?pageSize=50${encodedExecutionId ? `&executionId=${encodedExecutionId}` : ''}`;

    const response = await request.get(
      processedFilesUrl,
      {
        headers: {
          Authorization: `Bearer ${token}`,
          Accept: 'application/json',
        },
      }
    );

    lastBody = await response.text();

    if (response.status() === 200) {
      const payload = JSON.parse(lastBody) as Array<{
        fileName?: string;
        checksumAlgorithm?: string;
        checksumHex?: string;
      }>;

      const processedFileRecordObserved = (payload || []).some((record) => {
        const hasChecksum = !!record.checksumAlgorithm && !!record.checksumHex;
        const fileMatches = !expectedFileName || record.fileName === expectedFileName;
        return hasChecksum && fileMatches;
      });

      if (processedFileRecordObserved) {
        return;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
  }

  throw new Error(
    `Processed file record was not observed within ${timeoutMs}ms for configuration ${configurationId}. Last payload: ${lastBody}`
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

async function resolveFileRetrievalTestDataDir(): Promise<string> {
  const currentFilePath = fileURLToPath(import.meta.url);
  const startDir = path.dirname(currentFilePath);

  let currentDir = startDir;
  for (let i = 0; i < 8; i += 1) {
    const candidate = path.join(currentDir, 'platform', 'fileintegration', 'test-data');
    try {
      const stat = await fs.stat(candidate);
      if (stat.isDirectory()) {
        return candidate;
      }
    } catch {
      // continue searching parent directories
    }

    const parent = path.dirname(currentDir);
    if (parent === currentDir) {
      break;
    }

    currentDir = parent;
  }

  throw new Error('Could not locate platform/fileintegration/test-data directory from test/e2e/helpers.');
}

function combineUrl(baseUrl: string, childPath: string): string {
  const trimmedBase = baseUrl.replace(/\/+$/, '');
  const trimmedPath = childPath.replace(/^\/+/, '');
  return `${trimmedBase}/${trimmedPath}`;
}

function getUrlBody(urlString: string): Promise<string> {
  return new Promise((resolve, reject) => {
    const parsed = new URL(urlString);

    const requestModule = parsed.protocol === 'https:' ? https : http;
    const requestOptions = parsed.protocol === 'https:'
      ? { rejectUnauthorized: false }
      : undefined;

    const req = requestModule.get(urlString, requestOptions as never, (res) => {
      let responseBody = '';

      res.setEncoding('utf8');
      res.on('data', (chunk: string) => {
        responseBody += chunk;
      });

      res.on('end', () => {
        if (!res.statusCode || res.statusCode < 200 || res.statusCode >= 300) {
          reject(new Error(`Expected 2xx for ${urlString}, got ${res.statusCode}. Body: ${responseBody}`));
          return;
        }

        resolve(responseBody);
      });
    });

    req.on('error', reject);
  });
}
