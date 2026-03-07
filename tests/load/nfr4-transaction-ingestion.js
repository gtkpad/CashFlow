import http from 'k6/http';
import { check } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';

// NFR-4: Transaction ingestion must sustain 50 req/s with p95 < 500ms
// Usage: k6 run tests/load/nfr4-transaction-ingestion.js
// Override base URL: k6 run -e BASE_URL=http://localhost:5000 tests/load/nfr4-transaction-ingestion.js

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5103';
const AUTH_URL = __ENV.AUTH_URL || `${BASE_URL}`;
const TEST_EMAIL = __ENV.TEST_EMAIL || `loadtest-${Date.now()}@test.com`;
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'LoadTest123!';

export const options = {
  scenarios: {
    transaction_ingestion: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 60,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const registerRes = http.post(
    `${AUTH_URL}/api/identity/register`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  const loginRes = http.post(
    `${AUTH_URL}/api/identity/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  check(loginRes, {
    'login succeeded': (r) => r.status === 200,
  });

  const body = JSON.parse(loginRes.body);
  return { token: body.accessToken };
}

let counter = 0;

export default function (data) {
  counter++;
  const today = new Date().toISOString().split('T')[0];
  const type = counter % 2 === 0 ? 1 : 2; // alternate Credit(1)/Debit(2)

  const payload = JSON.stringify({
    referenceDate: today,
    type: type,
    amount: (Math.random() * 1000 + 1).toFixed(2),
    currency: 'BRL',
    description: `k6 load test transaction ${counter}`,
  });

  const res = http.post(`${BASE_URL}/api/v1/transactions`, payload, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${data.token}`,
    },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
  });
}

export function handleSummary(data) {
  const p95 = data.metrics.http_req_duration.values['p(95)'];
  const failRate = data.metrics.http_req_failed.values.rate;
  const result = {
    nfr: 'NFR-4',
    description: 'Transaction ingestion sustains 50 req/s with p95 < 500ms',
    timestamp: new Date().toISOString(),
    thresholds: {
      p95_ms: { expected: 500, actual: Math.round(p95 * 100) / 100, pass: p95 < 500 },
      error_rate: { expected: 0.01, actual: Math.round(failRate * 10000) / 10000, pass: failRate < 0.01 },
    },
    summary: {
      total_requests: data.metrics.http_reqs.values.count,
      duration_s: 120,
      rps_target: 50,
      p50_ms: Math.round(data.metrics.http_req_duration.values['p(50)'] * 100) / 100,
      p90_ms: Math.round(data.metrics.http_req_duration.values['p(90)'] * 100) / 100,
      p95_ms: Math.round(p95 * 100) / 100,
      p99_ms: Math.round(data.metrics.http_req_duration.values['p(99)'] * 100) / 100,
    },
    overall_pass: p95 < 500 && failRate < 0.01,
  };
  return {
    'tests/load/results/nfr4-result.json': JSON.stringify(result, null, 2),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
