import { defineConfig } from '@playwright/test';
import { createHash, randomBytes } from 'node:crypto';

const credential = process.env.NORMACASE_REVIEW_E2E_CREDENTIAL ?? randomBytes(32).toString('base64');
const otherCredential = process.env.NORMACASE_REVIEW_E2E_OTHER_CREDENTIAL ?? randomBytes(32).toString('base64');
const administratorCredential = process.env.NORMACASE_REVIEW_E2E_ADMINISTRATOR_CREDENTIAL ?? randomBytes(32).toString('base64');
const entitlementApproverCredential = process.env.NORMACASE_REVIEW_E2E_ENTITLEMENT_APPROVER_CREDENTIAL ?? randomBytes(32).toString('base64');
process.env.NORMACASE_REVIEW_E2E_CREDENTIAL = credential;
process.env.NORMACASE_REVIEW_E2E_OTHER_CREDENTIAL = otherCredential;
process.env.NORMACASE_REVIEW_E2E_ADMINISTRATOR_CREDENTIAL = administratorCredential;
process.env.NORMACASE_REVIEW_E2E_ENTITLEMENT_APPROVER_CREDENTIAL = entitlementApproverCredential;
const correctionCredential=process.env.NORMACASE_CORRECTION_E2E_CREDENTIAL??randomBytes(32).toString('base64');
process.env.NORMACASE_CORRECTION_E2E_CREDENTIAL=correctionCredential;
const correctionCaseId='synthetic-intake-'+createHash('sha256').update('synthetic-json:browser-correction-order').digest('hex');
const connection = process.env.NORMACASE_REVIEW_E2E_CONNECTION;
if (!credential || !connection) throw new Error('Reviewed workbench test configuration is required');

export default defineConfig({
  testDir: './e2e-reviewed',
  outputDir: 'test-results/review',
  workers: 1,
  use: { baseURL: 'http://localhost:5080', browserName: 'chromium' },
  webServer: {
    command: 'dotnet ../src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll',
    url: 'http://localhost:5080/api/packs',
    timeout: 30000,
    reuseExistingServer: false,
    env: {
      ...process.env,
      SyntheticReview__Enabled: 'true',
      SyntheticReview__PersistenceEnabled: 'true',
      SyntheticReview__Users__alice__Credential: credential,
      SyntheticReview__Users__alice__Actions__0: 'READ',
      SyntheticReview__Users__alice__Actions__1: 'ACCEPT',
      SyntheticReview__Users__alice__Actions__2: 'OVERRIDE',
      SyntheticReview__Users__alice__Actions__3: 'EXPORT',
      SyntheticReview__Users__alice__Actions__4: 'INTAKE',
      SyntheticReview__Users__alice__Actions__5: 'BATCH',
      SyntheticReview__Users__alice__CaseIds__0: 'demo-g-incomplete',
      SyntheticReview__Users__alice__CaseIds__1: 'demo-g-not-supported',
      SyntheticReview__Users__alice__CaseIds__2: 'demo-g-review',
      SyntheticReview__Users__alice__CaseIds__3: 'demo-g-supported',
      SyntheticReview__Users__alice__CaseIds__4: 'demo-g-batch-supported',
      SyntheticReview__Users__alice__CaseIds__5: 'demo-g-batch-not-supported',
      SyntheticReview__Users__corrector__Credential: correctionCredential,
      SyntheticReview__Users__corrector__Actions__0: 'READ',
      SyntheticReview__Users__corrector__Actions__1: 'INTAKE',
      SyntheticReview__Users__corrector__Actions__2: 'CORRECT',
      SyntheticReview__Users__corrector__Actions__3: 'ACCEPT',
      SyntheticReview__Users__corrector__Actions__4: 'EXPORT',
      SyntheticReview__Users__corrector__Actions__5: 'CLARIFY',
      SyntheticReview__Users__corrector__CaseIds__0: correctionCaseId,
      SyntheticReview__Users__bob__Credential: otherCredential,
      SyntheticReview__Users__bob__Actions__0: 'READ',
      SyntheticReview__Users__bob__CaseIds__0: 'demo-g-review',
      SyntheticReview__Administrator__Credential: administratorCredential,
      SyntheticReview__KnowledgeAdministration__PROPOSE: 'synthetic-local:user-alice',
      SyntheticReview__KnowledgeAdministration__REVIEW: 'synthetic-local:user-bob',
      SyntheticReview__KnowledgeAdministration__ACTIVATE: 'synthetic-local:user-bob',
      SyntheticReview__EntitlementApprover__Credential: entitlementApproverCredential,
      ConnectionStrings__SyntheticReview: connection
    }
  },
  reporter: 'list'
});
