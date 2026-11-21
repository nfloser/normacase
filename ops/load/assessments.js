// Lasttest der Headless-Bewertung (k6). Prüft das Qualitätsziel p95 < 200 ms ohne Netzlatenz.
// Start: k6 run -e TOKEN=$(...) -e BASE_URL=http://localhost:4200 ops/load/assessments.js
import http from 'k6/http';
import { check } from 'k6';

const base = __ENV.BASE_URL || 'http://localhost:4200';
const caseInput = JSON.parse(open('../../legacy/examples/cases/demo-a-supported.json'));

export const options = {
  scenarios: {
    steady: { executor: 'constant-arrival-rate', rate: 40, timeUnit: '1s', duration: '30s', preAllocatedVUs: 20 },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{name:assess}': ['p(95)<200'],
  },
};

export default function () {
  const body = JSON.stringify({ knowledgeRelease: 'demo-a-2026.1', case: caseInput });
  const response = http.post(`${base}/api/v1/assessments`, body, {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${__ENV.TOKEN}` },
    tags: { name: 'assess' },
  });
  check(response, {
    'status 200': (r) => r.status === 200,
    'deterministisches Ergebnis': (r) => r.json('document.assessment.outcome') === 'SUPPORTED',
  });
}
