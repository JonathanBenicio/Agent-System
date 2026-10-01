import { useEffect } from 'react'
import { getOnnxConnection } from '@/lib/signalr'
import { useToast } from '@/components/shared/Toast'
import { useQueryClient } from '@tanstack/react-query'
import { JOBS_QUERY_KEY } from '@/hooks/useOnnxModels'

export function OnnxJobListener() {
  const { addToast } = useToast()
  const queryClient = useQueryClient()

  useEffect(() => {
    const conn = getOnnxConnection()

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const handleJobStatusChanged = (payload: any) => {
      const status = payload.status || payload.Status
      const jobId = payload.jobId || payload.JobId
      const error = payload.error || payload.Error

      // Invalidate the ONNX jobs query so any active gallery refreshes immediately
      queryClient.invalidateQueries({ queryKey: JOBS_QUERY_KEY })

      if (status === 'Completed') {
        addToast(`🎉 Inferência ONNX concluída com sucesso! (Job: ${jobId.substring(0, 8)})`, 'success')
      } else if (status === 'Failed') {
        addToast(`❌ Falha na inferência ONNX: ${error || 'Erro desconhecido'} (Job: ${jobId.substring(0, 8)})`, 'error')
      }
    }

    conn.on('JobStatusChanged', handleJobStatusChanged)

    return () => {
      conn.off('JobStatusChanged', handleJobStatusChanged)
    }
  }, [addToast, queryClient])

  return null
}
