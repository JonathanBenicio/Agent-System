import http from 'k6/http'
import { check, group } from 'k6'
import { Rate } from 'k6/metrics'

export const options = {
  scenarios: {
    dashboard_load: {
      executor: 'constant-arrival-rate',
      rate: 20,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 10,
      maxVUs: 50,
    },
    api_stress: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '1m', target: 20 },
        { duration: '2m', target: 20 },
        { duration: '1m', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<2000'],
    http_req_failed: ['rate<0.05'],
  },
}

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000'
const errorRate = new Rate('api_errors')

export default function () {
  group('Dashboard API', () => {
    const r = http.get(`${BASE_URL}/api/admin/gateway/dashboard`)
    check(r, {
      'dashboard status 200': (r) => r.status === 200,
      'dashboard response < 1s': (r) => r.timings.duration < 1000,
    }) || errorRate.add(1)
  })

  group('Services API', () => {
    const r = http.get(`${BASE_URL}/api/admin/gateway/services`)
    check(r, {
      'services status 200': (r) => r.status === 200,
      'services is array': (r) => Array.isArray(r.json()),
    }) || errorRate.add(1)
  })

  group('Health API', () => {
    const r = http.get(`${BASE_URL}/api/admin/gateway/health`)
    check(r, {
      'health status 200': (r) => r.status === 200,
    }) || errorRate.add(1)
  })

  group('Agents API', () => {
    const r = http.get(`${BASE_URL}/api/agent/agents`)
    check(r, {
      'agents status 200': (r) => r.status === 200,
      'agents response < 500ms': (r) => r.timings.duration < 500,
    }) || errorRate.add(1)
  })
}
