export type JobStatus =
  | 'Created'
  | 'Queued'
  | 'Running'
  | 'Succeeded'
  | 'Failed'
  | 'Cancelled'
  | 'TimedOut'

export interface Job {
  id: string
  status: JobStatus
  progress: number
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  error: string | null
}

export interface CreateJobPayload {
  duration: string
  shouldFail: boolean
  timeout?: string | null
}

export interface ValidationProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export interface Notice {
  tone: 'info' | 'success' | 'error'
  title: string
  message: string
}
