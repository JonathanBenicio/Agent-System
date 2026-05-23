import { useEffect, useState, useCallback } from 'react'
import { getWorkflowConnection, startWorkflowConnection } from '@/lib/signalr'
import { workflowApi } from '@/lib/api'
import { toast } from 'sonner'

export interface WorkflowStepExecState {
  stepId: string
  stepName: string
  status: number
  errorMessage?: string
  completedAt?: string
  output?: Record<string, unknown>
}

export interface WorkflowExecutionState {
  id: string
  workflowId: string
  workflowName: string
  status: number
  errorMessage?: string
  startedAt?: string
  completedAt?: string
  steps: WorkflowStepExecState[]
}

interface ExecutionStartedEvent {
  status: number
  startedAt?: string
}

interface StepStartedEvent {
  stepId: string
  stepName: string
  status: number
}

interface StepCompletedEvent {
  stepId: string
  stepName: string
  status: number
  output?: Record<string, unknown>
  completedAt?: string
}

interface StepFailedEvent {
  stepId: string
  stepName: string
  status: number
  errorMessage?: string
  completedAt?: string
}

interface ExecutionCompletedEvent {
  status: number
  completedAt?: string
}

interface ExecutionFailedEvent {
  status: number
  errorMessage: string
  completedAt?: string
}

interface ExecutionCancelledEvent {
  status: number
  errorMessage?: string
  completedAt?: string
}

