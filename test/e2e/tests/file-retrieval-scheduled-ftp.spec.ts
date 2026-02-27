import { test } from '@playwright/test';
import {
  createConfiguration,
  ensureContainerRunning,
  getFileRetrievalConfig,
  seedFileToFtpContainer,
  waitForFileFound,
} from '../helpers/file-retrieval-api';

const fileRetrievalConfig = getFileRetrievalConfig();

test.describe('File Retrieval Scheduled FTP E2E', () => {
  test('seeds ftp file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

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

    const created = await createConfiguration(request, fileRetrievalConfig, sampleFileName, clientId);
    console.log("Scheduled configuration created");

    await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
    console.log("File found");

});
});
