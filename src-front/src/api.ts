import type { CreateJobPayload, Job, ValidationProblemDetails } from './types'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export class ApiError extends Error {
  readonly status: number
  readonly details?: ValidationProblemDetails | string

  constructor(message: string, status: number, details?: ValidationProblemDetails | string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

async function parseBody(response: Response): Promise<ValidationProblemDetails | string | undefined> {
  const contentType = response.headers.get('content-type') ?? ''

  if (contentType.includes('application/json')) {
    return (await response.json()) as ValidationProblemDetails
  }

  const text = await response.text()
  return text.trim() ? text : undefined
}

function buildErrorMessage(status: number, details?: ValidationProblemDetails | string): string {
  if (typeof details === 'string' && details.trim()) {
    return details
  }

  if (details && typeof details !== 'string' && details.errors) {
    const firstError = Object.values(details.errors).flat()[0]

    if (firstError) {
      return firstError
    }
  }

  if (details && typeof details !== 'string' && details.detail) {
    return details.detail
  }

  if (details && typeof details !== 'string' && details.title) {
    return details.title
  }

  switch (status) {
    case 404:
      return 'Задача не найдена.'
    case 409:
      return 'Задача уже завершилась и не может быть отменена.'
    default:
      return 'Не удалось выполнить запрос к API.'
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
    ...init,
  })

  if (!response.ok) {
    const details = await parseBody(response)
    throw new ApiError(buildErrorMessage(response.status, details), response.status, details)
  }

  if (response.status === 202 || response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const jobsApi = {
  list(): Promise<Job[]> {
    return request<Job[]>('/api/jobs')
  },
  get(id: string): Promise<Job> {
    return request<Job>(`/api/jobs/${id}`)
  },
  create(payload: CreateJobPayload): Promise<Job> {
    return request<Job>('/api/jobs', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    })
  },
  cancel(id: string): Promise<void> {
    return request<void>(`/api/jobs/${id}`, {
      method: 'DELETE',
    })
  },
  wait(id: string): Promise<Job> {
    return request<Job>(`/api/jobs/${id}/wait`)
  },
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Произошла непредвиденная ошибка.'
}
