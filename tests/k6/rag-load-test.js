import http from 'k6/http'
import { check, group } from 'k6'
import { Rate } from 'k6/metrics'

export const options = {
  stages: [
    { duration: '30s', target: 5 },  // Warm up
    { duration: '1m', target: 10 },  // Normal load
    { duration: '1m', target: 20 },  // High load
    { duration: '30s', target: 0 },  // Cool down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% of requests must complete below 2s
    http_req_failed: ['rate<0.05'],    // Less than 5% failures
  },
}

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5000'
const errorRate = new Rate('api_errors')

export default function () {
  group('RAG Search API', () => {
    const payload = JSON.stringify({
      query: 'sistema agentic',
      tenantId: 'tenant-stress-01',
      topK: 5,
      mode: 'hybrid'
    })

    const params = {
      headers: {
        'Content-Type': 'application/json',
      },
    }

    const r = http.post(`${BASE_URL}/api/test/rag/search`, payload, params)
    
    check(r, {
      'status is 200': (r) => r.status === 200,
      'response has results': (r) => {
        try {
          const body = JSON.parse(r.body)
          return Array.isArray(body) && body.length > 0
        } catch (e) {
          return false
        }
      },
      'response time < 1s': (r) => r.timings.duration < 1000,
    }) || errorRate.add(1)
  })
}
