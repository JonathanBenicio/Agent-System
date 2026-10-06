import { test, expect } from '@playwright/test'

const execution = {
  id: 'review-exec', workflowId: 'review', workflowName: 'Review', status: 3,
  startedAt: '2026-10-02T12:00:00Z',
  stepExecutions: [
    { stepId: 'approve-a', stepName: 'Financeiro', status: 3, output: {} },
    { stepId: 'approve-b', stepName: 'Operações', status: 3, output: {} },
  ],
}

test.beforeEach(async ({ page }) => {
  await page.route('**/hubs/**', route => route.abort())
  await page.route('**/api/workflow/executions/review-exec', route => route.fulfill({ json: execution }))
})

test('real store roundtrips all current string step types and model fields', async ({ page }) => {
  await page.goto('/workflow-review-harness.html')
  await expect(page.getByRole('button', { name: 'Aprovar etapa' })).toBeVisible()
  const types = ['action', 'agent', 'decision', 'parallel', 'wait', 'approval', 'subworkflow']
  const definition = {
    id: 'roundtrip', name: 'All types', description: 'preserve', version: 7,
    promptTemplate: 'Hello {{price}}', variables: { price: 99 }, triggerType: 1,
    cronExpression: '0 0 * * *', createdAt: '2026-10-02T12:00:00Z',
    steps: types.map((stepType, i) => ({
      id: `s${i}`, name: stepType, stepType, agentName: 'agent', toolName: 'tool',
      actionDescription: 'work', input: { nested: { key: 1 } }, output: { result: 2 },
      dependsOn: i ? [`s${i - 1}`] : [], conditionExpression: '{{ready}}',
      parallelSteps: [], maxRetries: 3, errorStrategy: 2, timeout: '00:00:30',
      modelOverride: 'small', allowedToolsOverride: ['tool'],
      compensationStep: { id: `undo${i}`, name: 'undo', stepType: 'action', dependsOn: [], input: {}, output: {}, parallelSteps: [], maxRetries: 0, errorStrategy: 0 },
    })),
  }
  const result = await page.evaluate(def => (window as unknown as { workflowReview: { roundtrip: (d: unknown) => unknown } }).workflowReview.roundtrip(def), definition)
  expect(result).toEqual(definition)
  const nodes = await page.evaluate(() => (window as unknown as { workflowReview: { nodeTypes: () => string[] } }).workflowReview.nodeTypes())
  expect(nodes[2]).toBe('decision')
  expect(nodes[4]).toBe('wait')
})

for (const status of [403, 409, 500]) {
  for (const action of ['approve', 'reject']) {
    test(`${action} ${status} preserves pending card and confirms no decision`, async ({ page }) => {
      let requestedStep = ''
      await page.route(`**/api/workflow/executions/review-exec/${action}?*`, route => {
        requestedStep = new URL(route.request().url()).searchParams.get('stepId') || ''
        return route.fulfill({ status, json: { error: 'synthetic failure' } })
      })
      await page.goto('/workflow-review-harness.html')
      await page.getByRole('combobox', { name: 'Etapa para decidir' }).selectOption('approve-b')
      await page.getByRole('button', { name: action === 'approve' ? 'Aprovar etapa' : 'Rejeitar etapa' }).click()
      await expect(page.getByText('A decisão não foi confirmada.', { exact: false })).toBeVisible()
      expect(requestedStep).toBe('approve-b')
      await expect(page.getByRole('button', { name: 'Aprovar etapa' })).toBeEnabled()
      await expect(page.getByText('Execução autorizada pelo operador.')).toHaveCount(0)
      await expect(page.getByText('Execução rejeitada e interrompida.')).toHaveCount(0)
      await expect(page.getByRole('combobox', { name: 'Etapa para decidir' })).toHaveValue('approve-b')
    })
  }
}

test('two approvals target selected step and only final approval confirms execution', async ({ page }) => {
  const seen: string[] = []
  await page.route('**/api/workflow/executions/review-exec/approve?*', route => {
    const stepId = new URL(route.request().url()).searchParams.get('stepId')!
    seen.push(stepId)
    return route.fulfill({ json: { ...execution, status: seen.length === 1 ? 3 : 0,
      stepExecutions: execution.stepExecutions.map(step => ({ ...step, status: seen.includes(step.stepId) ? 4 : 3 })) } })
  })
  await page.goto('/workflow-review-harness.html')
  await page.getByRole('combobox', { name: 'Etapa para decidir' }).selectOption('approve-b')
  await page.getByRole('button', { name: 'Aprovar etapa' }).click()
  await expect(page.getByRole('combobox', { name: 'Etapa para decidir' })).toHaveCount(0)
  await expect(page.getByText('Execução autorizada pelo operador.')).toHaveCount(0)
  await page.getByRole('button', { name: 'Aprovar etapa' }).click()
  await expect(page.getByText('Execução autorizada pelo operador.')).toBeVisible()
  expect(seen).toEqual(['approve-b', 'approve-a'])
})
