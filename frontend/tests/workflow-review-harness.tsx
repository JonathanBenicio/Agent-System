// Test-only browser harness: real workflow card/hook/store; API is mocked by Playwright.
import { createRoot } from 'react-dom/client'
import { WorkflowExecutionCard } from '../src/components/chat/WorkflowExecutionCard'
import { useWorkflowStore } from '../src/store/useWorkflowStore'
import { Toaster } from 'sonner'
import '../src/index.css'
import type { WorkflowDefinition } from '../src/types/api'

export function Harness() {
  return <><WorkflowExecutionCard executionId="review-exec" workflowName="Review" initialStatus={3} /><Toaster /></>
}

Object.assign(window, {
  workflowReview: {
    roundtrip: (definition: WorkflowDefinition) => {
      useWorkflowStore.getState().fromWorkflowDefinition(definition)
      return useWorkflowStore.getState().toWorkflowDefinition()
    },
    nodeTypes: () => useWorkflowStore.getState().nodes.map(node => node.type),
  },
})
createRoot(document.getElementById('root')!).render(<Harness />)