export function useWorkflowExecution(executionId: string, workflowName: string, initialStatus: number = 1) {
  const [execState, setExecState] = useState<WorkflowExecutionState>({
    id: executionId,
    workflowId: '',
    workflowName: workflowName,
    status: initialStatus,
    steps: []
  })
  
  const [approving, setApproving] = useState<boolean>(false)
  const [approvedState, setApprovedState] = useState<'pending' | 'approved' | 'rejected'>('pending')

  useEffect(() => {
    let active = true
    const conn = getWorkflowConnection()

    const handleExecutionStarted = (data: ExecutionStartedEvent) => {
      if (!active) return
      setExecState(prev => ({
        ...prev,
        status: data.status,
        startedAt: data.startedAt
      }))
    }

    const handleStepStarted = (data: StepStartedEvent) => {
      if (!active) return
      setExecState(prev => {
        const stepIndex = prev.steps.findIndex(s => s.stepId === data.stepId)
        const updatedSteps = [...prev.steps]
        const stepData = {
          stepId: data.stepId,
          stepName: data.stepName,
          status: data.status
        }
        if (stepIndex > -1) {
          updatedSteps[stepIndex] = { ...updatedSteps[stepIndex], ...stepData }
        } else {
          updatedSteps.push(stepData)
        }
        return { ...prev, steps: updatedSteps }
      })
    }

    const handleStepCompleted = (data: StepCompletedEvent) => {
      if (!active) return
      setExecState(prev => {
        const stepIndex = prev.steps.findIndex(s => s.stepId === data.stepId)
        const updatedSteps = [...prev.steps]
        const stepData = {
          stepId: data.stepId,
          stepName: data.stepName,
          status: data.status,
          output: data.output,
          completedAt: data.completedAt
        }
        if (stepIndex > -1) {
          updatedSteps[stepIndex] = { ...updatedSteps[stepIndex], ...stepData }
        } else {
          updatedSteps.push(stepData)
        }
        return { ...prev, steps: updatedSteps }
      })
    }

    const handleStepFailed = (data: StepFailedEvent) => {
      if (!active) return
      setExecState(prev => {
        const stepIndex = prev.steps.findIndex(s => s.stepId === data.stepId)
        const updatedSteps = [...prev.steps]
        const stepData = {
          stepId: data.stepId,
          stepName: data.stepName,
          status: data.status,
          errorMessage: data.errorMessage,
          completedAt: data.completedAt
        }
        if (stepIndex > -1) {
          updatedSteps[stepIndex] = { ...updatedSteps[stepIndex], ...stepData }
        } else {
          updatedSteps.push(stepData)
        }
        return { ...prev, steps: updatedSteps }
      })
    }

    const handleExecutionCompleted = (data: ExecutionCompletedEvent) => {
      if (!active) return
      setExecState(prev => ({
        ...prev,
        status: data.status,
        completedAt: data.completedAt
      }))
      toast.success(`Workflow "${workflowName}" concluído com sucesso!`)
    }

    const handleExecutionFailed = (data: ExecutionFailedEvent) => {
      if (!active) return
      setExecState(prev => ({
        ...prev,
        status: data.status,
        errorMessage: data.errorMessage,
        completedAt: data.completedAt
      }))
      toast.error(`Workflow "${workflowName}" falhou: ${data.errorMessage}`)
    }

    const handleExecutionCancelled = (data: ExecutionCancelledEvent) => {
      if (!active) return
      setExecState(prev => ({
        ...prev,
        status: data.status,
        errorMessage: data.errorMessage || 'Cancelado pelo usuário',
        completedAt: data.completedAt
      }))
    }

    conn.on('ExecutionStarted', handleExecutionStarted)
    conn.on('StepStarted', handleStepStarted)
    conn.on('StepCompleted', handleStepCompleted)
    conn.on('StepFailed', handleStepFailed)
    conn.on('ExecutionCompleted', handleExecutionCompleted)
    conn.on('ExecutionFailed', handleExecutionFailed)
    conn.on('ExecutionCancelled', handleExecutionCancelled)

    startWorkflowConnection()
      .then(async () => {
        if (conn.state === 'Connected') {
          await conn.invoke('SubscribeToWorkflow', executionId)
            .catch(err => console.error('Failed to subscribe to workflow on hub:', err))
        }
      })
      .catch(err => console.error('Workflow SignalR connection failed:', err))

    const fetchInitialState = async () => {
      try {
        const data = await workflowApi.getExecution(executionId)
        if (active) {
          setExecState({
            id: data.id,
            workflowId: data.workflowId,
            workflowName: data.workflowName || workflowName,
            status: data.status,
            errorMessage: data.errorMessage,
            startedAt: data.startedAt,
            completedAt: data.completedAt,
            steps: data.stepExecutions?.map(s => ({
              stepId: s.stepId,
              stepName: s.stepName,
              status: s.status,
              errorMessage: s.errorMessage,
              completedAt: s.completedAt,
              output: s.output
            })) || []
          })
          if (data.status === 3) setApprovedState('pending')
        }
      } catch (err) {
        console.error('Failed to load initial workflow execution state:', err)
      }
    }
    
    void fetchInitialState()

    return () => {
      active = false
      conn.off('ExecutionStarted', handleExecutionStarted)
      conn.off('StepStarted', handleStepStarted)
      conn.off('StepCompleted', handleStepCompleted)
      conn.off('StepFailed', handleStepFailed)
      conn.off('ExecutionCompleted', handleExecutionCompleted)
      conn.off('ExecutionFailed', handleExecutionFailed)
      conn.off('ExecutionCancelled', handleExecutionCancelled)
      
      if (conn.state === 'Connected') {
        conn.invoke('UnsubscribeFromWorkflow', executionId)
          .catch(err => console.warn('Failed to unsubscribe from workflow:', err))
      }
    }
  }, [executionId, workflowName])

  const handleApprove = useCallback(async () => {
    setApproving(true)
    try {
      await workflowApi.approveStep(executionId).catch(() => null)
      setApprovedState('approved')
      toast.success('Etapa aprovada com sucesso! Continuando execução do workflow.')
      setExecState(prev => ({ ...prev, status: 1 }))
    } catch (err) {
      console.error(err)
    } finally {
      setApproving(false)
    }
  }, [executionId])

  const handleReject = useCallback(async () => {
    setApproving(true)
    try {
      await workflowApi.rejectStep(executionId).catch(() => null)
      setApprovedState('rejected')
      toast.error('Etapa rejeitada. Cancelando execução do workflow.')
      setExecState(prev => ({ ...prev, status: 6, errorMessage: 'Rejeitado pelo revisor humano' }))
    } catch (err) {
      console.error(err)
    } finally {
      setApproving(false)
    }
  }, [executionId])

  return {
    execState,
    approving,
    approvedState,
    handleApprove,
    handleReject
  }
}
