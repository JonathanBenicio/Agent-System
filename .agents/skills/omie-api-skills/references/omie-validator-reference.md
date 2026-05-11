---
trigger: always_on
---

# Regras de Validação e Consumo - API Omie

Você é um agente validando e gerando código para a integração com a API da Omie. Siga estritamente estas diretrizes:

## 1. Rate Limits e Bloqueios (Obrigatório)
- **Bloqueio de 60s**: NUNCA faça duas requisições para o mesmo ID num intervalo menor que 60 segundos. Implemente cache local do lado da aplicação.
- **Limites Globais**: Máximo de 4 requisições simultâneas e 240 requisições por minuto (considerando IP + App Key + Método).
- **Tratamento HTTP 425**: Na 10ª requisição incorreta para a mesma rota, ocorrerá um bloqueio total de 30 minutos. Implemente mecanismos de retry, backoff e controle de consumo.

## 2. Paginação e Sincronização
- O limite de paginação da API é de 100 registros por página.
- Sempre prefira listagens em lote utilizando as datas de última consulta para trazer apenas registros incrementais.

## 3. Estrutura de Vendas (`PedidoVendaProduto`)
- **Cabeçalho**: O campo `codigo_cliente` é obrigatório. O campo `etapa` também é obrigatório e dita a coluna de faturamento, aceitando unicamente os valores "10", "20", "30", "40" ou "50".
- **Chaves de Integração**: Em inclusões e alterações, o `codigo_pedido_integracao` é de preenchimento obrigatório para servir como mapa de relacionamento entre a aplicação e o Omie.
- **Parcelas e Condições**: Se a forma de pagamento (`codigo_parcela`) for o valor customizado "999", é estritamente necessário preencher a tag `qtde_parcelas` juntamente com a estrutura `lista_parcelas`.

## 4. Boas Práticas Financeiras e de Faturamento
- É proibido incluir lançamentos financeiros diretos provenientes de serviços ou vendas. O código deve registrar um documento de origem (Pedido de Venda ou Ordem de Serviço) que, após faturado, gera o financeiro automaticamente, evitando duplicidade.
