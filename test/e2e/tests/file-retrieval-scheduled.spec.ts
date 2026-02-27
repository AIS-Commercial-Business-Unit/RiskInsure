import { test } from '@playwright/test';
import {
  getFileRetrievalConfig,
  ensureContainerRunning,

  createHttpsConfigurationInCosmos,
  createFtpConfigurationInCosmos,

  seedFileToHttpsContainer,
  seedFileToFtpContainer,

  waitForFileFound,
} from '../helpers/file-retrieval-api';


test.describe('File Retrieval Scheduled HTTP E2E', () => {
  test('seeds http file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileRetrievalConfig = getFileRetrievalConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-http-sample-${Date.now()}.txt`;

    console.log('Ensuring HTTPS container is running...');
    await ensureContainerRunning(fileRetrievalConfig.httpsContainerName);
    console.log('Seeding test file to HTTPS container...');
    await seedFileToHttpsContainer(sampleFileName, `sample payload ${new Date().toISOString()}`);

    console.log('Creating scheduled file retrieval configuration...');
    const created = await createHttpsConfigurationInCosmos(request, fileRetrievalConfig, sampleFileName, clientId);

    console.log('Waiting for file to be found...');
    await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
  });
});

test.describe('File Retrieval Scheduled FTP E2E', () => {
  test('seeds ftp file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const fileRetrievalConfig = getFileRetrievalConfig();

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-sample-${Date.now()}.txt`;

    await ensureContainerRunning(fileRetrievalConfig.ftpContainerName);
    console.log("FTP container is running");
    console.log("Adding sample file to FTP container...");
    await seedFileToFtpContainer(
      fileRetrievalConfig.ftpContainerName,
      sampleFileName,
      `sample payload ${new Date().toISOString()}`
    );
    console.log("Sample file added to FTP container");

    const created = await createFtpConfigurationInCosmos(request, fileRetrievalConfig, sampleFileName, clientId);
    console.log("Scheduled configuration created");

    await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
    console.log("File found");
  });
});
