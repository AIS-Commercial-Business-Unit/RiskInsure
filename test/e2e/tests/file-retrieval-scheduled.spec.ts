import { test } from '@playwright/test';
import {
  getFileRetrievalConfig,
  ensureContainerRunning,

  createHttpsConfigurationInCosmos,
  createFtpConfigurationInCosmos,
  createAzureBlobConfigurationInCosmos,

  seedFileToHttpsContainer,
  seedFileToFtpContainer,
  seedFileToAzuriteBlob,

  waitForFileFound,
  waitForProcessedFileRecord,
} from '../helpers/file-retrieval-api';
import { logDetail, logInfo, logStep, logSuccess } from '../helpers/test-logger';


test.describe('File Retrieval Scheduled HTTP E2E', () => {
  test('seeds http file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileRetrievalConfig = getFileRetrievalConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-http-sample-${Date.now()}.txt`;

    logStep('HTTP', `Ensuring container ${fileRetrievalConfig.httpsContainerName} is running`);
    await ensureContainerRunning(fileRetrievalConfig.httpsContainerName);    
    logSuccess('HTTP', `Container ${fileRetrievalConfig.httpsContainerName} is running`);

    logStep('HTTP', `Seeding file to HTTPS container ${fileRetrievalConfig.httpsContainerName}`);
    await seedFileToHttpsContainer(sampleFileName, `sample payload ${new Date().toISOString()}`);
    logSuccess('HTTP', `Seeded file ${sampleFileName}`);

    logStep('HTTP', 'Creating scheduled configuration in Cosmos DB');
    const created = await createHttpsConfigurationInCosmos(request, fileRetrievalConfig, sampleFileName, clientId);
    logSuccess('HTTP', `Configuration created: ${created.id}`);

    logInfo('HTTP', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
    logSuccess('HTTP', `File discovered in execution: ${executionId}`);

    logInfo('HTTP', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileRetrievalConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('HTTP', 'Processed file record observed');
  });
});

test.describe('File Retrieval Scheduled FTP E2E', () => {
  test('seeds ftp file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileRetrievalConfig = getFileRetrievalConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-sample-${Date.now()}.txt`;

    logStep('FTP', `Ensuring container ${fileRetrievalConfig.ftpContainerName} is running`);
    await ensureContainerRunning(fileRetrievalConfig.ftpContainerName);
    logSuccess('FTP', `Container ${fileRetrievalConfig.ftpContainerName} is running`);

    logStep('FTP', `Seeding file to FTP container ${fileRetrievalConfig.ftpContainerName}`);
    await seedFileToFtpContainer(
      fileRetrievalConfig.ftpContainerName,
      sampleFileName,
      `sample payload ${new Date().toISOString()}`
    );
    logSuccess('FTP', `Seeded file ${sampleFileName}`);

    logStep('FTP', 'Creating scheduled configuration in Cosmos DB');
    const created = await createFtpConfigurationInCosmos(request, fileRetrievalConfig, sampleFileName, clientId);
    logSuccess('FTP', `Configuration created: ${created.id}`);

    logInfo('FTP', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
    logSuccess('FTP', `File discovered in execution: ${executionId}`);

    logInfo('FTP', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileRetrievalConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('FTP', 'Processed file record observed');
  });
});

test.describe('File Retrieval Scheduled Azure Blob E2E', () => {
  test('seeds blob file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileRetrievalConfig = getFileRetrievalConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-blob-sample-${Date.now()}.txt`;

    logStep('AZURE', `Ensuring container ${fileRetrievalConfig.azuriteContainerName} is running`);
    await ensureContainerRunning(fileRetrievalConfig.azuriteContainerName);
    logSuccess('AZURE', `Container ${fileRetrievalConfig.azuriteContainerName} is running`);

    logStep('AZURE', `Seeding file to Azure Blob container ${fileRetrievalConfig.azuriteContainerName}`);
    await seedFileToAzuriteBlob(
      fileRetrievalConfig,
      sampleFileName,
      `sample payload ${new Date().toISOString()}`
    );
    logSuccess('AZURE', `Seeded blob ${sampleFileName}`);

    logStep('AZURE', 'Creating scheduled configuration in Cosmos DB');
    const created = await createAzureBlobConfigurationInCosmos(request, fileRetrievalConfig, sampleFileName, clientId);
    logSuccess('AZURE', `Configuration created: ${created.id}`);

    logInfo('AZURE', 'Waiting for file discovery');
    const executionId = await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
    logSuccess('AZURE', `File discovered in execution: ${executionId}`);

    logInfo('AZURE', 'Waiting for processed file record');
    await waitForProcessedFileRecord(request, fileRetrievalConfig, created.id, clientId, sampleFileName, executionId, 120000, 5000);
    logSuccess('AZURE', 'Processed file record observed');
    logDetail('────────────────────────────────────────────────────────────');
  });
});
