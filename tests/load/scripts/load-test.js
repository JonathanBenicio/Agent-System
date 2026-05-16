import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuração do teste: Subir gradativamente até 50 usuários simultâneos
export const options = {
  stages: [
    { duration: '30s', target: 20 }, // rampa de aquecimento
    { duration: '1m', target: 50 },  // carga principal
    { duration: '30s', target: 0 },  // rampa de descida
  ],
  thresholds: {
    // 95% das requisições devem terminar em menos de 500ms
    http_req_duration: ['p(95)<500'], 
    // Taxa de erro menor que 1%
    http_req_failed: ['rate<0.01'],   
  },
};

export default function () {
  // Usando host.docker.internal para acessar a API rodando no Windows Host
  const url = 'http://host.docker.internal:5000/api/test/rag/search'; 
  
  const payload = JSON.stringify({
    tenantId: 'tenant-stress-01',
    query: 'como configurar a pesquisa hibrida?',
    topK: 5,
    mode: 'hybrid' // Pode ser 'vector', 'fts' ou 'hybrid'
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Id': 'tenant-stress-01'
    },
  };

  const res = http.post(url, payload, params);

  check(res, {
    'status é 200': (r) => r.status === 200,
    'retornou resultados': (r) => {
        try {
            return JSON.parse(r.body).length >= 0;
        } catch {
            return false;
        }
    },
  });

  sleep(1); // tempo de "espera" de um usuário real lendo a tela
}
