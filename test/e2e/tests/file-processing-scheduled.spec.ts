import { test } from '@playwright/test';
import {
  getFileProcessingConfig,
  ensureContainerRunning,

  createHttpsConfigurationInCosmos,
  createFtpConfigurationInCosmos,
  createAzureBlobConfigurationInCosmos,

  seedFileToHttpsContainer,
  seedFileToFtpContainer,
  seedFileToAzuriteBlob,

  waitForFileFound,
  waitForProcessedFileRecord,
} from '../helpers/file-processing-api';
import { generateNachaFileContent } from '../helpers/generate-sample-data';
import { logDetail, logInfo, logStep, logSuccess } from '../helpers/test-logger';


test.describe('File Processing Scheduled HTTP E2E', () => {
  test('seeds http file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileProcessingConfig = getFileProcessingConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-http-sample-${Date.now()}.ach`;

    logStep('HTTP', `Ensuring container ${fileProcessingConfig.httpsContainerName} is running`);
    await ensureContainerRunning(fileProcessingConfig.httpsContainerName);    
    logSuccess('HTTP', `Container ${fileProcessingConfig.httpsContainerName} is running`);

    logStep('HTTP', `Seeding file to HTTPS container ${fileProcessingConfig.httpsContainerName}`);
    await seedFileToHttpsContainer(sampleFileName, generateNachaFileContent());
    logSuccess('HTTP', `Seeded file ${sampleFileName}`);

    logStep('HTTP', 'Creating scheduled configuration in Cosmos DB');
    const created = await createHttpsConfigurationInCosmos(request, fileProcessingConfig, sampleFileName, clientId);
    logSuccess('HTTP', `Configuration created: ${created.id}`);

    logInfo('HTTP', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileProcessingConfig, created.id, clientId, 120000, 5000);
    logSuccess('HTTP', `File discovered in execution: ${executionId}`);

    logInfo('HTTP', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileProcessingConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('HTTP', 'Processed file record observed');
  });
});

test.describe('File Processing Scheduled FTP E2E', () => {
  test('seeds ftp file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileProcessingConfig = getFileProcessingConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-sample-${Date.now()}.ach`;

    logStep('FTP', `Ensuring container ${fileProcessingConfig.ftpContainerName} is running`);
    await ensureContainerRunning(fileProcessingConfig.ftpContainerName);
    logSuccess('FTP', `Container ${fileProcessingConfig.ftpContainerName} is running`);

    logStep('FTP', `Seeding file to FTP container ${fileProcessingConfig.ftpContainerName}`);
    await seedFileToFtpContainer(
      fileProcessingConfig.ftpContainerName,
      sampleFileName,
      generateNachaFileContent()
    );
    logSuccess('FTP', `Seeded file ${sampleFileName}`);

    logStep('FTP', 'Creating scheduled configuration in Cosmos DB');
    const created = await createFtpConfigurationInCosmos(request, fileProcessingConfig, sampleFileName, clientId);
    logSuccess('FTP', `Configuration created: ${created.id}`);

    logInfo('FTP', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileProcessingConfig, created.id, clientId, 120000, 5000);
    logSuccess('FTP', `File discovered in execution: ${executionId}`);

    logInfo('FTP', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileProcessingConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('FTP', 'Processed file record observed');
  });
});

test.describe('File Processing Scheduled Azure Blob E2E', () => {
  test('seeds blob file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileProcessingConfig = getFileProcessingConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-blob-sample-${Date.now()}.ach`;

    logStep('AZURE', `Ensuring container ${fileProcessingConfig.azuriteContainerName} is running`);
    await ensureContainerRunning(fileProcessingConfig.azuriteContainerName);
    logSuccess('AZURE', `Container ${fileProcessingConfig.azuriteContainerName} is running`);

    logStep('AZURE', `Seeding file to Azure Blob container ${fileProcessingConfig.azuriteContainerName}`);
    await seedFileToAzuriteBlob(
      fileProcessingConfig,
      sampleFileName,
      generateNachaFileContent()
    );
    logSuccess('AZURE', `Seeded blob ${sampleFileName}`);

    logStep('AZURE', 'Creating scheduled configuration in Cosmos DB');
    const created = await createAzureBlobConfigurationInCosmos(request, fileProcessingConfig, sampleFileName, clientId);
    logSuccess('AZURE', `Configuration created: ${created.id}`);

    logInfo('AZURE', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileProcessingConfig, created.id, clientId, 120000, 5000);
    logSuccess('AZURE', `File discovered in execution: ${executionId}`);

    logInfo('AZURE', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileProcessingConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('AZURE', 'Processed file record observed');
    logDetail('────────────────────────────────────────────────────────────');
  });
});
