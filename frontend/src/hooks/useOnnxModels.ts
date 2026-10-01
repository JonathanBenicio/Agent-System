import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { onnxModelApi } from '@/lib/api'
import type { OnnxModelDetail } from '@/types/api'

const QUERY_KEY = ['onnx-models'] as const

export function useOnnxModelsList() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => onnxModelApi.list(),
  })
}

export function useUploadOnnxModel() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (formData: FormData) => onnxModelApi.create(formData),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEY })
    },
  })
}

export function useUpdateOnnxModel() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<OnnxModelDetail> }) =>
      onnxModelApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEY })
    },
  })
}

export function useDeleteOnnxModel() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => onnxModelApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEY })
    },
  })
}

export function useInspectOnnxModel() {
  return useMutation({
    mutationFn: (id: string) => onnxModelApi.inspect(id),
  })
}

export function useTestOnnxModel() {
  return useMutation({
    mutationFn: ({ id, formData }: { id: string; formData: FormData }) =>
      onnxModelApi.test(id, formData),
  })
}

export const JOBS_QUERY_KEY = ['onnx-jobs'] as const

export function useOnnxJobsList(modelId?: string, page = 1, pageSize = 10) {
  return useQuery({
    queryKey: [...JOBS_QUERY_KEY, modelId, page, pageSize] as const,
    queryFn: () => onnxModelApi.listJobs(modelId, page, pageSize),
    refetchInterval: 5000, // Fallback polling
  })
}

export function useDeleteOnnxJob() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (jobId: string) => onnxModelApi.deleteJob(jobId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: JOBS_QUERY_KEY })
    },
  })
}
