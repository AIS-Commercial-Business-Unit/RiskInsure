import { test } from '@playwright/test';
import {
  createHttpsConfiguration,
  ensureContainerRunning,
  ensureHttpFileReachable,
  getFileRetrievalConfig,
  seedFileToHttpsTestData,
  waitForFileFound,
} from '../helpers/file-retrieval-api';

const fileRetrievalConfig = getFileRetrievalConfig();

test.describe('File Retrieval Scheduled HTTP E2E', () => {
  test('seeds http file, creates scheduled configuration, and observes file discovery', async ({ request }) => {
    test.setTimeout(180000);

    const clientId = `e2e-client-${Date.now()}`;
    const sampleFileName = `e2e-http-sample-${Date.now()}.txt`;

    console.log('Ensuring HTTPS container is running...');
    await ensureContainerRunning(fileRetrievalConfig.httpsContainerName);
    console.log('Seeding test file to HTTPS container...');
    await seedFileToHttpsTestData(sampleFileName, `sample payload ${new Date().toISOString()}`);
    console.log('Ensuring seeded HTTP file is reachable...');
    await ensureHttpFileReachable(fileRetrievalConfig.httpHostBaseUrl, sampleFileName);

    console.log('Creating scheduled file retrieval configuration...');
    const created = await createHttpsConfiguration(request, fileRetrievalConfig, sampleFileName, clientId);

    console.log('Waiting for file to be found...');
    await waitForFileFound(request, fileRetrievalConfig, created.id, clientId, 120000, 5000);
  });
});
